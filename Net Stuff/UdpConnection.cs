using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using YeahGame.Messages;

using RawMessage = (System.ReadOnlyMemory<byte> Buffer, System.Net.IPEndPoint Source);

namespace YeahGame;

public class UdpConnection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TUserInfo> : ConnectionBase<TUserInfo, object?> where TUserInfo : ISerializable
{
    #region Public Properties

    public override IPEndPoint? RemoteEndPoint
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

    public override IPEndPoint? LocalEndPoint
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

    public override bool IsServer => _isServer;

    public override ConnectionState State
    {
        get
        {
            if (_client is null) return ConnectionState.None;
            if (!_client.Client.IsBound) return ConnectionState.None;
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
    public override bool IsConnected => _client != null && (_isServer || _thisIsMe is not null);

    #endregion

    #region Fields

    UdpClient? _client;
    Thread? _listeningThread;

    bool _isServer;
    bool _shouldListen;

    #endregion

    #region Connection Handling

    public override void StartClient(IPEndPoint endPoint)
    {
        _client = new UdpClient
        {
            DontFragment = true,
        };

        Debug.WriteLine($"[Net]: Connecting to {endPoint} ...");
        _client.Connect(endPoint);

        _isServer = false;
        _thisIsMe = null;
        _lostPackets = 0;

        _shouldListen = true;
        _listeningThread = new Thread(Listen) { Name = "UDP Listener" };
        _listeningThread.Start();
        OnConnectedToServer_Invoke(ConnectingPhase.Connected);

        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;

        Debug.WriteLine($"[Net]: Shaking hands with {endPoint} ...");
        Send(new HandshakeRequestMessage());
    }

    public override void StartHost(IPEndPoint endPoint)
    {
        _client = new UdpClient(endPoint)
        {
            DontFragment = true,
        };

        _isServer = true;
        _thisIsMe = null;
        _lostPackets = 0;

        _shouldListen = true;
        _listeningThread = new Thread(Listen) { Name = "UDP Listener" };
        _listeningThread.Start();

        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;
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
                        client = new ConnectionClient(source, null);
                        _connections.TryAdd(source, client);
                        Debug.WriteLine($"[Net]: Client {source} sending the first message ...");
                        OnClientConnected_Invoke(source, ConnectingPhase.Connected);
                    }

                    client.ReceivedAt = Time.NowNoCache;
                    client.IncomingQueue.Enqueue(buffer);
                }
                else
                {
                    _incomingQueue.Enqueue(new RawMessage(buffer, source));
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

    public override void Close()
    {
        base.Close();

        _shouldListen = false;
        _client?.Close();
        _client?.Dispose();
        _client = null;

        Debug.WriteLine($"[Net]: Closed");

        if (!_isServer)
        { OnDisconnectedFromServer_Invoke(); }
    }

    #endregion

    #region Message Handling & Receiving

    public override void Tick()
    {
        int endlessLoop = 500;

        while (_incomingQueue.TryDequeue(out RawMessage messageIn) &&
               endlessLoop-- > 0)
        { OnReceiveInternal(messageIn.Buffer.ToArray(), messageIn.Source); }

        SendImmediate(_outgoingQueue);
        _outgoingQueue.Clear();

        if (IsServer)
        {
            TickServer();
        }
        else
        {
            TickClient();
        }

        TryRefreshUserInfos();
    }

    #endregion

    #region Immediate Message Sending

    protected override void SendImmediate(Message message)
    {
        if (_client is null) return;

        _sentAt = Time.NowNoCache;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        if (IsServer)
        {
            Debug.WriteLine($"[Net]: == ALL => {message}");
            foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
            {
                message.Index = client.Value.SendingIndex++;
                byte[] data = Utils.Serialize(message);
                _client.Send(data, data.Length, client.Key);
                _sentBytes += data.Length;
                if (message is ReliableMessage reliableMessage &&
                    reliableMessage.ShouldAck)
                {
                    Debug.WriteLine($"[Net]: == {client.Key} => Waiting ACK for {message.Index} ...");
                    client.Value.SentReliableMessages[message.Index] = (reliableMessage.Copy(), (float)Time.NowNoCache);
                }
            }
        }
        else
        {
            message.Index = _sendingIndex++;
            byte[] data = Utils.Serialize(message);
            Debug.WriteLine($"[Net]: == SERVER => {message}");
            _client.Send(data, data.Length);
            _sentBytes += data.Length;
            if (message is ReliableMessage reliableMessage &&
                reliableMessage.ShouldAck)
            {
                Debug.WriteLine($"[Net]: == SERVER => Waiting ACK for {message.Index} ...");
                _sentReliableMessages[message.Index] = (reliableMessage.Copy(), (float)Time.NowNoCache);
            }
        }
    }

    protected override void SendImmediate(byte[] data, IEnumerable<Message> message)
    {
        if (_client is null) return;

        _sentAt = Time.NowNoCache;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        if (IsServer)
        {
            Debug.WriteLine($"[Net]: == ALL => {string.Join(", ", message)}");
            foreach (IPEndPoint client in _connections.Keys)
            {
                _client.Send(data, data.Length, client);
                _sentBytes += data.Length;
            }
        }
        else
        {
            Debug.WriteLine($"[Net]: == SERVER => {string.Join(", ", message)}");
            _client.Send(data, data.Length);
            _sentBytes += data.Length;
        }
    }

    protected override void SendImmediateTo(byte[] data, IPEndPoint destination, IEnumerable<Message> messages)
    {
        if (_client is null) return;

        _sentAt = Time.NowNoCache;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        if (IsServer)
        {
            if (_connections.TryGetValue(destination, out ConnectionClient? client))
            {
                Debug.WriteLine($"[Net]: == {destination} => {string.Join(", ", messages)}");
                _client.Send(data, data.Length, client.EndPoint);
                _sentBytes += data.Length;
                client.SentAt = Time.NowNoCache;
            }
        }
        else
        {
            if (destination.Equals(RemoteEndPoint))
            {
                Debug.WriteLine($"[Net]: == SERVER => {string.Join(", ", messages)}");
                _client.Send(data, data.Length);
                _sentBytes += data.Length;
            }
        }
    }

    #endregion
}
