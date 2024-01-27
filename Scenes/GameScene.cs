using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Win32.Common;
using YeahGame.Messages;

namespace YeahGame;

public class GameScene : Scene
{
    const float PlayerInfoSyncInterval = 15f;
    const float RespawnTime = 5f;
    const float SyncInterval = .1f;


    public override string Name => "Game";


    public readonly bool[]? Map;
    public readonly int MapWidth;
    public readonly int MapHeight;

    public bool ShouldSync => Time.Now - LastNetworkSync >= SyncInterval;

    public Chat Chat = new();

    public IReadOnlyList<Player?> Players => _players;
    public IReadOnlyList<Projectile?> Projectiles => _projectiles;
    public IReadOnlyList<Item?> Items => _items;
    public IReadOnlyList<NetworkEntity?> NetworkEntities => _networkEntities;
    public IReadOnlyList<Entity> Entities => _entities;


    float LastPlayerInfoSync;

    float LastNetworkSync;

    readonly Dictionary<string, float> _respawnTimers = new();
    float LocalRespawnTimer;

    int NetworkIdCounter = 1;

    readonly WeakList<Player> _players = new();
    readonly WeakList<Projectile> _projectiles = new();
    readonly WeakList<Item> _items = new();
    readonly WeakList<NetworkEntity> _networkEntities = new();
    readonly List<Entity> _entities = new();


    public GameScene()
    {
        // Map = Utils.LoadMap(File.ReadAllText("map.txt"), out MapWidth);
        // MapHeight = Map.Length / MapWidth;
    }

