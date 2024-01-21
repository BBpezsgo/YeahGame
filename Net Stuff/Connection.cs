using System.Collections.Concurrent;
using System.Collections.Frozen;
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

public enum ConnectionState
{
    None,
    Hosting,
    Connecting,
    Connected,
}

public readonly struct ConnectionUserInfo<TUserInfo>
{
    public readonly TUserInfo? Info;
    public readonly bool IsServer;
    public readonly bool IsRefreshing;

    public ConnectionUserInfo(TUserInfo? info, bool isServer, bool isRefreshing)
    {
        Info = info;
        IsServer = isServer;
        IsRefreshing = isRefreshing;
    }
}

public class Connection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TUserInfo> where TUserInfo : ISerializable
{
    class ConnectionClient
    {
        public readonly ConcurrentQueue<byte[]> IncomingQueue;
        public readonly ConcurrentQueue<Message> OutgoingQueue;
        public readonly IPEndPoint EndPoint;

        public bool ShookHands;

        public double ReceivedAt;
        public double SentAt;

        public ConnectionClient(IPEndPoint endPoint)
        {
            IncomingQueue = new ConcurrentQueue<byte[]>();
            OutgoingQueue = new ConcurrentQueue<Message>();
            EndPoint = endPoint;

            ReceivedAt = Time.NowNoCache;
            SentAt = Time.NowNoCache;
        }
    }

    class ConnectionUserInfoPrivate
    {
        const float MaxAge = 5f;
        const float MinRequestInterval = 1f;

        public TUserInfo? Info;
        public bool IsServer;
        public float ReceivedAt;
        public float RequestedAt;

        public bool ShouldRequest => ((float)Time.NowNoCache - ReceivedAt > MaxAge) && ((float)Time.Now - RequestedAt > MinRequestInterval);

        public ConnectionUserInfoPrivate(TUserInfo? info, bool isServer, float refreshedAt)
        {
            Info = info;
            IsServer = isServer;
            ReceivedAt = refreshedAt;
        }

        public static explicit operator ConnectionUserInfo<TUserInfo>(ConnectionUserInfoPrivate v) => new(
            v.Info,
            v.IsServer,
            v.ReceivedAt < v.RequestedAt);
    }

    #region Constants

    const double Timeout = 10d;
    const double PingInterval = 5d;
    const float DiscoveryInterval = 5f;
    const float MessageDropProbability = .0f;

    #endregion

    #region Events

    public event Connection.ClientConnectedEventHandler? OnClientConnected;
    public event Connection.ClientDisconnectedEventHandler? OnClientDisconnected;
    public event Connection.ConnectedToServerEventHandler? OnConnectedToServer;
    public event Connection.DisconnectedFromServerEventHandler? OnDisconnectedFromServer;
    public event Connection.MessageReceivedEventHandler? OnMessageReceived;

    #endregion

    #region Public Properties

    public TUserInfo? LocalUserInfo;

    public int LostPackets => _lostPackets;

    public IReadOnlyList<IPEndPoint> DiscoveredServers => _discoveredServers;
    public IReadOnlySet<string> Connections => _connections.Keys.Select(v => v.ToString()).ToFrozenSet();
    public IReadOnlyDictionary<string, ConnectionUserInfo<TUserInfo>> UserInfos => _userInfos.Select(v => new KeyValuePair<string, ConnectionUserInfo<TUserInfo>>(v.Key.ToString(), (ConnectionUserInfo<TUserInfo>)v.Value)).ToFrozenDictionary();

    public IPEndPoint? RemoteEndPoint
    {
        get
        {
            if (_client is null || _isServer) return null;

            try
            { return (IPEndPoint?)_client.Client.RemoteEndPoint; }
            catch (ObjectDisposedException)
            { return null; }
        }
    }

    public IPEndPoint? LocalEndPoint
    {
        get
        {
            if (_client is null) return null;

            if (_isServer)
            {
                try
                { return (IPEndPoint?)_client.Client.LocalEndPoint; }
                catch (ObjectDisposedException)
                { return null; }
            }
            else
            {
                return _thisIsMe;
            }
        }
    }

