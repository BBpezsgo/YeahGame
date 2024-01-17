using YeahGame.Messages;

namespace YeahGame;

public class Player : NetworkEntity
{
    const float Speed = 10;

    public float HP = 1;

    float LastShot = Time.Now;
    public string? Owner;

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

                Game.Connection.Send(new RPCmessage()
                {
                    ObjectId = NetworkId,
                    RPCId = 1,
                    Details = Utils.Serialize(writer =>
                    {
                        writer.Write(Position.X);
                        writer.Write(Position.Y);
                        writer.Write(velocity.X);
                        writer.Write(velocity.Y);
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
            Game.Connection.Send(new RPCmessage()
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
    }

    #region Networking

    void Sync()
    {
        if (!Game.Singleton.GameScene.ShouldSync) return;

        SendSyncMessage(Utils.Serialize(writer =>
        {
            writer.Write(Position.X);
            writer.Write(Position.Y);
        }));
    }

    public override void HandleMessage(ObjectSyncMessage message)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        Position.X = reader.ReadSingle();
        Position.Y = reader.ReadSingle();
    }

    public override void HandleRPC(RPCmessage message)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        if (message.RPCId == 1)
        {
            Vector2 projectilePosition = default;
            projectilePosition.X = reader.ReadSingle();
            projectilePosition.Y = reader.ReadSingle();
            Vector2 velocity = default;
            velocity.X = reader.ReadSingle();
            velocity.Y = reader.ReadSingle();
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
    }

    public override void NetworkDeserialize(BinaryReader reader)
    {
        Owner = reader.ReadString();
    }

    #endregion
}
