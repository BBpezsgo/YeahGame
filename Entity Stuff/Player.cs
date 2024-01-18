using YeahGame.Messages;

namespace YeahGame;

public class Player : NetworkEntity
{
    const float Speed = 10;

    public float HP = 1;
    public string? Owner;

    float LastShot = Time.Now;
    Vector2 NetPosition;
    const float NetPositionThreshold = .5f;

    public override EntityPrototype Prototype => EntityPrototype.Player;

    public override void Update()
    {
        if (Game.Singleton.GameScene.TryGetLocalPlayer(out Player? localPlayer) &&
            localPlayer == this)
        {
            // Movement
            if (Keyboard.IsKeyPressed('W')) Position.Y -= Speed * 0.5f * Time.Delta;
            if (Keyboard.IsKeyPressed('S')) Position.Y += Speed * 0.5f * Time.Delta;
            if (Keyboard.IsKeyPressed('A')) Position.X -= Speed * Time.Delta;
            if (Keyboard.IsKeyPressed('D')) Position.X += Speed * Time.Delta;

            // Keep inside the world borders
            Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
            Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);

            // Shooting
            if (Mouse.IsPressed(MouseButton.Left) &&
                Time.Now - LastShot > 0.5f)
            {
                // Calculate projectile direction
                Vector2 velocity = Mouse.RecordedConsolePosition - Position;
                velocity *= new Vector2(1f, 2f);
                velocity = Vector2.Normalize(velocity);

                Game.Connection.Send(new RPCMessage()
                {
                    ObjectId = NetworkId,
                    RPCId = 1,
                    Details = Utils.Serialize(writer =>
                    {
                        writer.Write(Position);
                        writer.Write(velocity);
                    })
                });

                if (Game.IsServer)
                {
                    velocity *= Projectile.Speed;
                    velocity *= new Vector2(1, 0.5f);

                    Projectile newProjectile = new(this)
                    {
                        Position = Position,
                        Velocity = velocity,
                        SpawnedAt = Time.Now
                    };
                    Game.Singleton.GameScene.AddEntity(newProjectile);

                }

                LastShot = Time.Now;
            }

            // Network sync
            Sync();
        }
    }

    public void Damage(float amount)
    {
        if (Game.IsServer)
        {
            Game.Connection.Send(new RPCMessage()
            {
                ObjectId = NetworkId,
                RPCId = 2,
                Details = Utils.Serialize(writer =>
                {
                    writer.Write(amount);
                })
            });

            HP -= amount;
            if (HP <= 0)
            {
                DoesExist = false;
                // Menu
            }
        }
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;
        Game.Renderer[Position] = (ConsoleChar)'○';

        if (Owner is not null && Game.Connection.LocalAddress?.ToString() != Owner && Game.Connection.PlayerInfos.TryGetValue(Owner, out (PlayerInfo Info, bool IsServer) info))
        {
            Game.Renderer.Text(Position.Round() + new Vector2Int(0, 1), info.Info.Username);
        }
    }

    #region Networking

    void Sync()
    {
        if (!Game.Singleton.GameScene.ShouldSync) return;

        if (Vector2.DistanceSquared(NetPosition, Position) >= NetPositionThreshold * NetPositionThreshold)
        {
            NetPosition = Position;
            SendSyncMessage(Utils.Serialize(writer =>
            {
                writer.Write(Position);
            }));
        }
    }

    public override void HandleMessage(ObjectSyncMessage message)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        Position = reader.ReadVector2();
    }

    public override void HandleRPC(RPCMessage message)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        if (message.RPCId == 1)
        {
            Vector2 projectilePosition = default;
            projectilePosition = reader.ReadVector2();
            Vector2 velocity = default;
            velocity = reader.ReadVector2();
            velocity *= Projectile.Speed;
            velocity *= new Vector2(1, 0.5f);

            Projectile newProjectile = new(this)
            {
                Position = projectilePosition,
                Velocity = velocity,
                SpawnedAt = Time.Now
            };
            Game.Singleton.GameScene.AddEntity(newProjectile);
        }
        else if (message.RPCId == 2)
        {
            Damage(reader.ReadSingle());
        }
    }

    public override void NetworkSerialize(BinaryWriter writer)
    {
        writer.Write(Owner!);
        writer.Write(Position);
    }

    public override void NetworkDeserialize(BinaryReader reader)
    {
        Owner = reader.ReadString();
        Position = reader.ReadVector2();
    }

    #endregion
}
