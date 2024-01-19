using YeahGame.Messages;

namespace YeahGame;

public class Player : NetworkEntity
{
    const float Speed = 10;
    const float UsernameHoverDistance = 5f;
    const float NetPositionThreshold = .5f;
    const float NetPositionMaxSleep = 3f;
    const float ReloadTime = .5f;

    public bool IsLocalOwned => Game.Connection.LocalEndPoint?.ToString() == Owner;
    public override EntityPrototype Prototype => EntityPrototype.Player;

    public float HP = 1;
    public string? Owner;
    float LastShot = Time.Now;

    Vector2 NetPosition;
    float LastNetPositionTime;

    public override void Update()
    {
        if (IsLocalOwned)
        {
            if (Keyboard.IsKeyPressed('W')) Position.Y -= Speed * 0.5f * Time.Delta;
            if (Keyboard.IsKeyPressed('S')) Position.Y += Speed * 0.5f * Time.Delta;
            if (Keyboard.IsKeyPressed('A')) Position.X -= Speed * Time.Delta;
            if (Keyboard.IsKeyPressed('D')) Position.X += Speed * Time.Delta;

            Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
            Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);

            if (Mouse.IsPressed(MouseButton.Left) &&
                Time.Now - LastShot >= ReloadTime)
            {
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

                    Game.Singleton.GameScene.SpawnEntity(new Projectile(this)
                    {
                        Position = Position,
                        Velocity = velocity,
                        SpawnedAt = Time.Now
                    });
                }

                LastShot = Time.Now;
            }

            SyncUp();
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
            { DoesExist = false; }
        }
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;
        Game.Renderer[Position] = new ConsoleChar('○', IsLocalOwned ? CharColor.BrightMagenta : CharColor.White);

        if (Owner is not null &&
            Vector2.Distance(Position, Mouse.RecordedConsolePosition) < UsernameHoverDistance &&
            Game.Connection.PlayerInfos.TryGetValue(Owner, out (PlayerInfo Info, bool IsServer) info))
        {
            Game.Renderer.Text(Position.Round() + new Vector2Int(0, 1), info.Info.Username);
        }
    }

    #region Networking

    protected override void SyncUp(BinaryWriter writer)
    {
        if (Vector2.DistanceSquared(NetPosition, Position) >= NetPositionThreshold * NetPositionThreshold ||
            Time.Now - LastNetPositionTime > NetPositionMaxSleep)
        {
            LastNetPositionTime = Time.Now;
            NetPosition = Position;
            writer.Write(Position);
        }
    }

    public override void SyncDown(ObjectSyncMessage message, System.Net.IPEndPoint source)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        Position = reader.ReadVector2();

        if (Game.Connection.IsServer)
        { Game.Connection.SendExpect(message, source); }
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

            Game.Singleton.GameScene.SpawnEntity(new Projectile(this)
            {
                Position = projectilePosition,
                Velocity = velocity,
                SpawnedAt = Time.Now
            });
        }
        else if (message.RPCId == 2)
        {
            Damage(reader.ReadSingle());
        }

        if (Game.Connection.IsServer)
        { Game.Connection.Send(message); }
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
