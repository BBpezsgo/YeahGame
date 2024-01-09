using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using YeahGame.Messages;

using UdpMessage = (byte[] Buffer, System.Net.IPEndPoint Source);

namespace YeahGame;

public class Connection
{
    #region Types'n Stuff

    class UdpClient
    {
        public readonly ConcurrentQueue<byte[]> IncomingQueue;
        public readonly IPEndPoint EndPoint;

        public double ReceivedAt;
        public double SentAt;

        public UdpClient(IPEndPoint endPoint)
        {
            IncomingQueue = new ConcurrentQueue<byte[]>();
            EndPoint = endPoint;

            ReceivedAt = Time.NowNoCache;
            SentAt = Time.NowNoCache;
        }
    }

    public enum ConnectingPhase
    {
        Connected,
        Handshake,
    }

    public delegate void ClientConnectedEventHandler(IPEndPoint client, ConnectingPhase phase);
    public delegate void ClientDisconnectedEventHandler(IPEndPoint client);
    public delegate void ConnectedToServerEventHandler(ConnectingPhase phase);
    public delegate void DisconnectedFromServerEventHandler();

    #endregion

    readonly ConcurrentQueue<UdpMessage> IncomingQueue;
    readonly Queue<Message> OutgoingQueue;

    public event ClientConnectedEventHandler? OnClientConnected;
    public event ClientDisconnectedEventHandler? OnClientDisconnected;
    public event ConnectedToServerEventHandler? OnConnectedToServer;
    public event DisconnectedFromServerEventHandler? OnDisconnectedFromServer;

    readonly ConcurrentDictionary<string, UdpClient> Connections;

    [MemberNotNullWhen(true, nameof(UdpSocket))]
    public bool IsConnected => UdpSocket != null;

    public IPEndPoint? ServerAddress => (!IsConnected || isServer) ? null : (IPEndPoint?)UdpSocket.Client.RemoteEndPoint;

    public bool IsServer => isServer;

    public IReadOnlyList<IPEndPoint> Clients => Connections.Values
        .Select(client => client.EndPoint)
        .ToList();

    public double ReceivedAt;
    public double SentAt;

    System.Net.Sockets.UdpClient? UdpSocket;
    readonly Thread ListeningThread;
    bool isServer;

    bool ShouldListen;

    public Connection()
    {
        IncomingQueue = new ConcurrentQueue<UdpMessage>();
        OutgoingQueue = new Queue<Message>();

        Connections = new ConcurrentDictionary<string, UdpClient>();

        ListeningThread = new Thread(Listen);
    }

    #region UDP Stuff

    public void Client(IPAddress address, int port)
    {
        isServer = false;
        UdpSocket = new System.Net.Sockets.UdpClient();
        UdpSocket.Connect(address, port);
        ShouldListen = true;
        ListeningThread.Start();
        OnConnectedToServer?.Invoke(ConnectingPhase.Connected);
        ReceivedAt = Time.NowNoCache;
        SentAt = Time.NowNoCache;
        Send(new NetControlMessage(NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY));
    }

    public void Server(IPAddress address, int port)
    {
        isServer = true;
        UdpSocket = new System.Net.Sockets.UdpClient(new IPEndPoint(address, port));
        ShouldListen = true;
        ListeningThread.Start();
        ReceivedAt = Time.NowNoCache;
        SentAt = Time.NowNoCache;
    }

    void Listen()
    {
        if (!IsConnected) return;

        while (ShouldListen)
        {
            try
            {
                IPEndPoint source = new(IPAddress.Any, 0);
                byte[] buffer = UdpSocket.Receive(ref source);

                if (!ShouldListen) break;

                ReceivedAt = Time.NowNoCache;

                if (isServer)
                {
                    if (!Connections.TryGetValue(source.ToString(), out UdpClient? client))
                    {
                        client = new UdpClient(source);
                        Connections.TryAdd(source.ToString(), client);
                        OnClientConnected?.Invoke(source, ConnectingPhase.Connected);
                    }

                    client.IncomingQueue.Enqueue(buffer);
                }
                else
                {
                    IncomingQueue.Enqueue(new UdpMessage(buffer, source));
                }
            }
            catch (SocketException ex)
            {
                Close();
                Debug.WriteLine($"[Net]: Error ({ex.ErrorCode}) ({ex.NativeErrorCode}) ({ex.SocketErrorCode}): {ex.Message}");
                break;
            }
        }
    }

    public void Close()
    {
        ShouldListen = false;
        Connections.Clear();
        UdpSocket?.Close();
        UdpSocket?.Dispose();
        UdpSocket = null;

        if (!isServer)
        { OnDisconnectedFromServer?.Invoke(); }
    }

    #endregion

    #region Message Handling

