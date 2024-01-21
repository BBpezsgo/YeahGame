﻿using System.Diagnostics;
using System.Net;
using Win32.LowLevel;

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
    #region Constants

    const int GraphWidth = 20;
    const bool DebugPanel = true;

    #endregion

    #region Public Static Stuff

    public static Game Singleton => singleton!;
    public static ConsoleRenderer Renderer => singleton!.renderer;
    public static bool IsServer => singleton!._connection.IsServer;
    public static Connection<PlayerInfo> Connection => singleton!._connection;

    #endregion

    #region Fields

    static Game? singleton;

    readonly ConsoleRenderer renderer;

    readonly Connection<PlayerInfo> _connection;

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

    readonly List<Scene> Scenes = new();

    #endregion

    public readonly MenuScene MenuScene;
    public readonly GameScene GameScene;

    public Game()
    {
        singleton = this;

        renderer = new ConsoleRenderer();

        _currentFps.Reset();

        _connection = new Connection<PlayerInfo>();

        _connection.OnClientConnected += (client, phase) => Debug.WriteLine($"Client {client} connecting: {phase}");
        _connection.OnClientDisconnected += (client) => Debug.WriteLine($"Client {client} disconnected");
        _connection.OnConnectedToServer += (phase) => Debug.WriteLine($"Connected to server: {phase}");
        _connection.OnDisconnectedFromServer += () => Debug.WriteLine($"Disconnected from server");

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
                i++;
                if (i > args.Length)
                {
                    WriteError($"Expected socket after \"--host\"");
                    return;
                }

                if (!MenuScene.TryParseSocket(args[i], out IPAddress? address, out ushort port, out string? error))
                {
                    WriteError(error);
                    return;
                }

                _connection.StartHost(address, port);
                continue;
            }

            if (args[i] == "--username")
            {
                i++;
                if (i > args.Length)
                {
                    WriteError($"Expected text after \"--username\"");
                    return;
                }

                _connection.LocalUserInfo = new PlayerInfo() { Username = args[i] };
                continue;
            }
        }

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

            Tick();

            renderer.Render();
        }

        _connection.Close();
        ConsoleListener.Stop();
        ConsoleHandler.Restore();
    }

    void Tick()
    {
        _connection.Tick();

        if (!_connection.IsConnected)
        { LoadScene("Menu"); }
        else
        { LoadScene("Game"); }

        for (int i = 0; i < Scenes.Count; i++)
        {
            if (Scenes[i].IsLoaded)
            {
                Scenes[i].Tick();
                Scenes[i].Render();
            }
        }

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

            if (Time.Now - _lastConnectionCounterReset >= 1f)
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
            renderer.Panel(_debugPanel, _debugPanel.IsActive ? CharColor.BrightCyan : CharColor.White, Ascii.PanelSides);
            ref SmallRect rect = ref _debugPanel.Rect;

            int y = rect.Y + 1;

            renderer.Dropdown(rect.X + 1, y, _fpsDropdown, $"FPS: {(int)(_currentFps.Min)}", Styles.DropdownStyle);
            y++;
            if (_fpsDropdown)
            {
                SmallRect graphRect = new(rect.X + 1, y, GraphWidth, 5);
                y += graphRect.Height;
                _fps.Render(graphRect, renderer, true);
            }

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
    }
}
