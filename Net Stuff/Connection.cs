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

public enum ConnectionState
{
    None,
    Hosting,
    Connecting,
    Connected,
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

    #region Public Properties

    public T? LocalUserInfo;

    public IReadOnlyList<IPEndPoint> DiscoveredServers => _discoveredServers;
    public ICollection<string> Connections => _connections.Keys;
    public IReadOnlyDictionary<string, (T Info, bool IsServer)> PlayerInfos => _playerInfos;

    public IPEndPoint? RemoteEndPoint
    {
        get
        {
            try
            { return (_client is null || _isServer) ? null : (IPEndPoint?)_client.Client.RemoteEndPoint; }
            catch (ObjectDisposedException)
            { return null; }
        }
    }
    public IPEndPoint? LocalEndPoint
    {
        get
        {
            try
            { return (_client is null) ? null : (IPEndPoint?)(_client.Client.LocalEndPoint); }
            catch (ObjectDisposedException)
            { return null; }
        }
    }

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
                if (_shookHandsWithServer)
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
    public bool IsConnected => !_justListen && _client != null && (_isServer || _shookHandsWithServer);

    public UdpClient? Client => _client;

    public IReadOnlyList<IPEndPoint> Clients => _connections.Values
        .Select(client => client.EndPoint)
        .ToList();

    public int SentBytes => _sentBytes;
    public int ReceivedBytes => _receivedBytes;

    #endregion

    #region Fields

    readonly ConcurrentQueue<UdpMessage> _incomingQueue = new();
    readonly Queue<Message> _outgoingQueue = new();

    readonly List<IPEndPoint> _discoveredServers = new();
    float _lastDiscoveryBroadcast;

    readonly ConcurrentDictionary<string, UdpClient<T>> _connections = new();
    readonly Dictionary<string, (T Info, bool IsServer)> _playerInfos = new();

    double _receivedAt;
    double _sentAt;

    UdpClient? _client;
    Thread? _listeningThread;

    bool _isServer;
    bool _justListen;
    bool _shouldListen;
    bool _shookHandsWithServer;

    int _sentBytes;
    int _receivedBytes;

    #endregion

    #region Connection Handling

    public void StartClient(IPAddress address, int port)
    {
        _client = new UdpClient();
        _client.Connect(address, port);

        _justListen = false;
        _isServer = false;
        _shookHandsWithServer = false;

        _shouldListen = true;
        _listeningThread = new Thread(Listen) { Name = "UDP Listener" };
        _listeningThread.Start();
        OnConnectedToServer?.Invoke(Connection.ConnectingPhase.Connected);

        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;

        Send(new NetControlMessage(NetControlMessageKind.HEY_IM_CLIENT_PLS_REPLY));
    }

    public void JustListen()
    {
        _client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));

        _justListen = true;
        _isServer = false;
        _shookHandsWithServer = false;

        _shouldListen = true;
        _listeningThread = new Thread(Listen) { Name = "UDP Listener" };
        _listeningThread.Start();

        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;
    }

    public void StartHost(IPAddress address, int port)
    {
        _client = new UdpClient(new IPEndPoint(address, port));

        _justListen = false;
        _isServer = true;
        _shookHandsWithServer = false;

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
        _shookHandsWithServer = false;

        if (!_isServer)
        { OnDisconnectedFromServer?.Invoke(); }
    }

    void FeedControlMessage(IPEndPoint source, NetControlMessage netControlMessage)
    {
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
                _shookHandsWithServer = true;
                return;
            }
            case NetControlMessageKind.IM_THERE:
            {
                return;
            }
            case NetControlMessageKind.PING:
            {
                Debug.WriteLine($"[Net]: =={source}=> PONG");
                SendImmediateTo(NetControlMessage.SharedPong, source);
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

    public void ResetCounter()
    {
        _sentBytes = 0;
        _receivedBytes = 0;
    }

    readonly List<string> shouldRemove = new();

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

        foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
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
                Debug.WriteLine($"[Net]: =={client.Value.EndPoint}=> PING");
                SendImmediateTo(NetControlMessage.SharedPing, client.Value.EndPoint);
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

        if (!_isServer)
        {
            if (Time.NowNoCache - _receivedAt > PingInterval &&
                Time.NowNoCache - _sentAt > PingInterval)
            {
                Debug.WriteLine($"[Net]: =={RemoteEndPoint}=> PING");
                SendImmediate(NetControlMessage.SharedPing);
            }

            if (Time.NowNoCache - _receivedAt > Timeout && _shouldListen)
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
        if (_client is null) return;

        _sentAt = Time.NowNoCache;

        byte[] data = Utils.Serialize(message);
        if (IsServer)
        {
            Debug.WriteLine($"[Net]: == ALL => {message}");
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
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

        byte[] data = Utils.Serialize(message);
        if (IsServer)
        {
            foreach (KeyValuePair<string, UdpClient<T>> client in _connections)
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
        _sentAt = Time.NowNoCache;

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

    public bool TryGetPlayerInfo(string owner, out (T Info, bool IsServer) info)
    {
        if (_playerInfos.TryGetValue(owner, out info))
        { return true; }

        if (LocalUserInfo != null)
        {
            info = (LocalUserInfo, _isServer);
            return true;
        }

        info = default;
        return false;
    }

    #endregion
}
