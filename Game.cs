using System.Diagnostics;
using System.Net;
using Win32.LowLevel;
using YeahGame.Messages;

namespace YeahGame;

public class PlayerInfo : ISerializable
{
    public required string Username { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Username = reader.ReadString();
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Username);
    }
}

public class Game
{
    const int GraphWidth = 20;

    static Game? singleton;
    public static Game Singleton => singleton!;

    readonly ConsoleRenderer renderer;
    public static ConsoleRenderer Renderer => singleton!.renderer;

    public static bool IsServer => singleton!._connection.IsServer;
    public static Connection<PlayerInfo> Connection => singleton!._connection;

    readonly Connection<PlayerInfo> _connection;

    readonly ConsoleDropdown _fpsDropdown = new();
    readonly ConsoleDropdown _sentBytesDropdown = new();
    readonly ConsoleDropdown _receivedBytesDropdown = new();
    readonly ConsoleDropdown _memoryDropdown = new();

    readonly ConsolePanel _debugPanel = new(new SmallRect(1, 1, 30, 25));

    float _lastConnectionCounterReset;
    Graph _sentBytes;
    Graph _receivedBytes;
    int _sentBytesPerSec;
    int _receivedBytesPerSec;

    float _lastFpsCounterReset;
    MinMax<int> _currentFps;
    MinMaxGraph _fps;

    float _lastMemoryCounterReset;
    long _lastAllocatedMemory;
    int _allocatePerSec;
    Graph _memory;

    public readonly MenuScene MenuScene;
    public readonly GameScene GameScene;

    readonly List<Scene> Scenes = new();

    public Game()
    {
        singleton = this;

        renderer = new ConsoleRenderer();

        _sentBytes = new Graph(GraphWidth + 1);
        _receivedBytes = new Graph(GraphWidth + 1);
        _fps = new MinMaxGraph(GraphWidth + 1);
        _memory = new Graph(GraphWidth + 1);

        _currentFps.Reset();

        _connection = new Connection<PlayerInfo>();

        _connection.OnClientConnected += (client, phase) => Debug.WriteLine($"Client {client} connecting: {phase}");
        _connection.OnClientDisconnected += (client) => Debug.WriteLine($"Client {client} disconnected");
        _connection.OnConnectedToServer += (phase) => Debug.WriteLine($"Connected to server: {phase}");
        _connection.OnDisconnectedFromServer += () => Debug.WriteLine($"Disconnected from server");

        _connection.OnMessageReceived += OnMessageReceived;
        _connection.OnClientConnected += OnClientConnected;
        _connection.OnClientDisconnected += OnClientDisconnected;

        MenuScene = new MenuScene();
        Scenes.Add(MenuScene);

        GameScene = new GameScene();
        _connection.OnMessageReceived += GameScene.OnMessageReceived;
        _connection.OnClientConnected += GameScene.OnClientConnected;
        _connection.OnClientDisconnected += GameScene.OnClientDisconnected;
        _connection.OnDisconnectedFromServer += GameScene.OnDisconnectedFromServer;
        Scenes.Add(GameScene);
    }

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
        bool wasResized = false;

        Console.WindowWidth = 80;
        Console.BufferWidth = 80;
        Console.WindowHeight = 30;
        Console.BufferHeight = 30;

        ConsoleListener.KeyEvent += Keyboard.Feed;
        ConsoleListener.MouseEvent += Mouse.Feed;
        ConsoleListener.WindowBufferSizeEvent += _ => wasResized = true;

        ConsoleListener.Start();
        ConsoleHandler.Setup();

