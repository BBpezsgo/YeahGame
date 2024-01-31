using System.Net;
using Win32.Gdi32;
using YeahGame.Messages;

namespace YeahGame;

public class Player : NetworkEntity, IDamageable
{
    const float Speed = 10;
    const float UsernameHoverDistance = 5f;
    const float NetPositionThreshold = .5f;
    const float NetPositionMaxSleep = 3f;
    const float ReloadTime = .5f;
    static readonly float InverseSqrt2 = 1f / MathF.Sqrt(2f);

    public bool IsLocalOwned => Game.Connection.LocalEndPoint?.Equals(Owner) ?? false;
    public override EntityPrototype Prototype => EntityPrototype.Player;

    public float HP = 1;
    public IPEndPoint? Owner;
    float LastShot = Time.Now;
    float GetPowerUpTime = 0;

    Vector2 NetPosition;
    float LastNetPositionTime;
    CapturedTouch? CapturedTouch;

    public byte Color => PlayerInfo is null ? CharColor.White : (byte)PlayerInfo.Color.Value;

    public UserDetails? PlayerInfo => Game.Connection.TryGetUserInfo(Owner, out ConnectionUserDetails info) ? info.Details : null;

    public Player()
    {
        IsSolid = true;
    }

    public override void Update()
    {
        if ((Game.IsServer || Game.IsOffline) && Owner is not null)
        {
            IReadOnlyList<Item?> items = Game.Singleton.GameScene.Items;
            for (int i = 0; i < items.Count; i++)
            {
                Item? item = items[i];
                if (item is null) continue;

                if (Vector2.Distance(item.Position, Position) < 2)
                {
                    if (Game.Singleton.GameScene.OnItemPickedUp(item, Owner))
                    { Game.Singleton.GameScene.DestroyEntity(item); }
                    continue;
                }
            }
        }

        if (!IsLocalOwned) return;

        if (!Game.Singleton.GameScene.Chat.IsChatting)
        {
            Vector2 velocity = default;

            velocity = Game.Singleton.Joystick.Input;

            if (Keyboard.IsKeyPressed('W')) velocity.Y = -1f;
            if (Keyboard.IsKeyPressed('S')) velocity.Y = +1f;
            if (Keyboard.IsKeyPressed('A')) velocity.X = -1f;
            if (Keyboard.IsKeyPressed('D')) velocity.X = +1f;

            if (velocity != default)
            {
                if (velocity.X != 0f && velocity.Y != 0f)
                {
                    velocity.X = (velocity.X < 0f) ? -InverseSqrt2 : +InverseSqrt2;
                    velocity.Y = (velocity.Y < 0f) ? -InverseSqrt2 : +InverseSqrt2;
                }
                velocity.Y *= .5f;
                Position += velocity * (Speed * Time.Delta);
            }

            if (Keyboard.IsKeyDown('E') &&
                Game.Connection.TryGetUserInfo(Owner, out ConnectionUserDetails info) &&
                info.Details is not null &&
                info.Details.Items.Value.Count > 0)
            {
                ItemType item = info.Details.Items.Value[0];
                info.Details.Items.Value.RemoveAt(0);
                info.Details.Items.WasChanged = true;
                switch (item)
                {
                    case ItemType.RapidFire:
                        GetPowerUpTime = Time.Now;
                        break;
                    case ItemType.SuicideBomber:
                        DoesExist = false;
                        for (int i = 0; i < Game.Singleton.GameScene.Entities.Count; i++)
                        {
                            Entity explodedEntity = Game.Singleton.GameScene.Entities[i];
                            if (explodedEntity is IDamageable &&
                                Vector2.Distance(Position, explodedEntity.Position) <= 5)
                            {
                                explodedEntity.DoesExist = false;
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

#if !SERVER
        Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
        Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);
#endif

        CapturedTouch ??= new CapturedTouch();
        CapturedTouch.Tick(p => !Game.Singleton.MouseBlockedByUI((Coord)p));

        bool shouldShoot = false;
        Vector2 shootPosition = default;

        if (Touch.IsTouchDevice)
        {
            shouldShoot = CapturedTouch.Has;
            shootPosition = CapturedTouch.Position;
        }
        else
        {
            shouldShoot = (
                !Mouse.WasUsed &&
                Mouse.IsPressed(MouseButton.Left) &&
                !Game.Singleton.MouseBlockedByUI(Mouse.RecordedConsolePosition)
            );
            shootPosition = Mouse.RecordedConsolePosition;
        }

        if (Time.Now - LastShot >= GetCurrentReloadTime() && shouldShoot)
        {
            Vector2 velocity = shootPosition - Position;
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

            if (Game.IsServer || Game.IsOffline)
            {
                Game.Singleton.GameScene.SpawnEntity(new Particles(ParticleConfigs.GetShoot(velocity), Utils.Random)
                {
                    Position = Position,
                });

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

    public void Damage(float amount)
    {
        if (!Game.IsServer && !Game.IsOffline) return;

        if (Game.IsServer)
        {
            Game.Connection.Send(new RPCMessage()
            {
                ObjectId = NetworkId,
                RPCId = 2,
                Details = Utils.Serialize(writer =>
                {
                    writer.Write(amount);
                }),
            });
        }

        HP -= amount;
        if (HP <= 0)
        { DoesExist = false; }
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;

        byte color = CharColor.White;

        UserDetails? info = PlayerInfo;

        if (info is not null)
        {
            color = (byte)info.Color.Value;

            if (Vector2.Distance(Position, Mouse.RecordedConsolePosition) < UsernameHoverDistance)
            {
                Game.Renderer.Text(Position + new Vector2(0, 1), info.Username.Value);
            }
        }

        Game.Renderer[Position] = new ConsoleChar('○', color);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        Game.Singleton.GameScene.SpawnEntity(new Particles(ParticleConfigs.GetDeath(CharColor.GetColor(Color)), Utils.Random)
        {
            Position = Position,
        });
    }

    float GetCurrentReloadTime()
    {
        if (Time.Now - GetPowerUpTime < 5)
        {
            return .1f;
        }

        return ReloadTime;
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

    public override void SyncDown(ObjectSyncMessage message, IPEndPoint source)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        Position = reader.ReadVector2();

        if (Game.IsServer)
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

            Game.Singleton.GameScene.SpawnEntity(new Particles(ParticleConfigs.GetShoot(velocity), Utils.Random)
            {
                Position = Position,
            });

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

        if (Game.IsServer)
        { Game.Connection.Send(message); }
    }

    public override void NetworkSerialize(BinaryWriter writer)
    {
        writer.Write(Owner!);
        writer.Write(Position);
    }

    public override void NetworkDeserialize(BinaryReader reader)
    {
        Owner = reader.ReadIPEndPoint();
        Position = reader.ReadVector2();
    }

    #endregion
}
