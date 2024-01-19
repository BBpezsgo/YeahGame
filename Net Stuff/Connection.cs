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

    public static void Broadcast(Message message, int port)
        => Broadcast(Utils.Serialize(message), port);

    public static void Broadcast(byte[] data, int port)
    {
        using UdpClient udpClient = new();
        udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, port));
    }
}

public class Connection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> where T : ISerializable
{
    #region Types'n Stuff

    class UdpClient<T2> where T2 : ISerializable
    {
        public readonly ConcurrentQueue<byte[]> IncomingQueue;
        public readonly ConcurrentQueue<Message> OutgoingQueue;
        public readonly IPEndPoint EndPoint;

        public double ReceivedAt;
        public double SentAt;

        public T2? Data;

        public UdpClient(IPEndPoint endPoint, T2? data)
        {
            IncomingQueue = new ConcurrentQueue<byte[]>();
            OutgoingQueue = new ConcurrentQueue<Message>();
            EndPoint = endPoint;

            ReceivedAt = Time.NowNoCache;
            SentAt = Time.NowNoCache;

            Data = data;
        }
    }

    #endregion

    #region Constants

    const double Timeout = 10d;
    const double PingInterval = 5d;
    const float DiscoveryInterval = 5f;

    #endregion

    #region Events

    public event Connection.ClientConnectedEventHandler? OnClientConnected;
    public event Connection.ClientDisconnectedEventHandler? OnClientDisconnected;
    public event Connection.ConnectedToServerEventHandler? OnConnectedToServer;
    public event Connection.DisconnectedFromServerEventHandler? OnDisconnectedFromServer;
    public event Connection.MessageReceivedEventHandler? OnMessageReceived;

    #endregion

    public T? LocalUserInfo;

    readonly ConcurrentQueue<UdpMessage> IncomingQueue;
    readonly Queue<Message> OutgoingQueue;

    public IReadOnlyList<IPEndPoint> DiscoveredServers => _discoveredServers;
    readonly List<IPEndPoint> _discoveredServers = new();

    public ICollection<string> Connections => _connections.Keys;
    readonly ConcurrentDictionary<string, UdpClient<T>> _connections;

    public IReadOnlyDictionary<string, (T Info, bool IsServer)> PlayerInfos => _playerInfos;
    readonly Dictionary<string, (T Info, bool IsServer)> _playerInfos;

    public double ReceivedAt;
    public double SentAt;

    float _lastDiscoveryBroadcast;

    UdpClient? UdpSocket;
    Thread? ListeningThread;
    bool isServer;
    bool justListen;

    bool ShouldListen;

    public IPEndPoint? RemoteEndPoint
    {
        get
        {
            try
            { return (!IsConnected || isServer) ? null : (IPEndPoint?)UdpSocket?.Client?.RemoteEndPoint; }
            catch (ObjectDisposedException)
            { return null; }
        }
    }
    public IPEndPoint? LocalEndPoint
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
    public bool IsConnected => UdpSocket != null && !justListen;

    public IReadOnlyList<IPEndPoint> Clients => _connections.Values
        .Select(client => client.EndPoint)
        .ToList();

    public Connection()
    {
        IncomingQueue = new ConcurrentQueue<UdpMessage>();
        OutgoingQueue = new Queue<Message>();

        _connections = new ConcurrentDictionary<string, UdpClient<T>>();

        _playerInfos = new Dictionary<string, (T, bool)>();

        ListeningThread = null;
    }

    #region Connection Handling

    public void Client(IPAddress address, int port)
    {
        UdpSocket = new UdpClient();
        UdpSocket.Connect(address, port);

        justListen = false;
        isServer = false;

        ShouldListen = true;
        ListeningThread = new Thread(Listen) { Name = "UDP Listener" };
        ListeningThread.Start();
        OnConnectedToServer?.Invoke(Connection.ConnectingPhase.Connected);

        ReceivedAt = Time.NowNoCache;
        SentAt = Time.NowNoCache;

        Send(new NetControlMessage(NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY));
    }

