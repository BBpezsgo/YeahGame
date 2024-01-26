using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.Versioning;
using System.Text;
using YeahGame.Messages;

using ConnectionClientDetails = System.Net.WebSockets.WebSocket;
using RawMessage = (System.ReadOnlyMemory<byte> Buffer, System.Net.IPEndPoint Source);

namespace YeahGame;

public class WebSocketConnection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TUserInfo> : ConnectionBase<TUserInfo, ConnectionClientDetails> where TUserInfo : ISerializable
{
    class WebSocketRequest
    {
        public readonly Task<HttpListenerWebSocketContext> Task;
        public readonly IPEndPoint RemoteEndPoint;

        public WebSocketRequest(Task<HttpListenerWebSocketContext> task, IPEndPoint remoteEndPoint)
        {
            Task = task;
            RemoteEndPoint = remoteEndPoint;
        }
    }

    #region Properties

    public override IPEndPoint? RemoteEndPoint => _serverEndPoint;

    public override IPEndPoint? LocalEndPoint
    {
        get
        {
            if (Game.IsOffline) return base.LocalEndPoint;
            if (IsServer) return (IPEndPoint?)IPEndPoint.Parse(new Uri(httpListener.Prefixes.ElementAt(0)).Host);
            return _thisIsMe;
        }
    }

    [MemberNotNullWhen(true, nameof(httpListener))]
    [UnsupportedOSPlatformGuard("browser")]
    public override bool IsServer => httpListener is not null;

    public override ConnectionState State
    {
        get
        {
            if (webSocketClient is not null)
            {
                return webSocketClient.State switch
                {
                    WebSocketState.None => ConnectionState.None,
                    WebSocketState.Connecting => ConnectionState.Connecting,
                    WebSocketState.Open => ConnectionState.Connected,
                    WebSocketState.CloseSent => ConnectionState.None,
                    WebSocketState.CloseReceived => ConnectionState.None,
                    WebSocketState.Closed => ConnectionState.None,
                    WebSocketState.Aborted => ConnectionState.None,
                    _ => ConnectionState.None,
                };
            }

            if (IsServer &&
                httpListener.IsListening)
            { return ConnectionState.Hosting; }

            return ConnectionState.None;
        }
    }

    public override bool IsConnected =>
        (webSocketClient is not null && webSocketClient.State == WebSocketState.Open && _thisIsMe is not null) ||
        IsServer;

    #endregion

    HttpListener? httpListener;
    readonly List<WebSocketRequest> incomingWebSockets = new();
    Task<HttpListenerContext>? httpListenerThreadGetContextTask;

    ClientWebSocket? webSocketClient;
    IPEndPoint? _serverEndPoint;
    Task<WebSocketReceiveResult>? _webSocketClientReceiveTask;
    readonly byte[] _webSocketClientIncomingBuffer = new byte[128];

    [UnsupportedOSPlatform("browser")]
    public override void StartHost(IPEndPoint endPoint)
    {
        Close();

        string url = $"http://{endPoint}/";

        httpListener = new HttpListener();
        httpListener.Prefixes.Add(url);
        httpListener.Start();

        Debug.WriteLine($"[Net:] Listening on {url} ...");

        _lostPackets = 0;
        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;
    }

    public override void StartClient(IPEndPoint endPoint)
    {
        Close();

        string url = $"ws://{endPoint}/";

        Debug.WriteLine($"[Net]: Connecting to {url} ...");

        _serverEndPoint = endPoint;
        webSocketClient = new ClientWebSocket();
        webSocketClient.ConnectAsync(new Uri(url, UriKind.Absolute), CancellationToken.None);

        OnConnectedToServer_Invoke(ConnectingPhase.Connected);

        _lostPackets = 0;
        _receivedAt = Time.NowNoCache;
        _sentAt = Time.NowNoCache;

        Debug.WriteLine($"[Net]: Shaking hands with {_serverEndPoint} ...");

        Send(new HandshakeRequestMessage());
    }

