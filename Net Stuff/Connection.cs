using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using YeahGame.Messages;

using UdpMessage = (byte[] Buffer, System.Net.IPEndPoint Source);

namespace YeahGame;

public class Connection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> where T : ISerializable
{
    #region Types'n Stuff

    class UdpClient<T2> where T2 : ISerializable
    {
        public readonly ConcurrentQueue<byte[]> IncomingQueue;
        public readonly ConcurrentQueue<byte[]> OutgoingQueue;
        public readonly IPEndPoint EndPoint;

        public double ReceivedAt;
        public double SentAt;

        public T2? Data;

        public UdpClient(IPEndPoint endPoint, T2? data)
        {
            IncomingQueue = new ConcurrentQueue<byte[]>();
            OutgoingQueue = new ConcurrentQueue<byte[]>();
            EndPoint = endPoint;

            ReceivedAt = Time.NowNoCache;
            SentAt = Time.NowNoCache;

            Data = data;
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

    #region Constants

    public const double Timeout = 10d;
    public const double PingInterval = 5d;

    #endregion

    #region Events

    public event ClientConnectedEventHandler? OnClientConnected;
    public event ClientDisconnectedEventHandler? OnClientDisconnected;
    public event ConnectedToServerEventHandler? OnConnectedToServer;
    public event DisconnectedFromServerEventHandler? OnDisconnectedFromServer;
    public event MessageReceivedEventHandler? OnMessageReceived;

    #endregion

    public T? LocalUserInfo;

    readonly ConcurrentQueue<UdpMessage> IncomingQueue;
    readonly Queue<Message> OutgoingQueue;

    public ICollection<string> Connections => _connections.Keys;
    readonly ConcurrentDictionary<string, UdpClient<T>> _connections;

    public IReadOnlyDictionary<string, (T Info, bool IsServer)> PlayerInfos => _playerInfos;
    readonly Dictionary<string, (T Info, bool IsServer)> _playerInfos;

    public double ReceivedAt;
    public double SentAt;

    UdpClient? UdpSocket;
    Thread ListeningThread;
    bool isServer;

    bool ShouldListen;

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

    [MemberNotNullWhen(true, nameof(UdpSocket))]
    public bool IsConnected => UdpSocket != null;

    public IReadOnlyList<IPEndPoint> Clients => _connections.Values
        .Select(client => client.EndPoint)
        .ToList();

    public Connection()
    {
        IncomingQueue = new ConcurrentQueue<UdpMessage>();
        OutgoingQueue = new Queue<Message>();

        _connections = new ConcurrentDictionary<string, UdpClient<T>>();

        _playerInfos = new Dictionary<string, (T, bool)>();

        ListeningThread = new Thread(Listen) { Name = "UDP Listener" };
    }

    #region Connection Handling

    public void Client(IPAddress address, int port)
    {
        isServer = false;
        UdpSocket = new UdpClient();
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
        UdpSocket = new UdpClient(new IPEndPoint(address, port));
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
                    if (!_connections.TryGetValue(source.ToString(), out UdpClient<T>? client))
                    {
                        client = new UdpClient<T>(source, default);
                        _connections.TryAdd(source.ToString(), client);
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
        _connections.Clear();
        UdpSocket?.Close();
        UdpSocket?.Dispose();
        UdpSocket = null;

        if (!isServer)
        { OnDisconnectedFromServer?.Invoke(); }

        ListeningThread = new Thread(Listen);
    }

    void FeedControlMessage(IPEndPoint source, NetControlMessage netControlMessage)
    {
        if (!IsConnected) return;

        if (_connections.TryGetValue(source.ToString(), out UdpClient<T>? client))
        { client.ReceivedAt = Time.NowNoCache; }

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

    #endregion

    #region Message Handling & Receiving

    public void Tick()
    {
        int endlessLoop = 500;

        while (IncomingQueue.TryDequeue(out UdpMessage messageIn))
        {
            OnReceiveInternal(messageIn.Source, messageIn.Buffer);

            if (endlessLoop-- < 0)
            { break; }
        }

        while (OutgoingQueue.TryDequeue(out Message? messageOut) && IsConnected)
        { SendImmediate(messageOut); }

        List<string> shouldRemove = new();

        foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
        {
            while (client.Value.OutgoingQueue.TryDequeue(out byte[]? messageOut) && IsConnected)
            {
                SendImmediateTo(messageOut, client.Value.EndPoint);
            }

            while (client.Value.IncomingQueue.TryDequeue(out byte[]? messageIn))
            {
                OnReceiveInternal(client.Value.EndPoint, messageIn);
            }

            if (Time.NowNoCache - client.Value.ReceivedAt > PingInterval &&
                Time.NowNoCache - client.Value.SentAt > PingInterval &&
                IsConnected)
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
            if (_connections.TryRemove(shouldRemove[i], out UdpClient<T>? removedClient))
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
                    NetControlMessage _message = new(reader);
                    FeedControlMessage(source, _message);
                    return;
                }

                case MessageType.ObjectSync:
                    message = new ObjectSyncMessage(reader);
                    break;

                case MessageType.ObjectControl:
                    message = new ObjectControlMessage(reader);
                    break;

                case MessageType.RPC:
                    message = new RPCMessage(reader);
                    break;

                case MessageType.InfoResponse:
                {
                    InfoResponseMessage _message = new(reader);

                    Debug.WriteLine($"[Net]: <= {source} == {_message}");

                    if (IsServer)
                    {
                        if (_message.Source is not null)
                        { _playerInfos[_message.Source.ToString()] = (Utils.Deserialize<T>(_message.Details), false); }
                        else
                        { _playerInfos[source.ToString()] = (Utils.Deserialize<T>(_message.Details), false); }

                        Send(_message);
                    }
                    else
                    {
                        if (_message.Source is not null)
                        { _playerInfos[_message.Source.ToString()] = (Utils.Deserialize<T>(_message.Details), false); }
                        else if (_message.IsServer && ServerAddress is not null)
                        { _playerInfos[ServerAddress.ToString()] = (Utils.Deserialize<T>(_message.Details), true); }
                    }

                    return;
                }

                case MessageType.InfoRequest:
                {
                    InfoRequestMessage _message = new(reader);

                    Debug.WriteLine($"[Net]: <= {source} == {_message}");

                    if (IsServer)
                    {
                        if (_message.FromServer)
                        {
                            if (LocalUserInfo is not null)
                            {
                                SendTo(new InfoResponseMessage()
                                {
                                    IsServer = true,
                                    Source = null,
                                    Details = Utils.Serialize(LocalUserInfo),
                                }, source);
                            }
                        }
                        else
                        {
                            if (_message.From is not null)
                            {
                                if (_playerInfos.TryGetValue(_message.From.ToString(), out (T Info, bool IsServer) info))
                                {
                                    SendTo(new InfoResponseMessage()
                                    {
                                        IsServer = false,
                                        Source = _message.From,
                                        Details = Utils.Serialize(info.Info),
                                    }, source);
                                }
                                else
                                {
                                    SendTo(new InfoRequestMessage()
                                    {
                                        From = _message.From,
                                        FromServer = false,
                                    }, _message.From);
                                }
                            }
                            else
                            {
                                foreach (KeyValuePair<string, (T Info, bool IsServer)> item in _playerInfos)
                                {
                                    SendTo(new InfoResponseMessage()
                                    {
                                        IsServer = false,
                                        Source = IPEndPoint.Parse(item.Key),
                                        Details = Utils.Serialize(item.Value.Info),
                                    }, source);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (LocalUserInfo is not null)
                        {
                            SendTo(new InfoResponseMessage()
                            {
                                IsServer = false,
                                Source = null,
                                Details = Utils.Serialize(LocalUserInfo),
                            }, source);
                        }
                    }

                    return;
                }

                default:
                    throw new NotImplementedException();
            }

            // Debug.WriteLine($"[Net]: <= {source} == {message}");

            OnMessageReceived?.Invoke(message, source);
        }
    }

    #endregion

    #region Message Sending

    public void SendImmediate<TMessage>(TMessage data) where TMessage : ISerializable
        => SendImmediate(Utils.Serialize(data));

    public void SendImmediate(byte[] data)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;
        if (isServer)
        {
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
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
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
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

    public void SendImmediateTo<TMessage>(TMessage data, IPEndPoint destination) where TMessage : ISerializable
        => SendImmediateTo(Utils.Serialize(data), destination);

    public void SendImmediateTo(byte[] data, IPEndPoint destination)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;
        if (isServer)
        {
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
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
