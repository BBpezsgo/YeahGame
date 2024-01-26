using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Win32.LowLevel;

namespace YeahGame;

public class PlayerInfo : ISerializable
{
    public Changed<string> Username;
    public ImmutableChanged<List<ItemType>> Items = new List<ItemType>();

    public void Deserialize(BinaryReader reader)
    {
        Bitfield _changedBitfield = reader.ReadByte();

        if (_changedBitfield[0])
        { Username = reader.ReadString(); }
        Username.WasChanged = false;

        if (_changedBitfield[1])
        { Items = new List<ItemType>(reader.ReadCollection(static v => (ItemType)v.ReadByte())); }
        Items.WasChanged = false;
    }

    public void Serialize(BinaryWriter writer)
    {
        Bitfield _changedBitfield = 0;
        _changedBitfield[0] = Username.WasChanged;
        _changedBitfield[1] = Items.WasChanged;
        writer.Write((byte)_changedBitfield);

        if (Username.WasChanged)
        { writer.Write(Username); }
        Username.WasChanged = false;

        if (Items.WasChanged)
        { writer.Write(Items.Value, static (v, item) => v.Write((byte)item)); }
        Items.WasChanged = false;
    }
}

public class Game
{
    #region Constants

    const int GraphWidth = 20;
    const bool DebugPanel = true;

    #endregion

    #region Public Static Stuff

    public static Game Singleton => singleton!;
    public static IRenderer<ConsoleChar> Renderer
    {
#if SERVER
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        get => throw new PlatformNotSupportedException();
#else
        get => singleton!.renderer;
#endif
    }
    public static bool IsServer => singleton!._connection?.IsServer ?? false;
    [MemberNotNullWhen(true, nameof(_connection))]
    [MemberNotNullWhen(true, nameof(Connection))]
    public static bool HasConnection => singleton!._connection is not null;
    public static ConnectionBase<PlayerInfo> Connection
    {
        get => singleton!._connection ?? throw new NullReferenceException($"{nameof(_connection)} is null");
        set => singleton!._connection = value;
    }
    public static bool IsOffline
    {
        get => singleton!._isOffline;
        set => singleton!._isOffline = value;
    }
    #endregion

    #region Fields

    static Game? singleton;

#if !SERVER
    readonly IRenderer<ConsoleChar> renderer;
#endif

    ConnectionBase<PlayerInfo>? _connection;
    bool _isOffline;

#if !SERVER

    readonly ConsoleDropdown _fpsDropdown = new();
    readonly ConsoleDropdown _sentBytesDropdown = new();
    readonly ConsoleDropdown _receivedBytesDropdown = new();
    // readonly ConsoleDropdown _memoryDropdown = new();

    readonly ConsolePanel _debugPanel = new(new SmallRect(1, 1, 33, 2));

    float _lastConnectionCounterReset;
    Graph _sentBytes = new(GraphWidth + 1);
    ColoredGraph _receivedBytes = new(GraphWidth + 1);
    int _sentBytesPerSec;
    int _receivedBytesPerSec;
    int _lastLostPackets;
    float _packetLoss;

    float _lastFpsCounterReset;
    MinMax<int> _currentFps;
    Graph _fps = new(GraphWidth + 1);

    // float _lastMemoryCounterReset;
    // long _lastAllocatedMemory;
    // int _allocatePerSec;
    // Graph _memory = new(GraphWidth + 1);

#endif

    readonly List<Scene> Scenes = new();

    bool _shouldRun;

    #endregion

    public readonly MenuScene MenuScene;
    public readonly GameScene GameScene;

    public Game(
#if !SERVER
        IRenderer<ConsoleChar> _renderer
#endif
        )
    {
        singleton = this;

#if !SERVER
        renderer = _renderer;
#endif

        MenuScene = new MenuScene();
        Scenes.Add(MenuScene);

        GameScene = new GameScene();
        Scenes.Add(GameScene);

#if !SERVER
        _currentFps.Reset();
        _debugPanel.Rect.X = (short)(_renderer.Width - _debugPanel.Rect.Width);
#endif
    }

