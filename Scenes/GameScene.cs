using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public class GameScene : Scene
{
    public override string Name => "Game";

    float lastNetworkSync;

    public bool ShouldSync => Time.Now - lastNetworkSync > .1f;

    readonly WeakList<Player> _players = new();
    readonly WeakList<Projectile> _projectiles = new();
    readonly WeakList<NetworkEntity> _networkEntities = new();
    readonly List<Entity> _entities = new();

    public readonly bool[] Map;
    public readonly int MapWidth;
    public readonly int MapHeight;

    public IReadOnlyList<Player?> Players => _players;
    public IReadOnlyList<Projectile?> Projectiles => _projectiles;
    public IReadOnlyList<NetworkEntity?> NetworkEntities => _networkEntities;
    public IReadOnlyList<Entity> Entities => _entities;

    public GameScene()
    {
        Map = Utils.LoadMap(File.ReadAllText("map.txt"), out MapWidth);
        MapHeight = Map.Length / MapWidth;
    }

    public override void Load()
    {
        base.Load();

        if (Game.IsServer)
        {
            for (int i = 0; i < 5; i++)
            {
                AddEntity(new Tester()
                {
                    NetworkId = GenerateNetworkId(),
                    Position = Random.Shared.NextVector2(new Vector2(0f, 0f), new Vector2(MapWidth, MapHeight)),
                });
            }
        }
    }

    public override void Unload()
    {
        base.Unload();
    }

    public override void Render()
    {
        _entities.RenderAll();
    }

    public override void Tick()
    {
        if (!TryGetLocalPlayer(out _) &&
            Game.IsServer &&
            Game.Connection.IsConnected)
        {
            Player player = new()
            {
                Owner = Game.Connection.LocalAddress!.ToString(),
                NetworkId = GenerateNetworkId(),
                Position = new Vector2(10, 5),
            };
            SpawnEntity(player);
        }

        _entities.UpdateAll();

        for (int i = 0; i < Map.Length; i++)
        {
            int x = i % MapWidth;
            int y = i / MapWidth;
            if (Game.Renderer.IsVisible(x, y) && Map[i])
            { Game.Renderer[x, y] = (ConsoleChar)Ascii.Blocks.Full; }
        }

        if (ShouldSync)
        { lastNetworkSync = Time.Now; }
    }
    public int GenerateNetworkId()
    {
        int id = 1;
        while (TryGetNetworkEntity(id, out _))
        { id++; }
        return id;
    }

    public static NetworkEntity GenerateNetworkEntity(ObjectControlMessage message)
        => GenerateNetworkEntity(message.EntityPrototype, message.ObjectId, message.Details);

    public static NetworkEntity GenerateNetworkEntity(EntityPrototype prototype, int networkId, byte[] details)
    {
        NetworkEntity result = prototype switch
        {
            EntityPrototype.Player => new Player(),
            EntityPrototype.Tester => new Tester(),
            _ => throw new NotImplementedException(),
        };

        result.NetworkId = networkId;

        Utils.Deserialize(details, result.NetworkDeserialize);

        return result;
    }

    public bool DestroyEntity(Entity entity)
    {
        if (Game.IsServer && entity is NetworkEntity networkEntity)
        {
            Game.Connection.Send(new ObjectControlMessage()
            {
                Kind = ObjectControlMessageKind.Destroy,
                ObjectId = networkEntity.NetworkId,
            });
        }

        return RemoveEntity(entity);
    }

    public bool RemoveEntity(Entity entity)
    {
        if (entity is Player player)
        { _players.Remove(player); }

        if (entity is Projectile projectile)
        { _projectiles.Remove(projectile); }

        return _entities.Remove(entity);
    }

    public void SpawnEntity(Entity entity)
    {
        if (Game.IsServer && entity is NetworkEntity networkEntity)
        {
            byte[] details = Utils.Serialize(networkEntity.NetworkSerialize);
            Game.Connection.Send(new ObjectControlMessage()
            {
                Kind = ObjectControlMessageKind.Spawn,
                ObjectId = networkEntity.NetworkId,
                Details = details,
                EntityPrototype = networkEntity.Prototype,
            });
        }

        AddEntity(entity);
    }

    public void AddEntity(Entity entity)
    {
        _entities.Add(entity);

        if (entity is Player player)
        { _players.Add(player); }

        if (entity is Projectile projectile)
        { _projectiles.Add(projectile); }

        if (entity is NetworkEntity networkEntity)
        { _networkEntities.Add(networkEntity); }
    }

    public bool TryGetNetworkEntity(int id, [NotNullWhen(true)] out NetworkEntity? entity)
    {
        for (int i = 0; i < NetworkEntities.Count; i++)
        {
            NetworkEntity? _entity = NetworkEntities[i];
            if (_entity != null && _entity.NetworkId == id)
            {
                entity = _entity;
                return true;
            }
        }

        entity = null;
        return false;
    }

    public bool TryGetLocalPlayer([NotNullWhen(true)] out Player? player)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            Player? _player = Players[i];
            if (_player != null && _player.Owner == Game.Connection.LocalAddress?.ToString())
            {
                player = _player;
                return true;
            }
        }

        player = null;
        return false;
    }

    public void OnClientDisconnected(IPEndPoint client)
    {
        for (int i = Players.Count - 1; i >= 0; i--)
        {
            Player? player = Players[i];
            if (player != null && player.Owner == client.ToString())
            { DestroyEntity(player); }
        }
    }

    public void OnClientConnected(IPEndPoint client, Connection<PlayerInfo>.ConnectingPhase phase)
    {
        if (phase != Connection<PlayerInfo>.ConnectingPhase.Handshake) return;

        Player player = new()
        {
            Owner = client.ToString(),
            NetworkId = GenerateNetworkId(),
            Position = new Vector2(10, 5),
        };
        SpawnEntity(player);
    }

    public void OnMessageReceived(Message message, IPEndPoint source)
    {
        if (message is ObjectSyncMessage objectMessage)
        {
            if (TryGetNetworkEntity(objectMessage.ObjectId, out NetworkEntity? entity))
            {
                entity.HandleMessage(objectMessage);
            }
            else
            {
                if (Game.IsServer)
                { throw new NotImplementedException(); }

                Game.Connection.Send(new ObjectControlMessage()
                {
                    ObjectId = objectMessage.ObjectId,
                    Kind = ObjectControlMessageKind.NotFound,
                });
                Debug.WriteLine($"[Net]: Object {objectMessage.ObjectId} not found ...");
            }

            return;
        }

        if (message is ObjectControlMessage objectControlMessage)
        {
            switch (objectControlMessage.Kind)
            {
                case ObjectControlMessageKind.Spawn:
                {
                    if (Game.IsServer) break;

                    NetworkEntity entity = GenerateNetworkEntity(objectControlMessage);

                    AddEntity(entity);

                    Debug.WriteLine($"[Net]: Spawning object {objectControlMessage.ObjectId} ...");

                    break;
                }
                case ObjectControlMessageKind.Destroy:
                {
                    if (Game.IsServer) break;

                    Debug.WriteLine($"[Net]: Destroying object {objectControlMessage.ObjectId} ...");

                    for (int i = NetworkEntities.Count - 1; i >= 0; i--)
                    {
                        NetworkEntity? entity = NetworkEntities[i];
                        if (entity != null && entity.NetworkId == objectControlMessage.ObjectId)
                        { RemoveEntity(entity); }
                    }

                    break;
                }
                case ObjectControlMessageKind.NotFound:
                {
                    if (!Game.IsServer) break;

                    if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? entity))
                    {
                        Debug.WriteLine($"[Net]: Sending object info for {objectControlMessage.ObjectId} to {source} ...");

                        byte[] details = Utils.Serialize(entity.NetworkSerialize);
                        Game.Connection.SendTo(new ObjectControlMessage()
                        {
                            Kind = ObjectControlMessageKind.Info,
                            ObjectId = entity.NetworkId,
                            Details = details,
                            EntityPrototype = entity.Prototype,
                        }, source);
                    }
                    else
                    {
                        Debug.WriteLine($"[Net]: Sending object info for {objectControlMessage.ObjectId}: destroyed to {source} ...");

                        Game.Connection.SendTo(new ObjectControlMessage()
                        {
                            Kind = ObjectControlMessageKind.Destroy,
                            ObjectId = objectControlMessage.ObjectId,
                        }, source);
                    }

                    break;
                }
                case ObjectControlMessageKind.Info:
                {
                    if (Game.IsServer) break;

                    Debug.WriteLine($"[Net]: Received object info for {objectControlMessage.ObjectId} ...");

                    if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? entity))
                    {
                        Utils.Deserialize(objectControlMessage.Details, entity.NetworkDeserialize);
                    }
                    else
                    {
                        entity = GenerateNetworkEntity(objectControlMessage);

                        AddEntity(entity);
                    }

                    break;
                }
                default:
                    break;
            }
            return;
        }

        if (message is RPCmessage rpcMessage)
        {
            if (Game.IsServer)
            {
                Game.Connection.Send(rpcMessage);
            }

            if (TryGetNetworkEntity(rpcMessage.ObjectId, out NetworkEntity? entity))
            {
                entity.HandleRPC(rpcMessage);
            }
            else
            {
                if (Game.IsServer)
                { throw new NotImplementedException(); }

                Game.Connection.Send(new ObjectControlMessage()
                {
                    ObjectId = rpcMessage.ObjectId,
                    Kind = ObjectControlMessageKind.NotFound,
                });
                Debug.WriteLine($"[Net]: Object {rpcMessage.ObjectId} not found ...");
            }
            return;
        }
    }
}