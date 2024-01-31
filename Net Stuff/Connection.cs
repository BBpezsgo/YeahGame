/*   === Connection ===
 *  
 *  Client              Server
 *  
 *  1. Establish connection (depends on the underlying protocol)
 *  2.
 *      Handshake Request
 *     ----------------->>
 *     
 *                          - Stores the client's address
 *                          - Sends back the address
 *  3.
 *      Handshake Response
 *     <<-----------------
 *  
 *  - Stores the address
 *  
 *  
 *  If someone idling more than 5 seconds, it sends a PING message to that EndPoint.
 *  If someone idling more than 10 seconds, it considered as disconnected.
 *  
 */

/*   === Reliable Messages ===
 *  
 *  EndPoint A              EndPoint B
 *  
 *  - Saves the message's
 *    id (and the payload)
 *    to a list
 *  
 *  CASE 1
 *  
 *   Any message that inherits
 *    from `ReliableMessage`
 *     ----------------->>
 *  
 *                          - Responses with a `ReliableMessageReceived`
 *                            message containing the incoming message's id
 *  
 *   ReliableMessageReceived
 *     <<-----------------
 *  
 *  - Removes the stored message id
 *    (and the payload) from the list
 *  - Message successfully received by
 *    the remote EndPoint
 *  
 *  
 *  CASE 2
 *  
 *   Any message that inherits
 *    from `ReliableMessage`
 *     ----------------->>
 *  
 *         NETWORK ERROR
 *                          - Didn't received anything
 *  
 *  - Loops through all the
 *    sent messages:
 *    If the message sent
 *    more than 1 second
 *    ago, it sends it again
 *    and updates the message's id
 *  
 */

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public readonly struct RawMessage
{
    public readonly ReadOnlyMemory<byte> Buffer;
    public readonly IPEndPoint Source;

    public RawMessage(ReadOnlyMemory<byte> buffer, IPEndPoint source)
    {
        Buffer = buffer;
        Source = source;
    }
}

public readonly struct SentReliableMessage
{
    public readonly ReliableMessage Message;
    public readonly float SentTime;

    SentReliableMessage(ReliableMessage message, float sentTime)
    {
        Message = message;
        SentTime = sentTime;
    }

    public static SentReliableMessage From(ReliableMessage message) => new(message.Copy(), (float)Time.NowNoCache);
}

public enum ConnectingPhase
{
    Connected,
    Handshake,
}

public enum ConnectionState
{
    None,
    Hosting,
    Connecting,
    Connected,
}

public readonly struct ConnectionUserDetails
{
    public readonly UserDetails? Details;
    public readonly bool IsServer;
    public readonly bool IsRefreshing;

    public ConnectionUserDetails(UserDetails? info, bool isServer, bool isRefreshing)
    {
        Details = info;
        IsServer = isServer;
        IsRefreshing = isRefreshing;
    }
}

public abstract class Connection
{
    public delegate void ClientConnectedEventHandler(IPEndPoint client, ConnectingPhase phase);
    public delegate void ClientDisconnectedEventHandler(IPEndPoint client);
    public delegate void ConnectedToServerEventHandler(ConnectingPhase phase);
    public delegate void DisconnectedFromServerEventHandler();
    public delegate void MessageReceivedEventHandler(Message message, IPEndPoint source);

    protected class ConnectionUserInfoPrivate
    {
        const float MaxAge = 5f;
        const float MinRequestInterval = 1f;

        public readonly UserDetails? Info;
        public readonly bool IsServer;
        public readonly float ReceivedAt;
        public float RequestedAt;

        public bool ShouldRequest => ((float)Time.NowNoCache - ReceivedAt > MaxAge) && ((float)Time.Now - RequestedAt > MinRequestInterval);

        public ConnectionUserInfoPrivate(UserDetails? info, bool isServer, float refreshedAt)
        {
            Info = info;
            IsServer = isServer;
            ReceivedAt = refreshedAt;
        }

        public static explicit operator ConnectionUserDetails(ConnectionUserInfoPrivate v) => new(
            v.Info,
            v.IsServer,
            v.ReceivedAt < v.RequestedAt);