        static void WriteError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--host")
            {
                if (i + 1 > args.Length)
                {
                    WriteError($"Expected socket after \"--host\"");
                    continue;
                }

                if (!MenuScene.TryParseSocket(args[i + 1], out System.Net.IPAddress? address, out ushort port, out string? error))
                {
                    WriteError(error);
                    continue;
                }

                _connection.StartHost(address, port);
                continue;
            }
        }

        while (true)
        {
            Time.Tick();
            Keyboard.Tick();
            Mouse.Tick();

            if (Keyboard.IsKeyDown(VirtualKeyCode.ESCAPE))
            { break; }

            if (wasResized)
            {
                renderer.RefreshBufferSize();
                wasResized = false;
            }
            else
            {
                renderer.ClearBuffer();
            }

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
                _fps.Append((_currentFps.Min, _currentFps.Max));

                _currentFps.Reset();
            }
            else
            {
                _currentFps.Set((int)Time.FPS);
            }

            if (Time.Now - _lastConnectionCounterReset >= 1f)
            {
                _lastConnectionCounterReset = Time.Now;
                _sentBytesPerSec = _connection.SentBytes;
                _receivedBytesPerSec = _connection.ReceivedBytes;

                _sentBytes.Append(_sentBytesPerSec);
                _receivedBytes.Append(_receivedBytesPerSec);

                _connection.ResetCounter();
            }

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

            _connection.Tick();

            if (!_connection.IsConnected)
            {
                LoadScene("Menu");
            }
            else
            {
                LoadScene("Game");
            }

            for (int i = 0; i < Scenes.Count; i++)
            {
                if (Scenes[i].IsLoaded)
                {
                    Scenes[i].Tick();
                    Scenes[i].Render();
                }
            }

            {
                renderer.Fill(_debugPanel.Rect, 0, ' ');
                renderer.Panel(_debugPanel, _debugPanel.IsActive ? CharColor.BrightCyan : CharColor.White, Ascii.PanelSides);
                ref SmallRect rect = ref _debugPanel.Rect;

                int y = rect.Y + 1;

                renderer.Dropdown(rect.X + 1, y, _fpsDropdown, $"FPS: {(int)(_currentFps.Min)}", Utils.DropdownStyle);
                y++;
                if (_fpsDropdown)
                {
                    SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                    y += graphRect.Height;
                    _fps.Render(graphRect, renderer, true);
                }

                renderer.Text(rect.X + 1, y++, $"State: {_connection.State}");

                renderer.Dropdown(rect.X + 1, y, _sentBytesDropdown, $"Sent: {_sentBytesPerSec} bytes/sec", Utils.DropdownStyle);
                y++;
                if (_sentBytesDropdown)
                {
                    SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                    y += graphRect.Height;
                    _sentBytes.Render(graphRect, renderer, false);
                }

                renderer.Dropdown(rect.X + 1, y, _receivedBytesDropdown, $"Received: {_receivedBytesPerSec} bytes/sec", Utils.DropdownStyle);
                y++;
                if (_receivedBytesDropdown)
                {
                    SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                    y += graphRect.Height;
                    _receivedBytes.Render(graphRect, renderer, false);
                }

                if (Utils.IsDebug)
                {
                    renderer.Dropdown(rect.X + 1, y, _memoryDropdown, $"Alloc: {Utils.FormatMemorySize(_allocatePerSec)}/sec", Utils.DropdownStyle);
                    y++;
                    if (_memoryDropdown)
                    {
                        SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                        y += graphRect.Height;
                        _memory.Render(graphRect, renderer, false);
                    }
                }

                rect.Height = (short)(y - rect.Y);
            }

            // {
            //     ConsoleChar c = renderer[Mouse.RecordedConsolePosition];
            //     renderer[Mouse.RecordedConsolePosition] = new ConsoleChar(c.Char, CharColor.Invert(c.Foreground), CharColor.Invert(c.Background));
            // }

            renderer.Render();
        }

        _connection.Close();
        ConsoleListener.Stop();
        ConsoleHandler.Restore();
    }

    void OnClientDisconnected(IPEndPoint client)
    {

    }

    void OnClientConnected(IPEndPoint client, Connection.ConnectingPhase phase)
    {

    }

    void OnMessageReceived(Message message, IPEndPoint source)
    {

    }
}