    [UnsupportedOSPlatform("browser")]
    void WsClientJob(object? parameter)
    {
        if (parameter is not WebSocketRequest wsRequest) return;

        WebSocket ws = wsRequest.Task.Result.WebSocket;
        byte[] buffer = new byte[128];
        while (ws.State == WebSocketState.Open)
        {
            Task<WebSocketReceiveResult> receiveTask = ws.ReceiveAsync(buffer, CancellationToken.None);
            try
            { receiveTask.Wait(); }
            catch (Exception) { }

            if (!receiveTask.IsCompletedSuccessfully)
            {
                Debug.WriteLine(receiveTask.Exception);
                continue;
            }

            WebSocketReceiveResult received = receiveTask.Result;

            _receivedAt = Time.NowNoCache;
            _receivedBytes += received.Count;

            switch (received.MessageType)
            {
                case WebSocketMessageType.Text:
                    Debug.WriteLine($"[WSS]: <= {wsRequest.RemoteEndPoint} == \"{Encoding.UTF8.GetString(buffer, 0, received.Count)}\"");
                    break;
                case WebSocketMessageType.Binary:
                    if (IsServer)
                    {
                        if (!_connections.TryGetValue(wsRequest.RemoteEndPoint, out ConnectionClient? client))
                        {
                            client = new ConnectionClient(wsRequest.RemoteEndPoint, wsRequest.Task.Result.WebSocket);
                            _connections.TryAdd(wsRequest.RemoteEndPoint, client);
                            Debug.WriteLine($"[Net]: Client {wsRequest.RemoteEndPoint} sending the first message ...");
                            OnClientConnected_Invoke(wsRequest.RemoteEndPoint, ConnectingPhase.Connected);
                        }

                        client.ReceivedAt = Time.NowNoCache;
                        client.IncomingQueue.Enqueue(buffer.AsMemory(0, received.Count).ToArray());
                    }
                    else
                    {
                        _incomingQueue.Enqueue(new RawMessage(buffer.AsMemory(0, received.Count), wsRequest.RemoteEndPoint));
                    }

                    // Debug.WriteLine($"[WSS]: <= {wsRequest.RemoteEndPoint} == {received.Count} bytes");
                    break;
                case WebSocketMessageType.Close:
                    ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                    break;
                default:
                    break;
            }
        }

        _connections.TryRemove(wsRequest.RemoteEndPoint, out _);
        Debug.WriteLine($"[WSS]: Client {wsRequest.RemoteEndPoint} closed");
    }

    public override void Close()
    {
        base.Close();

        Debug.WriteLine($"[Net]: Closing ...");

        webSocketClient?.Dispose();
        webSocketClient = null;

        if (IsServer)
        {
            httpListener?.Stop();
            httpListener?.Close();
        }
        httpListener = null;

        _serverEndPoint = null;

        _outgoingQueue.Clear();
        _incomingQueue.Clear();

        _connectionRemovals.Clear();
        _connections.Clear();

        Debug.WriteLine($"[Net]: Closed");
    }

    #region Message Handling & Receiving

    public override void Tick()
    {
        if (!OperatingSystem.IsBrowser())
        {
            for (int i = incomingWebSockets.Count - 1; i >= 0; i--)
            {
                WebSocketRequest wsRequest = incomingWebSockets[i];
                if (wsRequest.Task.IsCompleted)
                {
                    incomingWebSockets.RemoveAt(i);
                    if (wsRequest.Task.IsCompletedSuccessfully)
                    {
                        OnClientConnected_Invoke(wsRequest.RemoteEndPoint, ConnectingPhase.Connected);
                        new Thread(WsClientJob).Start(wsRequest);
                    }
                }
            }
        }

        if (IsServer &&
            httpListener.IsListening)
        {
            if (httpListenerThreadGetContextTask is null ||
                httpListenerThreadGetContextTask.IsCompleted)
            {
                if (httpListenerThreadGetContextTask is not null &&
                    httpListenerThreadGetContextTask.IsCompletedSuccessfully)
                {
                    if (httpListenerThreadGetContextTask.Result.Request.IsWebSocketRequest)
                    {
                        Task<HttpListenerWebSocketContext> acceptTask = httpListenerThreadGetContextTask.Result.AcceptWebSocketAsync(null);
                        incomingWebSockets.Add(new WebSocketRequest(acceptTask, httpListenerThreadGetContextTask.Result.Request.RemoteEndPoint));
                    }
                }

                httpListenerThreadGetContextTask = httpListener.GetContextAsync();
            }
        }

        if (webSocketClient is not null)
        {
            if (webSocketClient.State == WebSocketState.Open)
            {
                if (_webSocketClientReceiveTask is null ||
                    _webSocketClientReceiveTask.IsCompleted)
                {
                    if (_webSocketClientReceiveTask is not null &&
                        _webSocketClientReceiveTask.IsCompletedSuccessfully)
                    {
                        OnReceiveInternal(_webSocketClientReceiveTask.Result, _webSocketClientIncomingBuffer.AsMemory()[.._webSocketClientReceiveTask.Result.Count]);
                    }
                    Array.Clear(_webSocketClientIncomingBuffer);
                    _webSocketClientReceiveTask = webSocketClient.ReceiveAsync(new ArraySegment<byte>(_webSocketClientIncomingBuffer), CancellationToken.None);
                }
            }

            if (webSocketClient.CloseStatus.HasValue)
            {
                Debug.WriteLine($"[WS]: Closed: {webSocketClient.CloseStatus} \"{webSocketClient.CloseStatusDescription}\"");
                Game.Singleton.MenuScene.ExitReason = $"Closed ({webSocketClient.CloseStatus}) {webSocketClient.CloseStatusDescription}".TrimEnd();
                Close();
            }
        }

        int endlessLoop = 500;

        while (_incomingQueue.TryDequeue(out RawMessage messageIn) &&
               endlessLoop-- > 0)
        { OnReceiveInternal(messageIn.Buffer.ToArray(), messageIn.Source); }

        if (IsServer)
        {
            SendImmediate(_outgoingQueue);
            _outgoingQueue.Clear();

            TickServer();
        }
        else if (webSocketClient is not null && webSocketClient.State == WebSocketState.Open)
        {
            SendImmediate(_outgoingQueue);
            _outgoingQueue.Clear();

            TickClient();
        }

        TryRefreshUserInfos();
    }