        public static ConnectionUserInfoPrivate GetLoadingInstance(bool isServer) => new(default, isServer, 0f);
    }

    public event ClientConnectedEventHandler? OnClientConnected;
    public event ClientDisconnectedEventHandler? OnClientDisconnected;
    public event ConnectedToServerEventHandler? OnConnectedToServer;
    public event DisconnectedFromServerEventHandler? OnDisconnectedFromServer;
    public event MessageReceivedEventHandler? OnMessageReceived;

    protected void OnClientConnected_Invoke(IPEndPoint client, ConnectingPhase phase) => OnClientConnected?.Invoke(client, phase);
    protected void OnClientDisconnected_Invoke(IPEndPoint client) => OnClientDisconnected?.Invoke(client);
    protected void OnConnectedToServer_Invoke(ConnectingPhase phase) => OnConnectedToServer?.Invoke(phase);
    protected void OnMessageReceived_Invoke(Message message, IPEndPoint source) => OnMessageReceived?.Invoke(message, source);

    #region Constants

    protected const int MaxPayloadSize = 64;
    protected const double Timeout = 10d;
    protected const double PingInterval = 5d;
    protected const float MessageDropProbability = .0f;
    protected const float ReliableRetry = 1f;

    #endregion

    #region Properties

    public UserDetails? LocalUserInfo;

    public abstract ICollection<IPEndPoint> Connections { get; }

    public IReadOnlyDictionary<IPEndPoint, ConnectionUserDetails> UserInfos => _userInfos.Select(v => new KeyValuePair<IPEndPoint, ConnectionUserDetails>(v.Key, (ConnectionUserDetails)v.Value)).ToFrozenDictionary();

    public abstract IPEndPoint? RemoteEndPoint { get; }

    public virtual IPEndPoint? LocalEndPoint => Game.IsOffline ? new IPEndPoint(IPAddress.Any, 0) : null;

    public abstract bool IsServer { get; }

    public abstract ConnectionState State { get; }

    public abstract bool IsConnected { get; }

    public int SentBytes => _sentBytes;
    public int ReceivedBytes => _receivedBytes;
    public int ReceivedPackets => _receivedPackets;
    public int LostPackets => _lostPackets;

    #endregion

    #region Fields

    protected uint _sendingIndex;

    protected int _sentBytes;
    protected int _receivedBytes;
    protected int _receivedPackets;
    protected int _lostPackets;

    protected double _receivedAt;
    protected double _sentAt;

    protected IPEndPoint? _thisIsMe;

    protected readonly List<byte> _jointData = new(MaxPayloadSize);
    protected readonly List<Message> _jointMessages = new();
    protected readonly Dictionary<uint, SentReliableMessage> _sentReliableMessages = new();

    protected readonly List<IPEndPoint> _connectionRemovals = new();

    protected readonly ConcurrentQueue<RawMessage> _incomingQueue = new();
    protected readonly List<Message> _outgoingQueue = new();

    protected readonly Dictionary<IPEndPoint, ConnectionUserInfoPrivate> _userInfos = new();

    #endregion

    public abstract void StartHost(IPEndPoint endPoint);
    public abstract void StartClient(IPEndPoint endPoint);

    public virtual void Close()
    {
        _sendingIndex = 0;

        _sentBytes = 0;
        _receivedBytes = 0;
        _receivedPackets = 0;
        _lostPackets = 0;

        _receivedAt = 0d;
        _sentAt = 0d;

        _thisIsMe = null;

        _jointData.Clear();
        _jointMessages.Clear();
        _sentReliableMessages.Clear();

        _connectionRemovals.Clear();

        _incomingQueue.Clear();
        _outgoingQueue.Clear();

        _userInfos.Clear();

        Debug.WriteLine($"[Net]: Closed");

        if (!IsServer)
        { OnDisconnectedFromServer?.Invoke(); }
    }

    public abstract void Tick();

    public void ResetCounter()
    {
        _sentBytes = 0;
        _receivedBytes = 0;
    }

    #region Message Sending

