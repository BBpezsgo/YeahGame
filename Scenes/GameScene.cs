using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public class GameScene : Scene
{
    public override string Name => "Game";

    float lastPlayerInfoSync;
    public bool ShouldSyncPlayerInfo => Time.Now - lastPlayerInfoSync > 5f;

    float lastNetworkSync;
    public bool ShouldSync => Time.Now - lastNetworkSync > .1f;

    readonly WeakList<Player> _players = new();
    readonly WeakList<Projectile> _projectiles = new();
    readonly WeakList<NetworkEntity> _networkEntities = new();
    readonly List<Entity> _entities = new();

    const float RespawnTime = 5f;

    readonly Dictionary<string, float> _respawnTimers = new();
    float _localRespawnTimer;

    public readonly bool[]? Map;
    public readonly int MapWidth;
    public readonly int MapHeight;

    public IReadOnlyList<Player?> Players => _players;
    public IReadOnlyList<Projectile?> Projectiles => _projectiles;
    public IReadOnlyList<NetworkEntity?> NetworkEntities => _networkEntities;
    public IReadOnlyList<Entity> Entities => _entities;

    public GameScene()
    {
        // Map = Utils.LoadMap(File.ReadAllText("map.txt"), out MapWidth);
        // MapHeight = Map.Length / MapWidth;
    }

    public override void Load()
    {
        base.Load();

        if (Game.IsServer)
        {
            SpawnEntity(new Player()
            {
                Owner = Game.Connection.LocalAddress!.ToString(),
                NetworkId = GenerateNetworkId(),
                Position = new Vector2(10, 5),
            });

            // for (int i = 0; i < 5; i++)
            // {
            //     AddEntity(new Tester()
            //     {
            //         NetworkId = GenerateNetworkId(),
            //         Position = Random.Shared.NextVector2(new Vector2(0f, 0f), new Vector2(MapWidth, MapHeight)),
            //     });
            // }
        }
    }

    public override void Unload()
    {
        base.Unload();
    }

    public override void Render()
    {
        if (Map is not null)
        {
            for (int i = 0; i < Map.Length; i++)
            {
                int x = i % MapWidth;
                int y = i / MapWidth;
                if (Game.Renderer.IsVisible(x, y) && Map[i])
                { Game.Renderer[x, y] = (ConsoleChar)Ascii.Blocks.Full; }
            }
        }

        for (int i = 0; i < _entities.Count; i++)
        {
            Entity entity = _entities[i];
            if (entity.DoesExist) entity.Render();
        }

        if (Keyboard.IsKeyHold('\t'))
        {
            IReadOnlyDictionary<string, (PlayerInfo Info, bool IsServer)> infos = Game.Connection.PlayerInfos;

            SmallRect box = Layout.Center(new Coord(50, infos.Count + 4), new SmallRect(default, Game.Renderer.Rect));

            Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);

            int i = 2;

            if (Game.Connection.IsServer)
            {
                Game.Renderer.Text(box.Left + 2, box.Top + i++, $"{Game.Connection.LocalUserInfo?.Username} ({Game.Connection.LocalAddress}) (Server)", CharColor.BrightMagenta);
            }

            foreach (KeyValuePair<string, (PlayerInfo Info, bool IsServer)> item in infos)
            {
                if (item.Key == Game.Connection.LocalAddress?.ToString())
                {
                    Game.Renderer.Text(box.Left + 2, box.Top + i++, $"{item.Value.Info.Username} ({item.Key}){(item.Value.IsServer ? " (Server)" : string.Empty)}", CharColor.BrightMagenta);
                }
                else
                {
                    Game.Renderer.Text(box.Left + 2, box.Top + i++, $"{item.Value.Info.Username} ({item.Key}){(item.Value.IsServer ? " (Server)" : string.Empty)}", CharColor.White);
                }
            }
        }

        if (!TryGetLocalPlayer(out _))
        {
            SmallRect box = Layout.Center(new Coord(50, 7), new SmallRect(default, Game.Renderer.Rect));

            Game.Renderer.Fill(box, CharColor.Black, CharColor.Black, ' ');
            Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);
            
            ReadOnlySpan<char> text1 = "YOU DIED";
            Game.Renderer.Text(box.Left + Layout.Center(box.Width - 2, text1), box.Top + 2, text1, CharColor.BrightRed);

            ReadOnlySpan<char> text2 = $"Respawn in {RespawnTime - (Time.Now - _localRespawnTimer):0.0} sec ...";
            Game.Renderer.Text(box.Left + Layout.Center(box.Width - 2, text2), box.Top + 4, text2, CharColor.White);
        }
    }

    public override void Tick()
    {
        if (Game.Connection.IsConnected && ShouldSyncPlayerInfo)
        {
            lastPlayerInfoSync = Time.Now;
            Game.Connection.Send(new InfoRequestMessage()
            {
                From = null,
                FromServer = false,
            });
            Game.Connection.Send(new InfoRequestMessage()
            {
                From = null,
                FromServer = true,
            });
        }

        if (Game.IsServer)
        {
            foreach (string client in Game.Connection.Connections)
            {
                if (TryGetPlayer(client, out _))
                {
                    _respawnTimers.Remove(client);
                }
                else if (!_respawnTimers.TryAdd(client, Time.Now) &&
                         Time.Now - _respawnTimers[client] > RespawnTime)
                {
                    SpawnEntity(new Player()
                    {
                        Owner = client,
                        NetworkId = GenerateNetworkId(),
                        Position = new Vector2(10, 5),
                    });
                }
            }

            {
                string local = Game.Connection.LocalAddress!.ToString();
                if (TryGetLocalPlayer(out _))
                {
                    _respawnTimers.Remove(local);
                }
                else if (!_respawnTimers.TryAdd(local, Time.Now) &&
                         Time.Now - _respawnTimers[local] > RespawnTime)
                {
                    SpawnEntity(new Player()
                    {
                        Owner = local,
                        NetworkId = GenerateNetworkId(),
                        Position = new Vector2(10, 5),
                    });
                }
            }
        }

        if (!TryGetLocalPlayer(out _))
        {
            if (_localRespawnTimer == 0f)
            {
                _localRespawnTimer = Time.Now;
            }
        }
        else
        {
            _localRespawnTimer = 0f;
        }

        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            Entity entity = _entities[i];
            if (entity.DoesExist) entity.Update();
            else
            {
                DestroyEntity(entity);
            }
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
        => TryGetPlayer(Game.Connection.LocalAddress?.ToString(), out player);

    public bool TryGetPlayer(string? owner, [NotNullWhen(true)] out Player? player)
    {
        player = null;

        if (owner == null) return false;

        for (int i = 0; i < Players.Count; i++)
        {
            Player? _player = Players[i];
            if (_player != null && _player.Owner == owner)
            {
                player = _player;
                return true;
            }
        }

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
                {
                    Game.Connection.SendTo(new ObjectControlMessage()
                    {
                        ObjectId = objectMessage.ObjectId,
                        Kind = ObjectControlMessageKind.Destroy,
                    }, source);
                    Debug.WriteLine($"[Net]: Client {source} sent obj sync that doesnt exists ...");
                }
                else
                {
                    Game.Connection.Send(new ObjectControlMessage()
                    {
                        ObjectId = objectMessage.ObjectId,
                        Kind = ObjectControlMessageKind.NotFound,
                    });
                    Debug.WriteLine($"[Net]: Object {objectMessage.ObjectId} not found ...");
                }
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

        if (message is RPCMessage rpcMessage)
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