    void OnReceiveInternal(WebSocketReceiveResult result, Memory<byte> webSocketClientIncomingBuffer)
    {
        _receivedAt = Time.NowNoCache;
        _receivedBytes += result.Count;

        switch (result.MessageType)
        {
            case WebSocketMessageType.Text:
                Debug.WriteLine($"[WSS]: <= SERVER == \"{Encoding.UTF8.GetString(webSocketClientIncomingBuffer.ToArray(), 0, result.Count)}\"");
                break;
            case WebSocketMessageType.Binary:
                Debug.WriteLine($"[WSS]: <= SERVER == {result.Count} bytes");
                OnReceiveInternal(webSocketClientIncomingBuffer.ToArray(), RemoteEndPoint ?? throw new NullReferenceException());
                break;
            case WebSocketMessageType.Close:
                Close();
                Game.Singleton.MenuScene.ExitReason = $"Disconnected ({result.CloseStatus}) {result.CloseStatusDescription}".TrimEnd();
                break;
            default:
                break;
        }
    }

    #endregion

    #region Immediate Message Sending

    protected override void SendImmediate(Message message)
    {
        _sentAt = Time.NowNoCache;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        if (IsServer)
        {
            Debug.WriteLine($"[Net]: == ALL => {message}");
            foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
            {
                if (client.Value.Details.State != WebSocketState.Open) continue;

                message.Index = client.Value.SendingIndex++;
                byte[] data = Utils.Serialize(message);
                Task task = client.Value.Details.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
                task.Wait();
                _sentBytes += data.Length;
                if (message is ReliableMessage reliableMessage &&
                    reliableMessage.ShouldAck)
                {
                    Debug.WriteLine($"[Net]: == {client.Key} => Waiting ACK for {message.Index} ...");
                    client.Value.SentReliableMessages[message.Index] = (reliableMessage.Copy(), (float)Time.NowNoCache);
                }
            }
        }
        else if (webSocketClient is not null && webSocketClient.State == WebSocketState.Open)
        {
            message.Index = _sendingIndex++;
            byte[] data = Utils.Serialize(message);
            Debug.WriteLine($"[Net]: == SERVER => {message}");
            Task task = webSocketClient.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
            task.Wait();
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
        _sentAt = Time.NowNoCache;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        if (IsServer)
        {
            Debug.WriteLine($"[Net]: == ALL => {string.Join(", ", message)}");
            foreach (KeyValuePair<IPEndPoint, ConnectionClient> client in _connections)
            {
                if (client.Value.Details.State != WebSocketState.Open) continue;

                Task task = client.Value.Details.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
                task.Wait();
                _sentBytes += data.Length;
            }
        }
        else if (webSocketClient is not null && webSocketClient.State == WebSocketState.Open)
        {
            Debug.WriteLine($"[Net]: == SERVER => {string.Join(", ", message)}");
            Task task = webSocketClient.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
            task.Wait();
            _sentBytes += data.Length;
        }
    }

    protected override void SendImmediateTo(byte[] data, IPEndPoint destination, IEnumerable<Message> messages)
    {
        _sentAt = Time.NowNoCache;

        if (MessageDropProbability != 0f && Random.Shared.NextDouble() < MessageDropProbability)
        { return; }

        if (IsServer)
        {
            if (_connections.TryGetValue(destination, out ConnectionClient? client))
            {
                if (client.Details.State != WebSocketState.Open) return;

                Debug.WriteLine($"[Net]: == {destination} => {string.Join(", ", messages)}");
                Task task = client.Details.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
                task.Wait();
                _sentBytes += data.Length;
                client.SentAt = Time.NowNoCache;
            }
        }
        else if (webSocketClient is not null && webSocketClient.State == WebSocketState.Open)
        {
            if (destination.Equals(RemoteEndPoint))
            {
                Debug.WriteLine($"[Net]: == SERVER => {string.Join(", ", messages)}");
                Task task = webSocketClient.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
                task.Wait();
                _sentBytes += data.Length;
            }
        }
    }

    #endregion

}