    public abstract void Send(Message message);
    public abstract void SendTo(Message message, IPEndPoint destination);
    public abstract void SendExpect(Message message, IPEndPoint expect);

    #endregion

    #region User Info

    public bool TryGetUserInfo(string? owner, out ConnectionUserDetails userInfo)
        => TryGetUserInfo(owner is null ? null : IPEndPoint.Parse(owner), out userInfo);

    public bool TryGetUserInfo(IPEndPoint? owner, out ConnectionUserDetails info)
    {
        if (owner is null)
        {
            info = default;
            return false;
        }

        if (_userInfos.TryGetValue(owner, out ConnectionUserInfoPrivate? _info))
        {
            info = (ConnectionUserDetails)_info;
            return true;
        }

        if (owner.Equals(LocalEndPoint) &&
            LocalUserInfo != null)
        {
            info = new ConnectionUserDetails(LocalUserInfo, IsServer, false);
            return true;
        }

        info = default;
        return false;
    }

    #endregion
}

public abstract class Connection<TClientDetails> : Connection
{
    protected class Client
    {
        public readonly ConcurrentQueue<byte[]> IncomingQueue;
        public readonly List<Message> OutgoingQueue;
        public readonly IPEndPoint EndPoint;
        public Dictionary<uint, SentReliableMessage> SentReliableMessages;

        public bool ShookHands;

        public double ReceivedAt;
        public double SentAt;

        public uint ReceivingIndex;
        public uint SendingIndex;

        public TClientDetails Details;

        public Client(IPEndPoint endPoint, TClientDetails details)
        {
            IncomingQueue = new ConcurrentQueue<byte[]>();
            OutgoingQueue = new List<Message>();
            EndPoint = endPoint;
            SentReliableMessages = new Dictionary<uint, SentReliableMessage>();

            ReceivedAt = Time.NowNoCache;
            SentAt = Time.NowNoCache;
            ReceivingIndex = 0;

            Details = details;
        }
    }

    public override ICollection<IPEndPoint> Connections => _connections.Keys;

    protected readonly ConcurrentDictionary<IPEndPoint, Client> _connections = new();

    public override void Close()
    {
        base.Close();

        _connections.Clear();
    }

    protected void TickServer()
    {
        _connectionRemovals.Clear();

        foreach (KeyValuePair<IPEndPoint, Client> client in _connections)
        {
            SendImmediateTo(client.Value.OutgoingQueue, client.Value.EndPoint);
            client.Value.OutgoingQueue.Clear();

            int endlessLoop = 500;

            while (client.Value.IncomingQueue.TryDequeue(out byte[]? messageIn) &&
                   endlessLoop-- > 0)
            { OnReceiveInternal(messageIn, client.Value.EndPoint); }

            if (Time.NowNoCache - client.Value.ReceivedAt >= PingInterval &&
                Time.NowNoCache - client.Value.SentAt >= PingInterval)
            {
                Debug.WriteLine($"[Net]: == {client.Value.EndPoint} => Ping (idling more than {PingInterval} seconds)");
                SendImmediateTo(new NetControlMessage(NetControlMessageKind.PING), client.Value.EndPoint);
            }

            if (Time.NowNoCache - client.Value.ReceivedAt >= Timeout)
            {
                Debug.WriteLine($"[Net]: Removing client {client.Value.EndPoint} for idling more than {Timeout} seconds");
                _connectionRemovals.Add(client.Key);
                continue;
            }

            KeyValuePair<uint, SentReliableMessage>[] sentReliableMessages = client.Value.SentReliableMessages.ToArray();
            foreach (KeyValuePair<uint, SentReliableMessage> sentReliableMessage in sentReliableMessages)
            {
                if (Time.NowNoCache - sentReliableMessage.Value.SentTime >= ReliableRetry)
                {
                    SendImmediateTo(sentReliableMessage.Value.Message, client.Key);
                    client.Value.SentReliableMessages.Remove(sentReliableMessage.Key);
                }
            }
        }

        for (int i = 0; i < _connectionRemovals.Count; i++)
        {
            _userInfos.Remove(_connectionRemovals[i]);

            if (_connections.TryRemove(_connectionRemovals[i], out Client? removedClient))
            { OnClientDisconnected_Invoke(removedClient.EndPoint); }
        }
    }

