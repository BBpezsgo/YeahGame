﻿using System.Collections.Concurrent;
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
        public readonly ConcurrentQueue<byte[]> OutgoingQueue;
        public readonly IPEndPoint EndPoint;

        public double ReceivedAt;
        public double SentAt;

        public UdpClient(IPEndPoint endPoint)
        {
            IncomingQueue = new ConcurrentQueue<byte[]>();
            OutgoingQueue = new ConcurrentQueue<byte[]>();
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
    public delegate void MessageReceivedEventHandler(Message message, IPEndPoint source);

    #endregion

    public const double Timeout = 10;
    public const double PingInterval = 5;

    readonly ConcurrentQueue<UdpMessage> IncomingQueue;
    readonly Queue<Message> OutgoingQueue;

    public event ClientConnectedEventHandler? OnClientConnected;
    public event ClientDisconnectedEventHandler? OnClientDisconnected;
    public event ConnectedToServerEventHandler? OnConnectedToServer;
    public event DisconnectedFromServerEventHandler? OnDisconnectedFromServer;
    public event MessageReceivedEventHandler? OnMessageReceived;

    readonly ConcurrentDictionary<string, UdpClient> Connections;

    [MemberNotNullWhen(true, nameof(UdpSocket))]
    public bool IsConnected => UdpSocket != null;

    public IPEndPoint? ServerAddress
    {
        get
        {
            try
            { return (!IsConnected || isServer) ? null : (IPEndPoint?)UdpSocket?.Client?.RemoteEndPoint; }
            catch (ObjectDisposedException)
            { return null; }
        }
    }
    public IPEndPoint? LocalAddress
    {
        get
        {
            try
            { return (!IsConnected) ? null : (IPEndPoint?)(UdpSocket?.Client?.LocalEndPoint); }
            catch (ObjectDisposedException)
            { return null; }
        }
    }
    public bool IsServer => isServer;

    public IReadOnlyList<IPEndPoint> Clients => Connections.Values
        .Select(client => client.EndPoint)
        .ToList();

    public double ReceivedAt;
    public double SentAt;

    System.Net.Sockets.UdpClient? UdpSocket;
    Thread ListeningThread;
    bool isServer;

    bool ShouldListen;

    public Connection()
    {
        IncomingQueue = new ConcurrentQueue<UdpMessage>();
        OutgoingQueue = new Queue<Message>();

        Connections = new ConcurrentDictionary<string, UdpClient>();

        ListeningThread = new Thread(Listen) { Name = "UDP Listener" };
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
                Debug.WriteLine($"[Net]: Error ({ex.ErrorCode}) ({ex.NativeErrorCode}) ({ex.SocketErrorCode}): {ex.Message}");

                if (!isServer)
                {
                    Close();
                    break;
                }
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

        ListeningThread = new Thread(Listen);
    }

    #endregion

    #region Message Handling

    public void Tick()
    {
        int endlessLoop = 500;

        while (IncomingQueue.TryDequeue(out UdpMessage messageIn))
        {
            OnReceiveInternal(messageIn.Source, messageIn.Buffer);

            if (endlessLoop-- < 0)
            { break; }
        }

        while (OutgoingQueue.TryDequeue(out Message? messageOut))
        { SendImmediate(messageOut); }

        List<string> shouldRemove = new();

        foreach (KeyValuePair<string, UdpClient> client in Connections)
        {
            while (client.Value.OutgoingQueue.TryDequeue(out byte[]? messageOut))
            {
                SendImmediateTo(messageOut, client.Value.EndPoint);
            }

            while (client.Value.IncomingQueue.TryDequeue(out byte[]? messageIn))
            {
                OnReceiveInternal(client.Value.EndPoint, messageIn);
            }

            if (Time.NowNoCache - client.Value.ReceivedAt > PingInterval &&
                Time.NowNoCache - client.Value.SentAt > PingInterval)
            {
                Debug.WriteLine($"[Net]: =={client.Value.EndPoint}=> PING");
                SendImmediateTo(new NetControlMessage(NetControlMessageKind.PING), client.Value.EndPoint);
            }

            if (Time.NowNoCache - client.Value.ReceivedAt > Timeout)
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
            if (Time.NowNoCache - ReceivedAt > PingInterval &&
                Time.NowNoCache - SentAt > PingInterval)
            {
                Debug.WriteLine($"[Net]: =={ServerAddress}=> PING");
                SendImmediate(new NetControlMessage(NetControlMessageKind.PING));
            }

            if (Time.NowNoCache - ReceivedAt > Timeout && ShouldListen)
            {
                Debug.WriteLine($"[Net]: Server idling too long, disconnecting");
                Close();
            }
        }
    }

    void OnReceiveInternal(IPEndPoint source, byte[] buffer)
    {
        if (buffer.Length == 0) return;

        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            MessageType messageType = (MessageType)buffer[reader.BaseStream.Position];
            Message message;

            switch (messageType)
            {
                case MessageType.Control:
                {
                    NetControlMessage _message = new();
                    _message.Deserialize(reader);
                    FeedControlMessage(source, _message);
                    return;
                }

                case MessageType.ObjectSync:
                    message = new ObjectSyncMessage();
                    message.Deserialize(reader);
                    break;

                case MessageType.ObjectControl:
                    message = new ObjectControlMessage();
                    message.Deserialize(reader);
                    break;

                default:
                    throw new NotImplementedException();
            }

            Debug.WriteLine($"[Net]: <= {source} == {message}");

            OnMessageReceived?.Invoke(message, source);
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
            case MessageType.Control:
            {
                switch (netControlMessage.Kind)
                {
                    case NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY:
                    {
                        SendImmediateTo(new NetControlMessage(NetControlMessageKind.HEY_CLIENT_IM_SERVER), source);
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
                        SendImmediateTo(new NetControlMessage(NetControlMessageKind.PONG), source);
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

    public void SendTo(Message message, IPEndPoint destination)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;
        if (isServer)
        {
            foreach (KeyValuePair<string, UdpClient> client in Connections)
            {
                if (!client.Value.EndPoint.Equals(destination)) continue;

                client.Value.OutgoingQueue.Enqueue(Utils.Serialize(message));
            }
        }
        else
        {
            if (UdpSocket.Client.RemoteEndPoint?.ToString() == destination.ToString())
            {
                Send(message);
            }
        }
    }

    public void SendImmediateTo<T>(T data, IPEndPoint destination) where T : ISerializable
        => SendImmediateTo(Utils.Serialize(data), destination);

    public void SendImmediateTo(byte[] data, IPEndPoint destination)
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