    public void JustListen()
    {
        UdpSocket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));

        justListen = true;
        isServer = false;

        ShouldListen = true;
        ListeningThread = new Thread(Listen) { Name = "UDP Listener" };
        ListeningThread.Start();

        ReceivedAt = Time.NowNoCache;
        SentAt = Time.NowNoCache;
    }

    public void Server(IPAddress address, int port)
    {
        UdpSocket = new UdpClient(new IPEndPoint(address, port));

        justListen = false;
        isServer = true;

        ShouldListen = true;
        ListeningThread = new Thread(Listen) { Name = "UDP Listener" };
        ListeningThread.Start();

        ReceivedAt = Time.NowNoCache;
        SentAt = Time.NowNoCache;
    }

    public void DiscoverServers(int port)
    {
        if (Time.Now - _lastDiscoveryBroadcast >= DiscoveryInterval)
        {
            _lastDiscoveryBroadcast = Time.Now;
            Connection.Broadcast(new NetControlMessage()
            {
                Kind = NetControlMessageKind.ARE_U_SERVER,
            }, port);
        }
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

                if (IsServer)
                {
                    if (!_connections.TryGetValue(source.ToString(), out UdpClient<T>? client))
                    {
                        client = new UdpClient<T>(source, default);
                        _connections.TryAdd(source.ToString(), client);
                        OnClientConnected?.Invoke(source, Connection.ConnectingPhase.Connected);
                    }

                    client.ReceivedAt = Time.NowNoCache;
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
        justListen = false;
        ShouldListen = false;
        _connections.Clear();
        UdpSocket?.Close();
        UdpSocket?.Dispose();
        UdpSocket = null;

        if (!isServer)
        { OnDisconnectedFromServer?.Invoke(); }
    }

    void FeedControlMessage(IPEndPoint source, NetControlMessage netControlMessage)
    {
        if (!IsConnected) return;

        switch (netControlMessage.Kind)
        {
            case NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY:
            {
                SendImmediateTo(new NetControlMessage(NetControlMessageKind.HEY_CLIENT_IM_SERVER), source);
                OnClientConnected?.Invoke(source, Connection.ConnectingPhase.Handshake);
                return;
            }
            case NetControlMessageKind.HEY_CLIENT_IM_SERVER:
            {
                OnConnectedToServer?.Invoke(Connection.ConnectingPhase.Handshake);
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

            case NetControlMessageKind.ARE_U_SERVER:
            {
                if (IsServer)
                { SendImmediateTo(new NetControlMessage(NetControlMessageKind.YES_IM_SERVER), source); }
                return;
            }

            case NetControlMessageKind.YES_IM_SERVER:
            {
                for (int i = 0; i < _discoveredServers.Count; i++)
                {
                    if (_discoveredServers[i].Equals(source))
                    { return; }
                }
                _discoveredServers.Add(source);
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
            while (client.Value.OutgoingQueue.TryDequeue(out Message? messageOut) && IsConnected)
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
                Debug.WriteLine($"[Net]: =={RemoteEndPoint}=> PING");
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
                        else if (_message.IsServer && RemoteEndPoint is not null)
                        { _playerInfos[RemoteEndPoint.ToString()] = (Utils.Deserialize<T>(_message.Details), true); }
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

    public void SendImmediate(Message message)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;

        byte[] data = Utils.Serialize(message);
        if (IsServer)
        {
            Debug.WriteLine($"[Net]: == ALL => {message}");
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
            { UdpSocket.Send(data, data.Length, client.Value.EndPoint); }
        }
        else
        {
            Debug.WriteLine($"[Net]: == SERVER => {message}");
            UdpSocket.Send(data, data.Length);
        }
    }

    public void SendImmediateTo(Message message, IPEndPoint destination)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;

        byte[] data = Utils.Serialize(message);
        if (IsServer)
        {
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
            {
                if (!client.Value.EndPoint.Equals(destination)) continue;

                Debug.WriteLine($"[Net]: == {destination} => {message}");
                UdpSocket.Send(data, data.Length, client.Value.EndPoint);
                client.Value.SentAt = Time.NowNoCache;
            }
        }
        else
        {
            if (RemoteEndPoint?.ToString() == destination.ToString())
            {
                Debug.WriteLine($"[Net]: == SERVER => {message}");
                UdpSocket.Send(data, data.Length);
            }
        }
    }

    public void Send(Message message) => OutgoingQueue.Enqueue(message);

    public void SendTo(Message message, IPEndPoint destination)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;

        if (IsServer)
        {
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
            {
                if (!client.Value.EndPoint.Equals(destination)) continue;

                client.Value.OutgoingQueue.Enqueue(message);
            }
        }
        else
        {
            if (RemoteEndPoint?.ToString() == destination.ToString())
            {
                Send(message);
            }
        }
    }

    public void SendExpect(Message message, IPEndPoint expect)
    {
        if (!IsConnected) return;

        SentAt = Time.NowNoCache;

        if (IsServer)
        {
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
            {
                if (client.Value.EndPoint.Equals(expect)) continue;

                client.Value.OutgoingQueue.Enqueue(message);
            }
        }
        else
        {
            if (RemoteEndPoint?.ToString() != expect.ToString())
            {
                Send(message);
            }
        }
    }

    #endregion
}
