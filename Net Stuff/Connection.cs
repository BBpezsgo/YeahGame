using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using YeahGame.Messages;
using Thread = System.Threading.Thread;

namespace YeahGame;

class UdpClient
{
    public readonly Pipe Pipe;
    public readonly ConcurrentQueue<byte[]> IncomingQueue;
    public readonly IPEndPoint RemoteEndPoint;
    public bool IsAlive;
    public readonly bool DebugLog;

    readonly Thread ListeningThread;

    public UdpClient(UdpReceiveResult udpReceiveResult, bool debugLog = false)
    {
        Pipe = new Pipe();
        RemoteEndPoint = udpReceiveResult.RemoteEndPoint;
        IsAlive = true;
        IncomingQueue = new ConcurrentQueue<byte[]>();
        ListeningThread = new Thread(Listen);
        ListeningThread.Start();
        DebugLog = debugLog;
    }

    async void Listen()
    {
        if (DebugLog) Debug.WriteLine($"[Net]: Listening for {RemoteEndPoint} ...");
        while (IsAlive)
        {
            ReadResult readResult = await Pipe.Reader.ReadAsync();
            if (!IsAlive) break;
            IncomingQueue.Enqueue(readResult.Buffer.FirstSpan.ToArray());
            Pipe.Reader.AdvanceTo(readResult.Buffer.End);
        }
        if (DebugLog) Debug.WriteLine($"[Net]: Listening for {RemoteEndPoint} aborted");
    }

    public void Dispose()
    {
        IsAlive = false;
    }
}

public class Connection
{
    readonly ConcurrentQueue<UdpReceiveResult> IncomingQueue = new();
    readonly Queue<Message> OutgoingQueue = new();

    System.Net.Sockets.UdpClient? UdpSocket = null;
    readonly Thread ListeningThread;
    bool IsServer;

    bool ShouldListen;

    readonly ConcurrentDictionary<string, UdpClient> Connections = new();

    bool ConnectedToServer;

    public Connection()
    {
        ListeningThread = new Thread(Listen);
    }

    public void Client(IPAddress address, int port)
    {
        IsServer = false;
        UdpSocket = new System.Net.Sockets.UdpClient();
        UdpSocket.Connect(address, port);
        ShouldListen = true;
        ListeningThread.Start();
        Send(new NetControlMessage(NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY));
    }

    public void Server(IPAddress address, int port)
    {
        IsServer = true;
        UdpSocket = new System.Net.Sockets.UdpClient(new IPEndPoint(address, port));
        ShouldListen = true;
        ListeningThread.Start();
    }

    async void Listen()
    {
        if (UdpSocket == null) return;

        while (ShouldListen)
        {
            try
            {
                UdpReceiveResult result = await UdpSocket.ReceiveAsync();

                if (!ShouldListen) break;

                if (IsServer)
                {
                    if (Connections.TryGetValue(result.RemoteEndPoint.ToString(), out UdpClient? client))
                    {
                        await client.Pipe.Writer.WriteAsync(result.Buffer);
                    }
                    else
                    {
                        UdpClient newClient = new(result);
                        Connections.TryAdd(result.RemoteEndPoint.ToString(), new UdpClient(result));

                        await newClient.Pipe.Writer.WriteAsync(result.Buffer);

                        Console.WriteLine($"Client {result.RemoteEndPoint} connected");
                    }
                }
                else
                {
                    ConnectedToServer = true;
                }

                IncomingQueue.Enqueue(result);
            }
            catch (SocketException)
            { break; }
        }
    }

    public void Close()
    {
        ShouldListen = false;
        foreach (KeyValuePair<string, UdpClient> client in Connections)
        {
            client.Value.Dispose();
        }
        UdpSocket?.Close();
        UdpSocket?.Dispose();
    }

    public void Receive()
    {
        while (IncomingQueue.TryDequeue(out UdpReceiveResult messageIn))
        {
            OnReceiveInternal(messageIn.RemoteEndPoint, messageIn.Buffer);
        }

        while (OutgoingQueue.TryDequeue(out Message? messageOut))
        {
            SendImmediate(messageOut);
        }

        List<string> shouldRemove = new();

        foreach (KeyValuePair<string, UdpClient> client in Connections)
        {
            if (!client.Value.IsAlive)
            {
                shouldRemove.Add(client.Key);
                continue;
            }

            while (client.Value.IncomingQueue.TryDequeue(out byte[]? _message))
            {
                OnReceiveInternal(client.Value.RemoteEndPoint, _message);
            }
        }

        for (int i = shouldRemove.Count - 1; i >= 0; i--)
        {
            if (Connections.TryRemove(shouldRemove[i], out UdpClient? removedClient))
            { removedClient?.Dispose(); }
        }
    }

    void OnReceiveInternal(IPEndPoint source, byte[] buffer)
    {
        Console.WriteLine($"Received {buffer.Length} bytes from {source}");
        if (buffer.Length == 0) return;
        MessageType messageType = (MessageType)buffer[0];
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        switch (messageType)
        {
            case MessageType.CONTROL:
            {
                NetControlMessage message = new();
                message.Deserialize(reader);
                FeedControlMessage(source, message);
                break;
            }
            default:
                break;
        }
    }

    protected void Send(byte[] data)
    {
        if (UdpSocket == null) return;

        if (IsServer)
        {
            foreach (KeyValuePair<string, UdpClient> client in Connections)
            {
                Console.WriteLine($"Send {data.Length} bytes to client {client.Value.RemoteEndPoint}");
                UdpSocket.Send(data, data.Length, client.Value.RemoteEndPoint);
            }
        }
        else
        {
                Console.WriteLine($"Send {data.Length} bytes to sever");
            UdpSocket.Send(data, data.Length);
        }
    }

    protected void SendTo(byte[] data, IPEndPoint destination)
    {
        if (UdpSocket == null) return;
        if (!IsServer) return;

        foreach (KeyValuePair<string, UdpClient> client in Connections)
        {
            if (!client.Value.RemoteEndPoint.Equals(destination)) continue;

            UdpSocket.Send(data, data.Length, client.Value.RemoteEndPoint);

            break;
        }
    }

    public void FeedControlMessage(IPEndPoint source, NetControlMessage netControlMessage)
    {
        if (UdpSocket == null) return;

        if (netControlMessage.Type == MessageType.CONTROL)
        {
            switch (netControlMessage.Kind)
            {
                case NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY:
                {
                    Console.WriteLine("Received PLS message");
                    SendTo(Utils.Serialize(new NetControlMessage(NetControlMessageKind.HEY_CLIENT_IM_SERVER)), source);
                    return;
                }
                case NetControlMessageKind.HEY_CLIENT_IM_SERVER:
                {
                    Console.WriteLine("Received HEY message");
                    ConnectedToServer = true;
                    return;
                }
                default: return;
            }
        }
    }

    public void Dispose() => UdpSocket?.Dispose();

    public void Send(Message message) => OutgoingQueue.Enqueue(message);

    public void SendImmediate(Message message) => Send(Utils.Serialize(message));
}