    /*
    public IPEndPoint? LocalEndPointExternal
    {
        get
        {
            IPEndPoint? localEndPoint = LocalEndPoint;
            if (localEndPoint == null) return null;
            IPAddress? externalAddress = Ipify.ExternalAddress;
            if (externalAddress == null) return null;
            return new IPEndPoint(externalAddress, localEndPoint.Port);
        }
    }
    */

    public bool IsServer => _isServer;

    public ConnectionState State
    {
        get
        {
            if (_client is null) return ConnectionState.None;
            if (!_client.Client.IsBound) return ConnectionState.None;
            if (_justListen) return ConnectionState.None;
            if (_isServer)
            {
                return ConnectionState.Hosting;
            }
            else
            {
                if (_thisIsMe is not null)
                {
                    return ConnectionState.Connected;
                }
                else
                {
                    return ConnectionState.Connecting;
                }
            }
        }
    }

    [MemberNotNullWhen(true, nameof(_client))]
    [MemberNotNullWhen(true, nameof(Client))]
    public bool IsConnected => !_justListen && _client != null && (_isServer || _thisIsMe is not null);

    public UdpClient? Client => _client;

    public IReadOnlyList<IPEndPoint> Clients => _connections.Values
        .Select(client => client.EndPoint)
        .ToList();

    public int SentBytes => _sentBytes;
    public int ReceivedBytes => _receivedBytes;
    public int ReceivedPackets => _receivedPackets;

    #endregion

    #region Fields

    readonly Dictionary<IPEndPoint, uint> _receivingIndex = new();
    uint _sendingIndex;
    int _lostPackets;

    readonly ConcurrentQueue<UdpMessage> _incomingQueue = new();
    readonly Queue<Message> _outgoingQueue = new();

    readonly List<IPEndPoint> _discoveredServers = new();
    float _lastDiscoveryBroadcast;

    readonly ConcurrentDictionary<IPEndPoint, ConnectionClient> _connections = new();
    readonly Dictionary<IPEndPoint, ConnectionUserInfoPrivate> _userInfos = new();

    double _receivedAt;
    double _sentAt;

    UdpClient? _client;
    Thread? _listeningThread;

    bool _isServer;
    bool _justListen;
    bool _shouldListen;
    IPEndPoint? _thisIsMe;

    int _sentBytes;
    int _receivedBytes;
    int _receivedPackets;

    #endregion

    #region Connection Handling

    public void StartClient(IPEndPoint endPoint)
    {
        _client = new UdpClient();
        _client.AllowNatTraversal(true);

        Debug.WriteLine($"[Net]: Connecting to {endPoint} ...");
        _client.Connect(endPoint);

        _justListen = false;
        _isServer = false;
        _thisIsMe = null;
        _lostPackets = 0;

        _shouldListen = true;
        _listeningThread = new Thread(Listen) { Name = "UDP Listener" };
        _listeningThread.Start();
        OnConnectedToServer?.Invoke(Connection.ConnectingPhase.Connected);

        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;

        Debug.WriteLine($"[Net]: Shaking hands with {endPoint} ...");
        Send(new HandshakeRequestMessage());
    }

    public void JustListen()
    {
        _client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        _client.AllowNatTraversal(true);

        _justListen = true;
        _isServer = false;
        _thisIsMe = null;
        _lostPackets = 0;

        _shouldListen = true;
        _listeningThread = new Thread(Listen) { Name = "UDP Listener" };
        _listeningThread.Start();

        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;
    }

