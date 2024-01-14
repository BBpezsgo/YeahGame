using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace YeahGame;

public class Game
{
    static Game? singleton;
    public static Game Singleton => singleton!;

    readonly ConsoleRenderer renderer;
    public static ConsoleRenderer Renderer => singleton!.renderer;

    public Connection Connection;

    public List<Player> players = new();
    public List<Projectile> projectiles = new();

    public Game()
    {
        singleton = this;
        renderer = new ConsoleRenderer();

        Connection = new Connection();
    }

    public void Start()
    {
        bool wasResized = false;

        char input = Console.ReadKey().KeyChar;
        Console.Clear();

        Connection.OnClientConnected += (client, phase) => Console.WriteLine($"Client {client} connecting: {phase}");
        Connection.OnClientDisconnected += (client) => Console.WriteLine($"Client {client} disconnected");
        Connection.OnConnectedToServer += (phase) => Console.WriteLine($"Connected to server: {phase}");
        Connection.OnDisconnectedFromServer += () => Console.WriteLine($"Disconnected from server");

        Connection.OnMessageReceived += OnMessageReceived;
        Connection.OnClientConnected += OnClientConnected;
        Connection.OnClientDisconnected += OnClientDisconnected; ;

        if (input == 's')
        { Connection.Server(IPAddress.Parse("127.0.0.1"), 5000); }
        else
        { Connection.Client(IPAddress.Parse("127.0.0.1"), 5000); }

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

            Connection.Tick();

            if (wasResized)
            {
                renderer.RefreshBufferSize();
                wasResized = false;
            }
            else
            {
                renderer.ClearBuffer();
            }

            if (!TryGetLocalPlayer(out _) &&
                Connection.IsServer &&
                Connection.IsConnected)
            {
                Player player = new()
                {
                    Owner = Connection.LocalAddress!.ToString()
                };
                player.NetworkSpawn(player.Prototype);
                players.Add(player);
            }

            players.UpdateAll();
            projectiles.UpdateAll();

            players.RenderAll();
            projectiles.RenderAll();

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
            Owner = client.ToString()
        };
        player.NetworkSpawn(player.Prototype);
        players.Add(player);
    }

    void OnMessageReceived(Messages.Message message, IPEndPoint source)
    {
        if (message is Messages.ObjectMessage objectMessage)
        {
            if (!TryGetNetworkEntity(objectMessage.ObjectId, out NetworkEntity? entity))
            {
                Connection.Send(new Messages.ObjectControlMessage()
                {
                    Type = Messages.MessageType.ObjectControl,
                    ObjectId = objectMessage.ObjectId,
                    Kind = Messages.ObjectControlMessageKind.NotFound,
                });
                return;
            }

            entity.HandleMessage(objectMessage);
            return;
        }

        if (message is Messages.ObjectControlMessage objectControlMessage)
        {
            switch (objectControlMessage.Kind)
            {
                case Messages.ObjectControlMessageKind.Spawn:
                {
                    if (!Connection.IsServer)
                    {
                        NetworkEntity entity = GenerateNetworkEntity(objectControlMessage);

                        if (entity is Player player)
                        { players.Add(player); }
                        else
                        { throw new NotImplementedException(); }
                    }
                    break;
                }
                case Messages.ObjectControlMessageKind.Destroy:
                {
                    if (!Connection.IsServer)
                    {
                        for (int i = players.Count - 1; i >= 0; i--)
                        {
                            if (players[i].NetworkId == objectControlMessage.ObjectId)
                            { players.RemoveAt(i); }
                        }
                    }
                    break;
                }
                case Messages.ObjectControlMessageKind.NotFound:
                {
                    if (Connection.IsServer)
                    {
                        if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? entity))
                        {
                            entity.NetworkInfo(entity.Prototype);
                        }
                        else
                        {
                            Connection.Send(new Messages.ObjectControlMessage()
                            {
                                Type = Messages.MessageType.ObjectControl,
                                Kind = Messages.ObjectControlMessageKind.Destroy,
                                ObjectId = objectControlMessage.ObjectId,
                            });
                        }
                    }
                    break;
                }
                case Messages.ObjectControlMessageKind.Info:
                {
                    if (!Connection.IsServer)
                    {
                        if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? entity))
                        {
                            using MemoryStream stream = new(objectControlMessage.Details);
                            using BinaryReader reader = new(stream);
                            entity.NetworkDeserialize(reader);
                        }
                        else
                        {
                            entity = GenerateNetworkEntity(objectControlMessage);

                            if (entity is Player player)
                            { players.Add(player); }
                            else
                            { throw new NotImplementedException(); }
                        }
                    }
                    break;
                }
                default:
                    break;
            }
            return;
        }
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

        using MemoryStream stream = new(details);
        using BinaryReader reader = new(stream);
        result.NetworkDeserialize(reader);

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
