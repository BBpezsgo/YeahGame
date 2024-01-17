using System.Diagnostics;
using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public class PlayerInfo : ISerializable
{
    public string Username;

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
    static Game? singleton;
    public static Game Singleton => singleton!;

    readonly ConsoleRenderer renderer;
    public static ConsoleRenderer Renderer => singleton!.renderer;

    public static bool IsServer => singleton!._connection.IsServer;
    public static Connection<PlayerInfo> Connection => singleton!._connection;

    readonly Connection<PlayerInfo> _connection;

    public readonly MenuScene MenuScene;
    public readonly GameScene GameScene;

    readonly List<Scene> Scenes = new();

    public Game()
    {
        singleton = this;
        renderer = new ConsoleRenderer();

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
        Scenes.Add(GameScene);
    }

    public void LoadScene(string name)
    {
        for (int i = 0; i < Scenes.Count; i++)
        {
            if (Scenes[i].Name == name)
            { if (!Scenes[i].IsLoaded) Scenes[i].Load(); }
            else
            { if (Scenes[i].IsLoaded) Scenes[i].Unload(); }
        }
    }

    public void Start()
    {
        bool wasResized = false;

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

            if (wasResized)
            {
                renderer.RefreshBufferSize();
                wasResized = false;
            }
            else
            {
                renderer.ClearBuffer();
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

            renderer.Text(0, 0, $"FPS: {(int)(1d / Time.Delta)}");

            renderer.Render();
        }
    }

    void OnClientDisconnected(IPEndPoint client)
    {

    }

    void OnClientConnected(IPEndPoint client, Connection<PlayerInfo>.ConnectingPhase phase)
    {

    }

    void OnMessageReceived(Message message, IPEndPoint source)
    {

    }
}