    public override void Load()
    {
        base.Load();

        if (Game.IsServer || Game.IsOffline)
        {
            SpawnEntity(new Player()
            {
                Owner = Game.Connection.LocalEndPoint,
                NetworkId = GenerateNetworkId(),
                Position = GetSpawnPoint(),
            });

            const int SpawnItems = 10;

            for (int i = 0; i < SpawnItems; i++)
            {
                Vector2 position = Random.Shared.NextVector2(new Vector2(10, 10), new Vector2(50, 50));
                SpawnEntity(new Item()
                {
                    NetworkId = GenerateNetworkId(),
                    Position = position,
                    Type = ItemType.Item1,
                });
            }

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

        // if (Mouse.IsDown(MouseButton.Left))
        // {
        //     AddEntity(new FlashEffect()
        //     {
        //         Position = Mouse.RecordedConsolePosition,
        //     });
        // }

        Chat.Render();

        if (Keyboard.IsKeyHold('\t'))
        {
            IReadOnlyDictionary<IPEndPoint, ConnectionUserInfo<PlayerInfo>> infos = Game.Connection.UserInfos;

            bool selfContained = false;
            if (!Game.Connection.IsServer)
            {
                foreach (KeyValuePair<IPEndPoint, ConnectionUserInfo<PlayerInfo>> item in infos)
                {
                    if (item.Key.Equals(Game.Connection.LocalEndPoint))
                    {
                        selfContained = true;
                        break;
                    }
                }
            }

            SmallRect box = Layout.Center(new SmallSize(50, infos.Count + 3 + (selfContained ? 0 : 1)), new SmallRect(default, Game.Renderer.Size));

            Game.Renderer.Clear(box);
            Game.Renderer.Box(box, CharColor.Black, CharColor.White, in Ascii.BoxSides);
            box = box.Margin(2);

            int y = 0;

            if (!selfContained)
            { Game.Renderer.Text(box.Left, box.Top + y++, $"{Game.Connection.LocalUserInfo?.Username} ({Game.Connection.LocalEndPoint}){(Game.Connection.IsServer ? " (Server)" : string.Empty)}", CharColor.BrightMagenta); }

            foreach (KeyValuePair<IPEndPoint, ConnectionUserInfo<PlayerInfo>> item in infos)
            {
                StringBuilder builder = new();
                byte color = CharColor.White;

                if (item.Key.Equals(Game.Connection.LocalEndPoint))
                { color = CharColor.BrightMagenta; }

                if (item.Value.Info != null)
                { builder.Append($"{item.Value.Info.Username} "); }

                builder.Append($"({item.Key})");

                if (item.Value.IsServer)
                { builder.Append(" (Server)"); }

                if (item.Value.IsRefreshing)
                {
                    builder.Append(' ');
                    int loadingCharIndex = (int)(Time.Now * 8) % Ascii.Loading.Length;
                    builder.Append(Ascii.Loading[loadingCharIndex]);
                }

                Game.Renderer.Text(box.Left, box.Top + y++, builder.ToString(), color);
            }
        }
        else if (!TryGetLocalPlayer(out _) && Game.Connection.IsConnected)
        {
            SmallRect box = Layout.Center(new SmallSize(50, 7), new SmallRect(default, Game.Renderer.Size));

            Game.Renderer.Fill(box, CharColor.Black, CharColor.Black, ' ');
            Game.Renderer.Box(box, CharColor.Black, CharColor.White, in Ascii.BoxSides);

            ReadOnlySpan<char> text1 = "YOU DIED";
            Game.Renderer.Text(box.Left + Layout.Center(text1, box.Width - 2), box.Top + 2, text1, CharColor.BrightRed);

            ReadOnlySpan<char> text2 = $"Respawn in {Math.Max(0f, RespawnTime - (Time.Now - LocalRespawnTimer)):0.0} sec ...";
            Game.Renderer.Text(box.Left + Layout.Center(text2, box.Width - 2), box.Top + 4, text2, CharColor.White);
        }
        else if (Game.Connection.LocalUserInfo is not null)
        {
            PlayerInfo info = Game.Connection.LocalUserInfo;
            int y = Game.Renderer.Height - 2;
            for (int i = 0; i < info.Items.Value.Count; i++)
            {
                ItemType item = info.Items.Value[i];
                Game.Renderer.Text(Game.Renderer.Width - 10, y--, item.ToString());
            }
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

        if (Game.Connection.IsConnected &&
            Keyboard.IsKeyDown(Win32.LowLevel.VirtualKeyCode.END))
        {
            Game.Connection.Send(new BruhMessage()
            {
                ShouldAck = true,
            });
        }

        if (Game.IsServer)
        {
            foreach (IPEndPoint client in Game.Connection.Connections)
            {
                string clientString = client.ToString();
                if (TryGetPlayer(client, out _))
                {
                    _respawnTimers.Remove(clientString);
                }
                else if (!_respawnTimers.TryAdd(clientString, Time.Now) &&
                         Time.Now - _respawnTimers[clientString] >= RespawnTime)
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
                        Owner = Game.Connection.LocalEndPoint,
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

    #region Entity Management

    public bool DestroyEntity(Entity entity)
    {
        if (Game.IsServer && entity is NetworkEntity networkEntity)
        {
            Game.Connection.Send(new ObjectControlMessage()
            {
                Kind = ObjectControlMessageKind.Destroy,
                ObjectId = networkEntity.NetworkId,
                ShouldAck = true,
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

        if (entity is Item item)
        { _items.Remove(item); }

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

        if (entity is Item item)
        { _items.Add(item); }

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
        => TryGetPlayer(Game.Connection.LocalEndPoint, out player);

    public bool TryGetPlayer(IPEndPoint? owner, [NotNullWhen(true)] out Player? player)
    {
        player = null;

        if (owner is null) return false;

        for (int i = 0; i < Players.Count; i++)
        {
            Player? _player = Players[i];
            if (_player != null && owner.Equals(_player.Owner))
            {
                player = _player;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Networking

    public void OnDisconnectedFromServer()
    {
        Game.Singleton.MenuScene.ExitReason = "Disconnected form server";
    }

    public void OnConnectedToServer(ConnectingPhase phase)
    {
        if (phase != ConnectingPhase.Handshake) return;
    }

    public void OnClientDisconnected(IPEndPoint client)
    {
        for (int i = Players.Count - 1; i >= 0; i--)
        {
            Player? player = Players[i];
            if (player != null && client.Equals(player.Owner))
            { DestroyEntity(player); }
        }

        Chat.SendSystem($"Client {client} disconnected");
    }

    public void OnClientConnected(IPEndPoint client, ConnectingPhase phase)
    {
        if (phase != ConnectingPhase.Handshake) return;
        if (!Game.Connection.IsServer) return;

        Game.Connection.SendTo(new InfoRequestMessage()
        {
            From = null,
            FromServer = false,
        }, client);

        foreach (NetworkEntity? entity in _networkEntities)
        {
            if (entity is null) continue;
            Debug.WriteLine($"[NetEntity]: Sending object info for {entity.NetworkId} to {client} ...");

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
            Owner = client,
            NetworkId = GenerateNetworkId(),
            Position = GetSpawnPoint(),
        });

        Chat.SendSystem($"Client {client} connected");
    }

    public void OnMessageReceived(Message message, IPEndPoint source)
    {
        if (message is ChatMessage chatMessage)
        {
            Chat.Feed(chatMessage, source);

            if (Game.IsServer)
            { Game.Connection.Send(message); }

            return;
        }

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
                    Debug.WriteLine($"[NetEntity]: Client {source} sent obj sync that doesn't exists ...");
                }
                else
                {
                    Game.Connection.Send(new ObjectControlMessage()
                    {
                        ObjectId = objectMessage.ObjectId,
                        Kind = ObjectControlMessageKind.NotFound,
                    });
                    Debug.WriteLine($"[NetEntity]: Object {objectMessage.ObjectId} not found ...");
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
                    Debug.WriteLine($"[NetEntity]: Spawning object {objectControlMessage.ObjectId} ...");

                    return;
                }
                case ObjectControlMessageKind.Destroy:
                {
                    if (Game.IsServer) return;

                    Debug.WriteLine($"[NetEntity]: Destroying object {objectControlMessage.ObjectId} ...");

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
                        Debug.WriteLine($"[NetEntity]: Sending object info for {objectControlMessage.ObjectId} to {source} ...");

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
                        Debug.WriteLine($"[NetEntity]: Sending object info for {objectControlMessage.ObjectId}: destroyed to {source} ...");

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

                    Debug.WriteLine($"[NetEntity]: Received object info for {objectControlMessage.ObjectId} ...");

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
                Debug.WriteLine($"[NetEntity]: Client {source} looking for object that doesn't exists ...");
            }
            else
            {
                Game.Connection.Send(new ObjectControlMessage()
                {
                    ObjectId = rpcMessage.ObjectId,
                    Kind = ObjectControlMessageKind.NotFound,
                });
                Debug.WriteLine($"[NetEntity]: Object {rpcMessage.ObjectId} not found ...");
            }
            return;
        }
    }

    public int GenerateNetworkId()
    {
        int id = NetworkIdCounter++;
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
            EntityPrototype.Item => new Item(),
            _ => throw new NotImplementedException(),
        };

        result.NetworkId = networkId;

        Utils.Deserialize(details, result.NetworkDeserialize);

        return result;
    }

    #endregion

    #region Utils

    public bool MouseBlockedByUI(Coord point)
    {
        if (Keyboard.IsKeyHold('\t'))
        {
            return true;
        }

        if (!TryGetLocalPlayer(out _) && Game.Connection.IsConnected)
        {
            SmallRect box = Layout.Center(new SmallSize(50, 7), new SmallRect(default, Game.Renderer.Size));
            if (box.Contains(point)) return true;
        }

        // if (Game.Connection.LocalUserInfo is not null)
        // {
        //     PlayerInfo info = Game.Connection.LocalUserInfo;
        //     int y = Game.Renderer.Height - 2;
        //     for (int i = 0; i < info.Items.Value.Count; i++)
        //     {
        //         ItemType item = info.Items.Value[i];
        //         Game.Renderer.Text(Game.Renderer.Width - 10, y--, item.ToString());
        //     }
        // }

        if (Chat.Rect.Contains(point)) return true;

        return false;
    }

    Vector2 GetSpawnPoint()
    {
        return Random.Shared.NextVector2(new Vector2(0f, 0f), new Vector2(10f, 10f));
    }

    public bool OnItemPickedUp(Item item, string owner)
        => OnItemPickedUp(item, IPEndPoint.Parse(owner));
    public bool OnItemPickedUp(Item item, IPEndPoint owner)
    {
        if (!Game.IsServer && !Game.IsOffline) return false;

        if (Game.Connection.TryGetUserInfo(owner, out ConnectionUserInfo<PlayerInfo> userInfo) &&
            userInfo.Info is not null)
        {
            userInfo.Info.Items.Value.Add(item.Type);
            userInfo.Info.Items.WasChanged = true;

            if (Game.IsServer)
            {
                Game.Connection.Send(new InfoResponseMessage()
                {
                    IsServer = owner.Equals(Game.Connection.LocalEndPoint),
                    Source = owner,
                    Details = Utils.Serialize(userInfo.Info),
                });
            }
            return true;
        }

        return false;
    }

    #endregion
}