    public void SetupConnectionListeners<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TUserInfo>(ConnectionBase<TUserInfo> connection) where TUserInfo : ISerializable
    {
        connection.OnMessageReceived += GameScene.OnMessageReceived;
        connection.OnClientConnected += GameScene.OnClientConnected;
        connection.OnClientDisconnected += GameScene.OnClientDisconnected;
        connection.OnDisconnectedFromServer += GameScene.OnDisconnectedFromServer;
    }

    public static void Stop()
    { if (singleton is not null) singleton._shouldRun = false; }

    public void LoadScene(string name)
    {
        for (int i = 0; i < Scenes.Count; i++)
        {
            if (Scenes[i].Name != name &&
                Scenes[i].IsLoaded)
            { Scenes[i].Unload(); }
        }

        for (int i = 0; i < Scenes.Count; i++)
        {
            if (Scenes[i].Name == name &&
                !Scenes[i].IsLoaded)
            { Scenes[i].Load(); }
        }
    }

    public void Start(string[] args)
    {
#if !SERVER
        bool wasResized = false;
#endif

#if SERVER
        ProcessArguments(args);
#else
        if (true) ProcessArguments(args);
#endif

#if !SERVER
        if (!OperatingSystem.IsBrowser())
        {
            Console.WindowWidth = 80;
            Console.WindowHeight = 30;

            if (OperatingSystem.IsWindows())
            {
                Console.BufferWidth = 80;
                Console.BufferHeight = 30;
            }

            ConsoleListener.KeyEvent += Keyboard.Feed;
            ConsoleListener.MouseEvent += Mouse.Feed;
            ConsoleListener.WindowBufferSizeEvent += _ => wasResized = true;

            if (OperatingSystem.IsWindows())
            {
                ConsoleListener.Start();
                ConsoleHandler.Setup();
            }
        }
#endif

        _shouldRun = true;

        while (_shouldRun)
        {
            Time.Tick();
#if !SERVER
            Keyboard.Tick();
            Mouse.Tick();

            if (Keyboard.IsKeyDown(VirtualKeyCode.ESCAPE))
            { break; }

            if (wasResized)
            {
                renderer.RefreshBufferSize();
                wasResized = false;
                _debugPanel.ClampPosition(renderer.Size);
            }
            else
            {
                renderer.ClearBuffer();
            }
#endif

            Tick();

#if !SERVER
            renderer.Render();
#endif
        }

        _connection?.Close();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ConsoleListener.Stop();
            ConsoleHandler.Restore();
        }
    }

    void ProcessArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--username")
            {
                i++;
                if (i > args.Length)
                {
                    Console.WriteLine($"Expected text after \"--username\"");
                    return;
                }

                _connection ??= new UdpConnection<PlayerInfo>();
                _connection.LocalUserInfo = new PlayerInfo() { Username = args[i] };
                continue;
            }

            if (args[i] == "--host")
            {
                i++;
                if (i > args.Length)
                {
                    Console.WriteLine($"Expected socket after \"--host\"");
                    return;
                }

                if (!MenuScene.TryParseSocket(args[i], out IPEndPoint? endPoint, out string? error))
                {
                    Console.WriteLine(error);
                    return;
                }

                _connection ??= new UdpConnection<PlayerInfo>();
                _connection.StartHost(endPoint);
                continue;
            }

            if (args[i] == "--client")
            {
                i++;
                if (i > args.Length)
                {
                    Console.WriteLine($"Expected socket after \"--client\"");
                    return;
                }

                if (!MenuScene.TryParseSocket(args[i], out IPEndPoint? endPoint, out string? error))
                {
                    Console.WriteLine(error);
                    return;
                }

                _connection ??= new UdpConnection<PlayerInfo>();
                _connection.StartClient(endPoint);
                continue;
            }
        }
    }

    public void Tick()
    {
        _connection?.Tick();

        if (_connection is null || !_connection.IsConnected && !_isOffline)
        { LoadScene("Menu"); }
        else
        { LoadScene("Game"); }

        for (int i = 0; i < Scenes.Count; i++)
        {
            if (Scenes[i].IsLoaded)
            {
                Scenes[i].Tick();
#if !SERVER
                Scenes[i].Render();
#endif
            }
        }

#if !SERVER
        if (DebugPanel)
        {
            /*
            if (Utils.IsDebug)
            {
                if (Time.Now - _lastMemoryCounterReset >= 1f)
                {
                    long currentAllocatedMemory = GC.GetTotalAllocatedBytes();
                    _lastMemoryCounterReset = Time.Now;
                    _allocatePerSec = (int)(currentAllocatedMemory - _lastAllocatedMemory);
                    _memory.Append(_allocatePerSec);
                    _lastAllocatedMemory = currentAllocatedMemory;
                }
            }
            */

            if (Time.Now - _lastFpsCounterReset >= 1f)
            {
                _lastFpsCounterReset = Time.Now;
                // int diff = _currentFps.Difference;
                // byte color = diff switch
                // {
                //     < 50 => CharColor.White,
                //     < 100 => CharColor.BrightYellow,
                //     _ => CharColor.BrightRed,
                // };
                _fps.Append(_currentFps.Min);

                _currentFps.Reset();
            }
            else
            {
                _currentFps.Set((int)Time.FPS);
            }

            if (_connection is not null && Time.Now - _lastConnectionCounterReset >= 1f)
            {
                _lastConnectionCounterReset = Time.Now;
                _sentBytesPerSec = _connection.SentBytes;
                _receivedBytesPerSec = _connection.ReceivedBytes;
                int loss = _connection.LostPackets - _lastLostPackets;
                _lastLostPackets = _connection.LostPackets;

                _packetLoss = (_connection.ReceivedPackets == 0) ? 1f : (1f - ((float)_lastLostPackets / (float)_connection.ReceivedPackets));

                _sentBytes.Append(_sentBytesPerSec);
                _receivedBytes.Append(_receivedBytesPerSec, loss switch
                {
                    > 1 => CharColor.BrightRed,
                    > 0 => CharColor.Yellow,
                    _ => CharColor.White,
                });

                _connection.ResetCounter();
            }

            renderer.Fill(_debugPanel.Rect, 0, ' ');
            renderer.Panel(_debugPanel, _debugPanel.IsActive ? CharColor.BrightCyan : CharColor.White, in Ascii.PanelSides);
            ref SmallRect rect = ref _debugPanel.Rect;

            int y = rect.Y + 1;

            renderer.Dropdown(rect.X + 1, y, _fpsDropdown, $"FPS: {_currentFps.Min}", Styles.DropdownStyle);
            y++;
            if (_fpsDropdown)
            {
                SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                y += graphRect.Height;
                _fps.Render(graphRect, renderer, true);
            }

            if (_connection is not null)
            {
                renderer.Text(rect.X + 1, y++, $"State: {_connection.State}");
                renderer.Text(rect.X + 1, y++, $"Lost Packets: {_connection.LostPackets}");

                renderer.Dropdown(rect.X + 1, y, _sentBytesDropdown, $"Sent: {_sentBytesPerSec} bytes/sec", Styles.DropdownStyle);
                y++;
                if (_sentBytesDropdown)
                {
                    SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                    y += graphRect.Height;
                    _sentBytes.Render(graphRect, renderer, false);
                }

                renderer.Dropdown(rect.X + 1, y, _receivedBytesDropdown, $"Received: {_receivedBytesPerSec} bytes/sec ({(_packetLoss * 100):0}%)", Styles.DropdownStyle);
                y++;
                if (_receivedBytesDropdown)
                {
                    SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                    y += graphRect.Height;
                    _receivedBytes.Render(graphRect, renderer, false);
                }
            }

            // if (Utils.IsDebug)
            // {
            //     renderer.Dropdown(rect.X + 1, y, _memoryDropdown, $"Alloc: {Utils.FormatMemorySize(_allocatePerSec)}/sec", Utils.DropdownStyle);
            //     y++;
            //     if (_memoryDropdown)
            //     {
            //         SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
            //         y += graphRect.Height;
            //         _memory.Render(graphRect, renderer, false);
            //     }
            // }

            rect.Height = (short)(y - rect.Y);
        }

        // {
        //     ConsoleChar c = renderer[Mouse.RecordedConsolePosition];
        //     renderer[Mouse.RecordedConsolePosition] = new ConsoleChar(c.Char, CharColor.Invert(c.Foreground), CharColor.Invert(c.Background));
        // }
#endif
    }
}