    protected void TickClient()
    {
        KeyValuePair<uint, SentReliableMessage>[] sentReliableMessages = _sentReliableMessages.ToArray();
        foreach (KeyValuePair<uint, SentReliableMessage> sentReliableMessage in sentReliableMessages)
        {
            if (Time.NowNoCache - sentReliableMessage.Value.SentTime >= ReliableRetry)
            {
                SendImmediate(sentReliableMessage.Value.Message);
                _sentReliableMessages.Remove(sentReliableMessage.Key);
            }
        }

        if (Time.NowNoCache - _receivedAt >= PingInterval &&
            Time.NowNoCache - _sentAt >= PingInterval &&
            IsConnected)
        {
            Debug.WriteLine($"[Net]: == {RemoteEndPoint} => Ping (idling more than {PingInterval} seconds)");
            SendImmediate(new NetControlMessage(NetControlMessageKind.PING));
        }

        if (Time.NowNoCache - _receivedAt >= Timeout &&
            IsConnected)
        {
            Debug.WriteLine($"[Net]: Server idling more than {Timeout} seconds, disconnecting ...");
            Close();
        }
    }

    protected void FeedControlMessage(NetControlMessage netControlMessage, IPEndPoint source)
    {
        switch (netControlMessage.Kind)
        {
            case NetControlMessageKind.PING:
            {
                if (Utils.IsDebug)
                {
                    Debug.WriteLine($"[Net]: <= {source} == Ping");
                    Debug.WriteLine($"[Net]: == {source} => Pong");
                }
                SendImmediateTo(new NetControlMessage(NetControlMessageKind.PONG), source);
                return;
            }
            case NetControlMessageKind.PONG:
            {
                Debug.WriteLine($"[Net]: <= {source} == Pong");
                return;
            }

            default: return;
        }
    }