    public void Tick()
    {
        while (IncomingQueue.TryDequeue(out UdpMessage messageIn))
        { OnReceiveInternal(messageIn.Source, messageIn.Buffer); }

        while (OutgoingQueue.TryDequeue(out Message? messageOut))
        { SendImmediate(messageOut); }

        List<string> shouldRemove = new();

        foreach (KeyValuePair<string, UdpClient> client in Connections)
        {
            while (client.Value.IncomingQueue.TryDequeue(out byte[]? messageIn))
            {
                OnReceiveInternal(client.Value.EndPoint, messageIn);
            }

            if (Time.NowNoCache - client.Value.ReceivedAt > 5d &&
                Time.NowNoCache - client.Value.SentAt > 5d)
            {
                Debug.WriteLine($"[Net]: =={client.Value.EndPoint}=> PING");
                SendSendImmediateTo(new NetControlMessage(NetControlMessageKind.PING), client.Value.EndPoint);
            }

            if (Time.NowNoCache - client.Value.ReceivedAt > 10d)
            {
                Debug.WriteLine($"[Net]: Kicking client {client.Value.EndPoint} for idling too long");
                shouldRemove.Add(client.Key);
                continue;
            }
        }

        for (int i = 0; i < shouldRemove.Count; i++)
        {
            if (Connections.TryRemove(shouldRemove[i], out UdpClient? removedClient))
            { OnClientDisconnected?.Invoke(removedClient.EndPoint); }
        }

        if (!isServer && IsConnected)
        {
            if (Time.NowNoCache - ReceivedAt > 5d &&
                Time.NowNoCache - SentAt > 5d)
            {
                Debug.WriteLine($"[Net]: =={ServerAddress}=> PING");
                SendImmediate(new NetControlMessage(NetControlMessageKind.PING));
            }

            if (Time.NowNoCache - ReceivedAt > 10d && ShouldListen)
            {
                Debug.WriteLine($"[Net]: Server idling too long, disconnecting");
                Close();
            }
        }
    }

    void OnReceiveInternal(IPEndPoint source, byte[] buffer)
    {
        if (buffer.Length == 0) return;

        MessageType messageType = (MessageType)buffer[0];

        switch (messageType)
        {
            case MessageType.CONTROL:
            {
                NetControlMessage message = Utils.Deserialize<NetControlMessage>(buffer);
                FeedControlMessage(source, message);
                break;
            }
            default:
                break;
        }
    }

    #endregion

    void FeedControlMessage(IPEndPoint source, NetControlMessage netControlMessage)
    {
        if (!IsConnected) return;

        if (Connections.TryGetValue(source.ToString(), out UdpClient? client))
        { client.ReceivedAt = Time.NowNoCache; }

        switch (netControlMessage.Type)
        {
            case MessageType.CONTROL:
            {
                switch (netControlMessage.Kind)
                {
                    case NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY:
                    {
                        SendSendImmediateTo(new NetControlMessage(NetControlMessageKind.HEY_CLIENT_IM_SERVER), source);
                        OnClientConnected?.Invoke(source, ConnectingPhase.Handshake);
                        return;
                    }
                    case NetControlMessageKind.HEY_CLIENT_IM_SERVER:
                    {
                        OnConnectedToServer?.Invoke(ConnectingPhase.Handshake);
                        return;
                    }
                    case NetControlMessageKind.IM_THERE:
                    {
                        return;
                    }
                    case NetControlMessageKind.PING:
                    {
                        Debug.WriteLine($"[Net]: =={source}=> PONG");
                        SendSendImmediateTo(new NetControlMessage(NetControlMessageKind.PONG), source);
                        return;
                    }
                    case NetControlMessageKind.PONG:
                    {
                        Debug.WriteLine($"[Net]: <={source}== PONG");
                        return;
                    }
                    default: return;
                }
            }
            default:
                break;
        }
    }

    #region Message Sending

    public void SendImmediate<T>(T data) where T : ISerializable
        => SendImmediate(Utils.Serialize(data));

    public void SendImmediate(byte[] data)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;
        if (isServer)
        {
            foreach (KeyValuePair<string, UdpClient> client in Connections)
            {
                UdpSocket.Send(data, data.Length, client.Value.EndPoint);
            }
        }
        else
        {
            UdpSocket.Send(data, data.Length);
        }
    }

    public void SendSendImmediateTo<T>(T data, IPEndPoint destination) where T : ISerializable
        => SendSendImmediateTo(Utils.Serialize(data), destination);

    public void SendSendImmediateTo(byte[] data, IPEndPoint destination)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;
        if (isServer)
        {
            foreach (KeyValuePair<string, UdpClient> client in Connections)
            {
                if (!client.Value.EndPoint.Equals(destination)) continue;

                UdpSocket.Send(data, data.Length, client.Value.EndPoint);
                client.Value.SentAt = Time.NowNoCache;

                break;
            }
        }
        else
        {
            if (UdpSocket.Client.RemoteEndPoint?.ToString() == destination.ToString())
            {
                UdpSocket.Send(data, data.Length);
            }
        }
    }

    public void Send(Message message) => OutgoingQueue.Enqueue(message);

    #endregion
}