    public void StartHost(IPEndPoint endPoint)
    {
        _client = new UdpClient(endPoint);
        _client.AllowNatTraversal(true);

        _justListen = false;
        _isServer = true;
        _thisIsMe = null;
        _lostPackets = 0;

        _shouldListen = true;
        _listeningThread = new Thread(Listen) { Name = "UDP Listener" };
        _listeningThread.Start();

        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;
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
        if (_client is null) return;

        Debug.WriteLine($"[Net]: Listening on {_client.Client.LocalEndPoint}");

        while (_shouldListen)
        {
            try
            {
                IPEndPoint source = new(IPAddress.Any, 0);
                byte[] buffer = _client.Receive(ref source);

                if (!_shouldListen) break;

                _receivedAt = Time.NowNoCache;
                _receivedBytes += buffer.Length;

                if (IsServer)
                {
                    if (!_connections.TryGetValue(source, out ConnectionClient? client))
                    {
                        client = new ConnectionClient(source);
                        _connections.TryAdd(source, client);
                        Debug.WriteLine($"[Net]: Client {source} sending the first message ...");
                        OnClientConnected?.Invoke(source, Connection.ConnectingPhase.Connected);
                    }

                    client.ReceivedAt = Time.NowNoCache;
                    client.IncomingQueue.Enqueue(buffer);
                }
                else
                {
                    _incomingQueue.Enqueue(new UdpMessage(buffer, source));
                }
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"[Net]: Error ({ex.ErrorCode}) ({ex.NativeErrorCode}) ({ex.SocketErrorCode}): {ex.Message}");

                if (!_isServer)
                {
                    Close();
                    break;
                }
            }
        }
    }

    public void Close()
    {
        _justListen = false;
        _shouldListen = false;
        _connections.Clear();
        _client?.Close();
        _client?.Dispose();
        _client = null;
        _thisIsMe = null;
        _lostPackets = 0;

        Debug.WriteLine($"[Net]: Closed");

        if (!_isServer)
        { OnDisconnectedFromServer?.Invoke(); }
    }

    void FeedControlMessage(IPEndPoint source, NetControlMessage netControlMessage)
    {
        switch (netControlMessage.Kind)
        {
            case NetControlMessageKind.IM_THERE:
            {
                return;
            }
            case NetControlMessageKind.PING:
            {
                Debug.WriteLine($"[Net]: <= {source} == Ping");
                Debug.WriteLine($"[Net]: == {source} => Pong");
                SendImmediateTo(new NetControlMessage(NetControlMessageKind.PONG), source);
                return;
            }
            case NetControlMessageKind.PONG:
            {
                Debug.WriteLine($"[Net]: <= {source} == Pong");
                return;
            }

            case NetControlMessageKind.ARE_U_SERVER:
            {
                Debug.WriteLine($"[Net]: <= {source} == Are you a server?");

                if (IsServer)
                { SendImmediateTo(new NetControlMessage(NetControlMessageKind.YES_IM_SERVER), source); }
                return;
            }

            case NetControlMessageKind.YES_IM_SERVER:
            {
                Debug.WriteLine($"[Net]: <= {source} == I'm a server");

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

    public void ResetCounter()
    {
        _sentBytes = 0;
        _receivedBytes = 0;
    }

    readonly List<IPEndPoint> shouldRemove = new();

    public void Tick()
    {
        int endlessLoop = 500;

        while (_incomingQueue.TryDequeue(out UdpMessage messageIn))
        {
            OnReceiveInternal(messageIn.Source, messageIn.Buffer);

            if (endlessLoop-- < 0)
            { break; }
        }

        while (_outgoingQueue.TryDequeue(out Message? messageOut))
        { SendImmediate(messageOut); }

        shouldRemove.Clear();

        foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
        {
            while (client.Value.OutgoingQueue.TryDequeue(out Message? messageOut))
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
                Debug.WriteLine($"[Net]: == {client.Value.EndPoint} => Ping (idling more than {PingInterval} seconds)");
                SendImmediateTo(new NetControlMessage(NetControlMessageKind.PING), client.Value.EndPoint);
            }

            if (Time.NowNoCache - client.Value.ReceivedAt > Timeout)
            {
                Debug.WriteLine($"[Net]: Removing client {client.Value.EndPoint} for idling more than {Timeout} seconds");
                shouldRemove.Add(client.Key);
                continue;
            }
        }

        for (int i = 0; i < shouldRemove.Count; i++)
        {
            _userInfos.Remove(shouldRemove[i]);

            if (_connections.TryRemove(shouldRemove[i], out ConnectionClient? removedClient))
            { OnClientDisconnected?.Invoke(removedClient.EndPoint); }
        }

        foreach (KeyValuePair<IPEndPoint, ConnectionUserInfoPrivate> userInfo in _userInfos)
        {
            if (userInfo.Value.ShouldRequest)
            {
                SendImmediateTo(new InfoRequestMessage()
                {
                    From = userInfo.Key,
                    FromServer = userInfo.Key.Equals(RemoteEndPoint),
                }, userInfo.Key);
                userInfo.Value.RequestedAt = (float)Time.NowNoCache;
            }
        }

        if (!_isServer && _client is not null)
        {
            if (Time.NowNoCache - _receivedAt > PingInterval &&
                Time.NowNoCache - _sentAt > PingInterval)
            {
                Debug.WriteLine($"[Net]: == {RemoteEndPoint} => Ping (idling more than {PingInterval} seconds)");
                SendImmediate(new NetControlMessage(NetControlMessageKind.PING));
            }

            if (Time.NowNoCache - _receivedAt > Timeout && _shouldListen)
            {
                Debug.WriteLine($"[Net]: Server idling more than {Timeout} seconds, disconnecting ...");
                Close();
            }
        }
    }

    void OnReceiveInternal(IPEndPoint source, byte[] buffer)
    {
        if (buffer.Length == 0) return;

        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);

        void HandleIndexing(Message message)
        {
            if (!_receivingIndex.TryGetValue(source, out uint value))
            {
                value = 0;
                _receivingIndex.Add(source, value);
            }
            else
            {
                _receivingIndex[source] = ++value;
            }

            if (value != message.Index)
            {
                Debug.WriteLine($"[Net]: Lost packet (expected {value} got {message.Index})");
                _lostPackets++;
                _receivingIndex[source] = message.Index;
            }
        }

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            MessageType messageType = (MessageType)buffer[reader.BaseStream.Position];
            Message message;

            switch (messageType)
            {
                case MessageType.Control:
                {
                    NetControlMessage _message = new(reader);
                    HandleIndexing(_message);
                    _receivedPackets++;
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
                    HandleIndexing(_message);
                    _receivedPackets++;

                    Debug.WriteLine($"[Net]: <= {source} == {_message}");

                    if (IsServer)
                    {
                        if (_message.Source is not null)
                        { _userInfos[_message.Source] = new ConnectionUserInfoPrivate(Utils.Deserialize<TUserInfo>(_message.Details), false, (float)Time.NowNoCache); }
                        else
                        { _userInfos[source] = new ConnectionUserInfoPrivate(Utils.Deserialize<TUserInfo>(_message.Details), false, (float)Time.NowNoCache); }

                        Send(_message);
                    }
                    else
                    {
                        if (_message.Source is not null)
                        { _userInfos[_message.Source] = new ConnectionUserInfoPrivate(Utils.Deserialize<TUserInfo>(_message.Details), false, (float)Time.NowNoCache); }
                        else if (_message.IsServer && RemoteEndPoint is not null)
                        { _userInfos[RemoteEndPoint] = new ConnectionUserInfoPrivate(Utils.Deserialize<TUserInfo>(_message.Details), true, (float)Time.NowNoCache); }
                    }

                    return;
                }

                case MessageType.InfoRequest:
                {
                    InfoRequestMessage _message = new(reader);
                    HandleIndexing(_message);
                    _receivedPackets++;

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
                                if (_userInfos.TryGetValue(_message.From, out ConnectionUserInfoPrivate? info))
                                {
                                    if (info.Info != null)
                                    {
                                        SendTo(new InfoResponseMessage()
                                        {
                                            IsServer = false,
                                            Source = _message.From,
                                            Details = Utils.Serialize(info.Info),
                                        }, source);
                                    }
                                }
                                else
                                {
                                    _userInfos.Add(_message.From, new ConnectionUserInfoPrivate(default, false, 0f));
                                    SendTo(new InfoRequestMessage()
                                    {
                                        From = _message.From,
                                        FromServer = false,
                                    }, _message.From);
                                }
                            }
                            else
                            {
                                foreach (KeyValuePair<IPEndPoint, ConnectionUserInfoPrivate> item in _userInfos)
                                {
                                    if (item.Value.Info != null)
                                    {
                                        SendTo(new InfoResponseMessage()
                                        {
                                            IsServer = false,
                                            Source = item.Key,
                                            Details = Utils.Serialize(item.Value.Info),
                                        }, source);
                                    }
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

                case MessageType.HandshakeRequest:
                {
                    HandshakeRequestMessage _message = new(reader);

                    Debug.WriteLine($"[Net]: <= {source} == Handshake Request");

                    SendImmediateTo(new HandshakeResponseMessage()
                    {
                        ThisIsYou = source,
                    }, source);

                    if (_connections.TryGetValue(source, out ConnectionClient? client) &&
                        !client.ShookHands)
                    {
                        client.ShookHands = true;
                        Debug.WriteLine($"[Net]: Shook hands with client {source}");
                        OnClientConnected?.Invoke(source, Connection.ConnectingPhase.Handshake);
                    }

                    return;
                }
                case MessageType.HandshakeResponse:
                {
                    HandshakeResponseMessage _message = new(reader);

                    Debug.WriteLine($"[Net]: <= {source} == Handshake Response (This is me: {_message.ThisIsYou})");

                    Debug.WriteLine($"[Net]: Connected to {source} as {_message.ThisIsYou}");

                    OnConnectedToServer?.Invoke(Connection.ConnectingPhase.Handshake);
                    _thisIsMe = _message.ThisIsYou;

                    return;
                }

                default: throw new NotImplementedException();
            }

            // Debug.WriteLine($"[Net]: <= {source} == {message}");

            HandleIndexing(message);
            _receivedPackets++;

            if (!_isServer && _thisIsMe is null)
            { SendImmediate(new HandshakeRequestMessage()); }

            OnMessageReceived?.Invoke(message, source);
        }
    }

    #endregion

    #region Message Sending

    public void SendImmediate(Message message)
    {
        if (_client is null) return;

        _sentAt = Time.NowNoCache;

        message.Index = _sendingIndex++;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        byte[] data = Utils.Serialize(message);
        if (IsServer)
        {
            Debug.WriteLine($"[Net]: == ALL => {message}");
            foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
            {
                _client.Send(data, data.Length, client.Value.EndPoint);
                _sentBytes += data.Length;
            }
        }
        else
        {
            Debug.WriteLine($"[Net]: == SERVER => {message}");
            _client.Send(data, data.Length);
            _sentBytes += data.Length;
        }
    }

    public void SendImmediateTo(Message message, IPEndPoint destination)
    {
        if (_client is null) return;

        _sentAt = Time.NowNoCache;

        message.Index = _sendingIndex++;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        byte[] data = Utils.Serialize(message);
        if (IsServer)
        {
            foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
            {
                if (!client.Value.EndPoint.Equals(destination)) continue;

                Debug.WriteLine($"[Net]: == {destination} => {message}");
                _client.Send(data, data.Length, client.Value.EndPoint);
                _sentBytes += data.Length;
                client.Value.SentAt = Time.NowNoCache;
            }
        }
        else
        {
            if (RemoteEndPoint?.ToString() == destination.ToString())
            {
                Debug.WriteLine($"[Net]: == SERVER => {message}");
                _client.Send(data, data.Length);
                _sentBytes += data.Length;
            }
        }
    }

    public void Send(Message message) => _outgoingQueue.Enqueue(message);

    public void SendTo(Message message, IPEndPoint destination)
    {
        _sentAt = Time.NowNoCache;

        if (IsServer)
        {
            foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
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
        _sentAt = Time.NowNoCache;

        if (IsServer)
        {
            foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
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

    public bool TryGetPlayerInfo(string owner, out ConnectionUserInfo<TUserInfo> info)
        => TryGetPlayerInfo(IPEndPoint.Parse(owner), out info);
    public bool TryGetPlayerInfo(IPEndPoint owner, out ConnectionUserInfo<TUserInfo> info)
    {
        if (_userInfos.TryGetValue(owner, out ConnectionUserInfoPrivate? _info))
        {
            info = (ConnectionUserInfo<TUserInfo>)_info;
            return true;
        }

        if (LocalUserInfo != null)
        {
            info = new ConnectionUserInfo<TUserInfo>(LocalUserInfo, _isServer, false);
            return true;
        }

        info = default;
        return false;
    }

    #endregion
}