    protected void OnReceiveInternal(byte[] buffer, IPEndPoint source)
    {
        if (buffer.Length == 0) return;

        try
        {
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
                        OnReceivingInternal(_message, source);
                        FeedControlMessage(_message, source);
                        return;
                    }

                    case MessageType.Bruh:
                    {
                        BruhMessage _message = new(reader);
                        OnReceivingInternal(_message, source);

                        Debug.WriteLine($"[Net]: Client {source} requested to shut down the server :(");

                        Game.Stop();

                        return;
                    }

                    case MessageType.ReliableMessageReceived:
                    {
                        ReliableMessageReceived _message = new(reader);
                        OnReceivingInternal(_message, source);

                        if (Utils.IsDebug)
                        {
                            Debug.WriteLine($"[Net]: <= {source} == ACK {_message.Index}");
                        }

                        if (IsServer)
                        {
                            foreach (KeyValuePair<IPEndPoint, Client> client in _connections)
                            {
                                client.Value.SentReliableMessages.Remove(_message.AckIndex, out SentReliableMessage removed);
                                removed.Message?.Callback?.Invoke();
                            }
                        }
                        else
                        {
                            _sentReliableMessages.Remove(_message.AckIndex, out SentReliableMessage removed);
                            removed.Message.Callback?.Invoke();
                        }

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

                    case MessageType.ChatMessage:
                        message = new ChatMessage(reader);
                        break;

                    case MessageType.InfoResponse:
                    {
                        InfoResponseMessage _message = new(reader);
                        OnReceivingInternal(_message, source);

                        if (Utils.IsDebug)
                        {
                            Debug.WriteLine($"[Net]: <= {source} == {_message}");
                        }

                        IPEndPoint? infoSource = _message.Source;

                        if (IsServer)
                        { infoSource ??= source; }

                        if (_message.IsServer)
                        { infoSource ??= RemoteEndPoint; }

                        if (infoSource is null)
                        {
                            Debug.WriteLine($"[Net]: User info (sent by {source}) source is null");
                        }
                        else
                        {
                            UserDetails data = Utils.Deserialize<UserDetails>(_message.Details);
                            if (!IsServer)
                            {
                                if (infoSource.Equals(LocalEndPoint))
                                { LocalUserInfo = data; }
                            }
                            _userInfos[infoSource] = new ConnectionUserInfoPrivate(data, _message.IsServer, (float)Time.NowNoCache);
                        }

                        if (IsServer)
                        {
                            Send(new InfoResponseMessage()
                            {
                                ShouldAck = false,

                                IsServer = false,
                                Source = _message.Source ?? source,
                                Details = _message.Details,
                            });
                        }

                        return;
                    }

                    case MessageType.InfoRequest:
                    {
                        InfoRequestMessage _message = new(reader);
                        OnReceivingInternal(_message, source);

                        Debug.WriteLine($"[Net]: <= {source} == {_message}");

                        if (IsServer)
                        {
                            if (_message.FromServer ||
                                (_message.From?.Equals(LocalEndPoint) ?? false))
                            {
                                if (LocalUserInfo is null)
                                { return; }

                                SendTo(new InfoResponseMessage()
                                {
                                    IsServer = true,
                                    Source = null,
                                    Details = Utils.Serialize(LocalUserInfo),
                                }, source);
                                return;
                            }

                            if (_message.From is not null)
                            {
                                if (_userInfos.TryGetValue(_message.From, out ConnectionUserInfoPrivate? info))
                                {
                                    if (info.Info is null)
                                    { return; }

                                    SendTo(new InfoResponseMessage()
                                    {
                                        IsServer = false,
                                        Source = _message.From,
                                        Details = Utils.Serialize(info.Info),
                                    }, source);
                                }
                                else
                                {
                                    _userInfos.Add(_message.From, ConnectionUserInfoPrivate.GetLoadingInstance(false));
                                    SendTo(new InfoRequestMessage()
                                    {
                                        From = _message.From,
                                        FromServer = false,
                                    }, _message.From);
                                }

                                return;
                            }
                        }

                        SendAllUserDetailsTo(source, false);

                        return;
                    }

                    case MessageType.HandshakeRequest:
                    {
                        HandshakeRequestMessage _message = new(reader);
                        OnReceivingInternal(_message, source);

                        if (!IsServer)
                        {
                            Debug.WriteLine($"[Net]: Received a handshake request from {source} but I'm a client");
                            return;
                        }

                        if (Utils.IsDebug)
                        {
                            Debug.WriteLine($"[Net]: <= {source} == Handshake Request");
                        }

                        SendImmediateTo(new HandshakeResponseMessage()
                        {
                            ThisIsYou = source,
                        }, source);

                        if (_connections.TryGetValue(source, out Client? client) &&
                            !client.ShookHands)
                        {
                            client.ShookHands = true;
                            Debug.WriteLine($"[Net]: Shook hands with client {source}");
                            OnClientConnected_Invoke(source, ConnectingPhase.Handshake);

                            SendAllUserDetailsTo(source, false);
                        }

                        return;
                    }
                    case MessageType.HandshakeResponse:
                    {
                        HandshakeResponseMessage _message = new(reader);
                        OnReceivingInternal(_message, source);

                        if (IsServer)
                        {
                            Debug.WriteLine($"[Net]: Server received a {MessageType.HandshakeResponse} message from {source} (wtf?)");
                            return;
                        }

                        if (Utils.IsDebug)
                        {
                            Debug.WriteLine($"[Net]: <= {source} == Handshake Response (This is me: {_message.ThisIsYou})");
                        }

                        Debug.WriteLine($"[Net]: Connected to {source} as {_message.ThisIsYou}");

                        OnConnectedToServer_Invoke(ConnectingPhase.Handshake);
                        _thisIsMe = _message.ThisIsYou;

                        if (LocalUserInfo is not null)
                        {
                            SendImmediate(new InfoResponseMessage()
                            {
                                ShouldAck = true,

                                Details = Utils.Serialize(LocalUserInfo),
                                IsServer = false,
                                Source = _thisIsMe,
                            });
                        }

                        return;
                    }

                    default: throw new NotImplementedException();
                }

                if (Utils.IsDebug)
                {
                    // Debug.WriteLine($"[Net]: <= {source} == {message}");
                }

                OnReceivingInternal(message, source);

                if (!IsServer && _thisIsMe is null)
                { SendImmediate(new HandshakeRequestMessage()); }

                OnMessageReceived_Invoke(message, source);
            }
        }
        catch (EndOfStreamException)
        {

        }
    }

    void OnReceivingInternal(Message message, IPEndPoint source)
    {
        _receivedPackets++;

        if (message is ReliableMessage reliableMessage &&
            reliableMessage.ShouldAck)
        {
            if (IsServer)
            {
                SendImmediateTo(new ReliableMessageReceived()
                { AckIndex = message.Index }, source);
            }
            else
            {
                SendImmediate(new ReliableMessageReceived()
                { AckIndex = message.Index });
            }
        }

        if (!_connections.TryGetValue(source, out Client? client))
        { return; }

        client.ReceivingIndex++;

        if (client.ReceivingIndex != message.Index)
        {
            if (Utils.IsDebug)
            {
                Debug.WriteLine($"[Net]: Lost packet (expected {client.ReceivingIndex} got {message.Index})");
            }
            _lostPackets++;
            client.ReceivingIndex = message.Index;
        }
    }

    #region Message Sending

    public override void Send(Message message) => _outgoingQueue.Add(message);

    public override void SendTo(Message message, IPEndPoint destination)
    {
        if (IsServer)
        {
            if (_connections.TryGetValue(destination, out Client? client))
            { client.OutgoingQueue.Add(message); }
        }
        else
        {
            if (destination.Equals(RemoteEndPoint))
            { _outgoingQueue.Add(message); }
        }
    }

    public override void SendExpect(Message message, IPEndPoint expect)
    {
        if (IsServer)
        {
            foreach (KeyValuePair<IPEndPoint, Client> client in _connections)
            {
                if (client.Value.EndPoint.Equals(expect)) continue;
                client.Value.OutgoingQueue.Add(message);
            }
        }
        else
        {
            if (!expect.Equals(RemoteEndPoint))
            { _outgoingQueue.Add(message); }
        }
    }

    #endregion

    #region Immediate Message Sending

    protected void SendImmediate(IEnumerable<Message> messages)
    {
        if (IsServer)
        {
            foreach (IPEndPoint client in _connections.Keys)
            { SendImmediateTo(messages, client); }
        }
        else
        {
            _jointData.Clear();
            _jointMessages.Clear();

            foreach (Message message in messages)
            {
                message.Index = _sendingIndex++;
                if (message is ReliableMessage reliableMessage &&
                    reliableMessage.ShouldAck)
                {
                    if (Utils.IsDebug)
                    {
                        Debug.WriteLine($"[Net]: == SERVER => Waiting ACK for {message.Index} ...");
                    }
                    _sentReliableMessages[message.Index] = SentReliableMessage.From(reliableMessage);
                }
                byte[] data = Utils.Serialize(message);

                if (_jointData.Count + data.Length > MaxPayloadSize)
                {
                    SendImmediate(_jointData.ToArray(), _jointMessages);
                    _jointData.Clear();
                }

                if (data.Length >= MaxPayloadSize)
                {
                    SendImmediate(data, [message]);
                }
                else
                {
                    _jointData.AddRange(data);
                    _jointMessages.Add(message);
                }
            }

            if (_jointData.Count > 0)
            { SendImmediate(_jointData.ToArray(), _jointMessages); }

            _jointData.Clear();
            _jointMessages.Clear();
        }
    }

    protected abstract void SendImmediate(Message message);
    protected abstract void SendImmediate(byte[] data, IEnumerable<Message> messages);
    protected void SendImmediateTo(Message message, IPEndPoint destination)
    {
        if (IsServer)
        {
            if (_connections.TryGetValue(destination, out Client? destinationClient))
            {
                message.Index = destinationClient.SendingIndex++;
                if (message is ReliableMessage reliableMessage &&
                    reliableMessage.ShouldAck)
                {
                    if (Utils.IsDebug)
                    {
                        Debug.WriteLine($"[Net]: == {destination} => Waiting ACK for {message.Index} ...");
                    }
                    destinationClient.SentReliableMessages[message.Index] = SentReliableMessage.From(reliableMessage);
                }
            }
        }
        else
        {
            message.Index = _sendingIndex++;
            if (message is ReliableMessage reliableMessage &&
                reliableMessage.ShouldAck)
            {
                if (Utils.IsDebug)
                {
                    Debug.WriteLine($"[Net]: == SERVER => Waiting ACK for {message.Index} ...");
                }
                _sentReliableMessages[message.Index] = SentReliableMessage.From(reliableMessage);
            }
        }

        byte[] data = Utils.Serialize(message);
        SendImmediateTo(data, destination, [message]);
    }
    protected abstract void SendImmediateTo(byte[] data, IPEndPoint destination, IEnumerable<Message> messages);
    protected void SendImmediateTo(IEnumerable<Message> messages, IPEndPoint destination)
    {
        _jointData.Clear();
        _jointMessages.Clear();
        _connections.TryGetValue(destination, out Client? destinationClient);

        foreach (Message message in messages)
        {
            if (IsServer)
            {
                if (destinationClient is not null)
                {
                    message.Index = destinationClient.SendingIndex++;
                    if (message is ReliableMessage reliableMessage &&
                        reliableMessage.ShouldAck)
                    {
                        if (Utils.IsDebug)
                        {
                            Debug.WriteLine($"[Net]: == {destination} => Waiting ACK for {message.Index} ...");
                        }
                        destinationClient.SentReliableMessages[message.Index] = SentReliableMessage.From(reliableMessage);
                    }
                }
            }
            else
            {
                message.Index = _sendingIndex++;
                if (message is ReliableMessage reliableMessage &&
                    reliableMessage.ShouldAck)
                {
                    if (Utils.IsDebug)
                    {
                        Debug.WriteLine($"[Net]: == SERVER => Waiting ACK for {message.Index} ...");
                    }
                    _sentReliableMessages[message.Index] = SentReliableMessage.From(reliableMessage);
                }
            }

            byte[] data = Utils.Serialize(message);

            if (_jointData.Count + data.Length > MaxPayloadSize)
            {
                SendImmediateTo(_jointData.ToArray(), destination, _jointMessages);
                _jointData.Clear();
            }

            if (data.Length >= MaxPayloadSize)
            {
                SendImmediateTo(data, destination, [message]);
            }
            else
            {
                _jointData.AddRange(data);
                _jointMessages.Add(message);
            }
        }

        if (_jointData.Count > 0)
        { SendImmediateTo(_jointData.ToArray(), destination, _jointMessages); }

        _jointData.Clear();
        _jointMessages.Clear();
    }

    #endregion

    #region User Info

    protected void TryRefreshUserInfos()
    {
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
    }

    protected void SendAllUserDetailsTo(IPEndPoint destination, bool shouldAck)
    {
        if (IsServer)
        {
            foreach ((IPEndPoint key, ConnectionUserInfoPrivate value) in _userInfos)
            {
                if (value.Info is null)
                { continue; }

                SendTo(new InfoResponseMessage()
                {
                    ShouldAck = shouldAck,

                    IsServer = false,
                    Source = key,
                    Details = Utils.Serialize(value.Info),
                }, destination);
            }

#if !SERVER
            if (LocalUserInfo is not null)
            {
                SendTo(new InfoResponseMessage()
                {
                    ShouldAck = shouldAck,

                    IsServer = true,
                    Source = null,
                    Details = Utils.Serialize(LocalUserInfo),
                }, destination);
            }
#endif
        }
        else
        {
            if (LocalUserInfo is null)
            { return; }

            SendTo(new InfoResponseMessage()
            {
                ShouldAck = shouldAck,

                IsServer = false,
                Source = null,
                Details = Utils.Serialize(LocalUserInfo),
            }, destination);
        }
    }

    #endregion
}
