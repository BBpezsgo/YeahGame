using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace YeahGame;

public class Game
{
    static Game? singleton;
    public static Game Singleton => singleton!;

    readonly ConsoleRenderer renderer;
    public static ConsoleRenderer Renderer => singleton!.renderer;

    float lastNetworkSync;

    public bool ShouldSync => Time.Now - lastNetworkSync > .5f;

    public Connection Connection;

    public List<Player> players = new();
    public List<Projectile> projectiles = new();

    ConsoleRenderer.TextField GUI_InputField = new("127.0.0.1");
    string? GUI_InputError = null;

    readonly ConsoleRenderer.ButtonStyle buttonStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Hover = CharColor.Make(CharColor.Black, CharColor.White),
        Down = CharColor.Make(CharColor.Black, CharColor.BrightCyan),
    };

    readonly ConsoleRenderer.TextFieldStyle textFieldStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Active = CharColor.Make(CharColor.Black, CharColor.White),
    };

    public Game()
    {
        singleton = this;
        renderer = new ConsoleRenderer();
        Connection = new Connection();
    }

    public void Start()
    {
        bool wasResized = false;
        Connection.OnClientConnected += (client, phase) => Debug.WriteLine($"Client {client} connecting: {phase}");
        Connection.OnClientDisconnected += (client) => Debug.WriteLine($"Client {client} disconnected");
        Connection.OnConnectedToServer += (phase) => Debug.WriteLine($"Connected to server: {phase}");
        Connection.OnDisconnectedFromServer += () => Debug.WriteLine($"Disconnected from server");

        Connection.OnMessageReceived += OnMessageReceived;
        Connection.OnClientConnected += OnClientConnected;
        Connection.OnClientDisconnected += OnClientDisconnected; ;

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

            if (!Connection.IsConnected)
            {
                SmallRect box = Layout.Center(new Coord(30, 8), new SmallRect(default, renderer.Rect));

                renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);

                renderer.Text(box.Left + 2, box.Top + 2, "Socket:");

                renderer.InputField(
                    new SmallRect((short)(box.Left + 2), (short)(box.Top + 3), (short)(box.Width - 4), 1),
                    textFieldStyle,
                    ref GUI_InputField);

                renderer.Text(box.Left + 2, box.Top + 4, GUI_InputError, CharColor.BrightRed);

                if (renderer.Button(
                    Layout.Center(
                        new Coord(10, 1),
                        new SmallRect(box.Left, (short)(box.Top + 4), box.Width, 1)
                        ),
                    "Connect",
                    buttonStyle))
                {
                    GUI_InputError = null;
                    if (IPAddress.TryParse(GUI_InputField.Value.ToString(), out IPAddress? address))
                    { Connection.Client(address, 5000); }
                    else
                    { GUI_InputError = "Invalid input"; }
                }

                if (renderer.Button(
                    Layout.Center(
                        new Coord(10, 1),
                        new SmallRect(box.Left, (short)(box.Top + 5), box.Width, 1)
                        ),
                    "Host",
                    buttonStyle))
                {
                    GUI_InputError = null;
                    if (IPAddress.TryParse(GUI_InputField.Value.ToString(), out IPAddress? address))
                    { Connection.Server(address, 5000); }
                    else
                    { GUI_InputError = "Invalid input"; }
                }
            }
            else
            {
                Connection.Tick();

                if (!TryGetLocalPlayer(out _) &&
                    Connection.IsServer &&
                    Connection.IsConnected)
                {
                    Player player = new()
                    {
                        Owner = Connection.LocalAddress!.ToString(),
                        NetworkId = GenerateNetworkId(),
                        Position = new Vector2(10, 5),
                    };
                    player.NetworkSpawn();
                    players.Add(player);
                }

                players.UpdateAll();
                projectiles.UpdateAll();

                players.RenderAll();
                projectiles.RenderAll();

                if (ShouldSync)
                { lastNetworkSync = Time.Now; }
            }

            renderer.Text(0, 0, $"FPS: {(int)(1d / Time.Delta)}");

            renderer.Render();
        }
    }

    void OnClientDisconnected(IPEndPoint client)
    {
        for (int i = players.Count - 1; i >= 0; i--)
        {
            if (players[i].Owner == client.ToString())
            { players.RemoveAt(i); }
        }
    }

    void OnClientConnected(IPEndPoint client, Connection.ConnectingPhase phase)
    {
        if (phase != Connection.ConnectingPhase.Handshake) return;

        Player player = new()
        {
            Owner = client.ToString(),
            NetworkId = GenerateNetworkId(),
            Position = new Vector2(10, 5),
        };
        player.NetworkSpawn();
        players.Add(player);
    }

    void OnMessageReceived(Messages.Message message, IPEndPoint source)
    {
        if (message is Messages.ObjectSyncMessage objectMessage)
        {
            if (!TryGetNetworkEntity(objectMessage.ObjectId, out NetworkEntity? entity))
            {
                if (Connection.IsServer)
                { throw new NotImplementedException(); }

                Connection.Send(new Messages.ObjectControlMessage()
                {
                    ObjectId = objectMessage.ObjectId,
                    Kind = Messages.ObjectControlMessageKind.NotFound,
                });
                Debug.WriteLine($"[Net]: Object {objectMessage.ObjectId} not found ...");
            }
            else
            {
                entity.HandleMessage(objectMessage);
            }

            return;
        }

        if (message is Messages.ObjectControlMessage objectControlMessage)
        {
            switch (objectControlMessage.Kind)
            {
                case Messages.ObjectControlMessageKind.Spawn:
                {
                    if (Connection.IsServer) break;

                    NetworkEntity entity = GenerateNetworkEntity(objectControlMessage);

                    if (entity is Player player)
                    { players.Add(player); }
                    else
                    { throw new NotImplementedException(); }

                    Debug.WriteLine($"[Net]: Spawning object {objectControlMessage.ObjectId} ...");

                    break;
                }
                case Messages.ObjectControlMessageKind.Destroy:
                {
                    if (Connection.IsServer) break;

                    Debug.WriteLine($"[Net]: Destroying object {objectControlMessage.ObjectId} ...");

                    for (int i = players.Count - 1; i >= 0; i--)
                    {
                        if (players[i].NetworkId == objectControlMessage.ObjectId)
                        { players.RemoveAt(i); }
                    }

                    break;
                }
                case Messages.ObjectControlMessageKind.NotFound:
                {
                    if (!Connection.IsServer) break;

                    if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? entity))
                    {
                        Debug.WriteLine($"[Net]: Sending object info for {objectControlMessage.ObjectId} to {source} ...");
                        entity.NetworkInfo(source);
                    }
                    else
                    {
                        Debug.WriteLine($"[Net]: Sending object info for {objectControlMessage.ObjectId}: destroyed to {source} ...");

                        Connection.SendTo(new Messages.ObjectControlMessage()
                        {
                            Kind = Messages.ObjectControlMessageKind.Destroy,
                            ObjectId = objectControlMessage.ObjectId,
                        }, source);
                    }

                    break;
                }
                case Messages.ObjectControlMessageKind.Info:
                {
                    if (Connection.IsServer) break;

                    Debug.WriteLine($"[Net]: Received object info for {objectControlMessage.ObjectId} ...");

                    if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? entity))
                    {
                        Utils.Deserialize(objectControlMessage.Details, entity.NetworkDeserialize);
                    }
                    else
                    {
                        entity = GenerateNetworkEntity(objectControlMessage);

                        if (entity is Player player)
                        { players.Add(player); }
                        else
                        { throw new NotImplementedException(); }
                    }

                    break;
                }
                default:
                    break;
            }
            return;
        }
    }

    public int GenerateNetworkId()
    {
        int id = 1;
        while (TryGetNetworkEntity(id, out _))
        { id++; }
        return id;
    }

    public static NetworkEntity GenerateNetworkEntity(Messages.ObjectControlMessage message)
        => GenerateNetworkEntity(message.EntityPrototype, message.ObjectId, message.Details);

    public static NetworkEntity GenerateNetworkEntity(EntityPrototype prototype, int networkId, byte[] details)
    {
        NetworkEntity result = prototype switch
        {
            EntityPrototype.Player => new Player(),
            _ => throw new NotImplementedException(),
        };

        result.NetworkId = networkId;

        Utils.Deserialize(details, result.NetworkDeserialize);

        return result;
    }

    public bool TryGetNetworkEntity(int id, [NotNullWhen(true)] out NetworkEntity? entity)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].NetworkId == id)
            {
                entity = players[i];
                return true;
            }
        }

        entity = null;
        return false;
    }

    public bool TryGetLocalPlayer([NotNullWhen(true)] out Player? player)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Owner == Connection.LocalAddress?.ToString())
            {
                player = players[i];
                return true;
            }
        }

        player = null;
        return false;
    }
}
