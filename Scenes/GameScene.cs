﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public class GameScene : Scene
{
    const float PlayerInfoSyncInterval = 15f;
    const float RespawnTime = 5f;
    const float SyncInterval = .1f;


    public override string Name => "Game";


    float LastPlayerInfoSync;

    float LastNetworkSync;

    readonly Dictionary<string, float> _respawnTimers = new();
    float LocalRespawnTimer;

    int NetworkIdCounter = 1;

    readonly WeakList<Player> _players = new();
    readonly WeakList<Projectile> _projectiles = new();
    readonly WeakList<NetworkEntity> _networkEntities = new();
    readonly List<Entity> _entities = new();


    public readonly bool[]? Map;
    public readonly int MapWidth;
    public readonly int MapHeight;

    public bool ShouldSync => Time.Now - LastNetworkSync >= SyncInterval;

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
                Owner = Game.Connection.LocalEndPoint!.ToString(),
                NetworkId = GenerateNetworkId(),
                Position = GetSpawnPoint(),
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

            bool selfContained = false;
            if (!Game.Connection.IsServer)
            {
                foreach (KeyValuePair<string, (PlayerInfo Info, bool IsServer)> item in infos)
                {
                    if (item.Key == Game.Connection.LocalEndPoint?.ToString())
                    {
                        selfContained = true;
                        break;
                    }
                }
            }

            SmallRect box = Layout.Center(new Coord(50, infos.Count + 3 + (selfContained ? 0 : 1)), new SmallRect(default, Game.Renderer.Rect));

            Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);

            int i = 2;

            if (!selfContained)
            { Game.Renderer.Text(box.Left + 2, box.Top + i++, $"{Game.Connection.LocalUserInfo?.Username} ({Game.Connection.LocalEndPoint}){(Game.Connection.IsServer ? " (Server)" : string.Empty)}", CharColor.BrightMagenta); }

            foreach (KeyValuePair<string, (PlayerInfo Info, bool IsServer)> item in infos)
            {
                if (item.Key == Game.Connection.LocalEndPoint?.ToString())
                { Game.Renderer.Text(box.Left + 2, box.Top + i++, $"{item.Value.Info.Username} ({item.Key}){(item.Value.IsServer ? " (Server)" : string.Empty)}", CharColor.BrightMagenta); }
                else
                { Game.Renderer.Text(box.Left + 2, box.Top + i++, $"{item.Value.Info.Username} ({item.Key}){(item.Value.IsServer ? " (Server)" : string.Empty)}", CharColor.White); }
            }
        }

        if (!TryGetLocalPlayer(out _))
        {
            SmallRect box = Layout.Center(new Coord(50, 7), new SmallRect(default, Game.Renderer.Rect));

            Game.Renderer.Fill(box, CharColor.Black, CharColor.Black, ' ');
            Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);

            ReadOnlySpan<char> text1 = "YOU DIED";
            Game.Renderer.Text(box.Left + Layout.Center(box.Width - 2, text1), box.Top + 2, text1, CharColor.BrightRed);

            ReadOnlySpan<char> text2 = $"Respawn in {Math.Max(0f, RespawnTime - (Time.Now - LocalRespawnTimer)):0.0} sec ...";
            Game.Renderer.Text(box.Left + Layout.Center(box.Width - 2, text2), box.Top + 4, text2, CharColor.White);
        }
    }

    public override void Tick()
    {
        if (Game.Connection.IsConnected && Time.Now - LastPlayerInfoSync >= PlayerInfoSyncInterval)
        {
            LastPlayerInfoSync = Time.Now;
            Game.Connection.Send(new InfoRequestMessage()
            {
                From = null,
                FromServer = false,
            });
            if (!Game.IsServer)
            {
                Game.Connection.Send(new InfoRequestMessage()
                {
                    From = null,
                    FromServer = true,
                });
            }
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
                         Time.Now - _respawnTimers[client] >= RespawnTime)
                {
                    SpawnEntity(new Player()
                    {
                        Owner = client,
                        NetworkId = GenerateNetworkId(),
                        Position = GetSpawnPoint(),
                    });
                }
            }

            {
                string local = Game.Connection.LocalEndPoint!.ToString();
                if (TryGetLocalPlayer(out _))
                {
                    _respawnTimers.Remove(local);
                }
                else if (!_respawnTimers.TryAdd(local, Time.Now) &&
                         Time.Now - _respawnTimers[local] >= RespawnTime)
                {
                    SpawnEntity(new Player()
                    {
                        Owner = local,
                        NetworkId = GenerateNetworkId(),
                        Position = GetSpawnPoint(),
                    });
                }
            }
        }

        if (!TryGetLocalPlayer(out _))
        {
            if (LocalRespawnTimer == 0f)
            {
                LocalRespawnTimer = Time.Now;
            }
        }
        else
        {
            LocalRespawnTimer = 0f;
        }

        {
            int i = -1;
            while (++i < _entities.Count)
            {
                Entity entity = _entities[i];
                if (entity.DoesExist) entity.Update();
                else DestroyEntity(entity);
            }
        }

        if (ShouldSync)
        { LastNetworkSync = Time.Now; }
    }

    public int GenerateNetworkId()
    {
        int id = NetworkIdCounter++;
        while (TryGetNetworkEntity(id, out _))
        { id++; }
        return id;
    }

    Vector2 GetSpawnPoint()
    {
        return Random.Shared.NextVector2(new Vector2(0f, 0f), new Vector2(10f, 10f));
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

        if (entity is NetworkEntity networkEntity)
        { _networkEntities.Remove(networkEntity); }

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
        => TryGetPlayer(Game.Connection.LocalEndPoint?.ToString(), out player);

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

    public void OnClientConnected(IPEndPoint client, Connection.ConnectingPhase phase)
    {
        if (phase != Connection.ConnectingPhase.Handshake) return;
        if (!Game.Connection.IsServer) return;

        Game.Connection.SendTo(new InfoRequestMessage()
        {
            From = null,
            FromServer = false,
        }, client);

        foreach (NetworkEntity? entity in _networkEntities)
        {
            if (entity is null) continue;
            Debug.WriteLine($"[Net]: Sending object info for {entity.NetworkId} to {client} ...");

            byte[] details = Utils.Serialize(entity.NetworkSerialize);
            Game.Connection.SendTo(new ObjectControlMessage()
            {
                Kind = ObjectControlMessageKind.Info,
                ObjectId = entity.NetworkId,
                Details = details,
                EntityPrototype = entity.Prototype,
            }, client);
        }

        SpawnEntity(new Player()
        {
            Owner = client.ToString(),
            NetworkId = GenerateNetworkId(),
            Position = GetSpawnPoint(),
        });
    }

    public void OnMessageReceived(Message message, IPEndPoint source)
    {
        if (message is ObjectSyncMessage objectMessage)
        {
            if (TryGetNetworkEntity(objectMessage.ObjectId, out NetworkEntity? entity))
            {
                entity.SyncDown(objectMessage, source);
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
                    if (Game.IsServer) return;

                    if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? existing))
                    {
                        if (existing.Prototype == objectControlMessage.EntityPrototype)
                        {
                            Utils.Deserialize(objectControlMessage.Details, existing.NetworkDeserialize);
                            return;
                        }

                        RemoveEntity(existing);
                    }

                    AddEntity(GenerateNetworkEntity(objectControlMessage));
                    Debug.WriteLine($"[Net]: Spawning object {objectControlMessage.ObjectId} ...");

                    return;
                }
                case ObjectControlMessageKind.Destroy:
                {
                    if (Game.IsServer) return;

                    Debug.WriteLine($"[Net]: Destroying object {objectControlMessage.ObjectId} ...");

                    for (int i = NetworkEntities.Count - 1; i >= 0; i--)
                    {
                        NetworkEntity? entity = NetworkEntities[i];
                        if (entity != null && entity.NetworkId == objectControlMessage.ObjectId)
                        { RemoveEntity(entity); }
                    }

                    return;
                }
                case ObjectControlMessageKind.NotFound:
                {
                    if (!Game.IsServer) return;

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

                    return;
                }
                case ObjectControlMessageKind.Info:
                {
                    if (Game.IsServer) return;

                    Debug.WriteLine($"[Net]: Received object info for {objectControlMessage.ObjectId} ...");

                    if (TryGetNetworkEntity(objectControlMessage.ObjectId, out NetworkEntity? entity))
                    {
                        if (entity.Prototype == objectControlMessage.EntityPrototype)
                        {
                            Utils.Deserialize(objectControlMessage.Details, entity.NetworkDeserialize);
                            return;
                        }

                        RemoveEntity(entity);
                    }

                    entity = GenerateNetworkEntity(objectControlMessage);
                    AddEntity(entity);

                    return;
                }
                default: return;
            }
        }

        if (message is RPCMessage rpcMessage)
        {
            if (Game.IsServer)
            { Game.Connection.Send(rpcMessage); }

            if (TryGetNetworkEntity(rpcMessage.ObjectId, out NetworkEntity? entity))
            {
                entity.HandleRPC(rpcMessage);
                return;
            }

            if (Game.IsServer)
            {
                Game.Connection.SendTo(new ObjectControlMessage()
                {
                    ObjectId = rpcMessage.ObjectId,
                    Kind = ObjectControlMessageKind.Destroy,
                }, source);
            }
            else
            {
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