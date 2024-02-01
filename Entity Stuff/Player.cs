using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public class Player : NetworkEntity, IDamageable
{
    const float Speed = 10;
    const float UsernameHoverDistance = 5f;
    const float NetPositionThreshold = .5f;
    const float NetPositionMaxSleep = 3f;
    const float ReloadTime = .5f;
    const float SelfDestructionDamageRadius = 5f;
    const float RapidFireTime = 3.5f;
    const float RapidFireReload = .2f;
    const float DoubleFireTime = 5f;
    static readonly float InverseSqrt2 = 1f / MathF.Sqrt(2f);

    const int RPC_Shoot = 1;
    const int RPC_Damage = 2;
    const int RPC_Powerup = 3;

    public bool IsLocalOwned => Game.Connection.LocalEndPoint?.Equals(Owner) ?? false;
    public override EntityPrototype Prototype => EntityPrototype.Player;

    public float HP = 1;
    public IPEndPoint? Owner;
    float LastShot = Time.Now;

    float GotRapidFireTime;
    float GotDoubleFireTime;

    Vector2 NetPosition;
    float LastNetPositionTime;
    CapturedTouch? CapturedTouch;

    public byte Color
    {
        get
        {
            if (PlayerInfo is null) return CharColor.White;
            if (!Enum.IsDefined(PlayerInfo.Color.Value)) return CharColor.White;

            return (byte)PlayerInfo.Color.Value;
        }
    }

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

                UsePowerup(item);

                Game.Connection.Send(new RPCMessage()
                {
                    ObjectId = NetworkId,
                    RPCId = RPC_Powerup,
                    ShouldAck = true,
                    Details = Utils.Serialize(writer =>
                    {
                        writer.Write((byte)item);
                    }),
                });
            }
        }

#if !SERVER
        Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
        Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);
#endif

        CapturedTouch ??= new CapturedTouch();
        CapturedTouch.Tick(p => !Game.Singleton.MouseBlockedByUI((Coord)p));

        bool shouldShoot = false;
        Vector2 shootTarget = default;

        if (Touch.IsTouchDevice)
        {
            shouldShoot = CapturedTouch.Has;
            shootTarget = CapturedTouch.Position;
        }
        else
        {
            shouldShoot =
                !Mouse.WasUsed &&
                Mouse.IsPressed(MouseButton.Left) &&
                !Game.Singleton.MouseBlockedByUI(Mouse.RecordedConsolePosition);
            shootTarget = Mouse.RecordedConsolePosition;
        }

        if (Time.Now - LastShot >= GetCurrentReloadTime() && shouldShoot)
        {
            Vector2 direction = shootTarget - Position;
            if (Time.Now - GotDoubleFireTime <= DoubleFireTime)
            {
                Shoot(Utils.RotateVectorDeg(direction, -7f));
                Shoot(Utils.RotateVectorDeg(direction, 7f));
            }
            else
            {
                Shoot(direction);
            }
        }

        SyncUp();
    }

    void UsePowerup(ItemType item)
    {
        switch (item)
        {
            case ItemType.RapidFire:
            {
                GotRapidFireTime = Time.Now;
                break;
            }

            case ItemType.SuicideBomber:
            {
                if (!Game.IsServer &&
                    !Game.IsOffline)
                { break; }

                DoesExist = false;

                for (int i = 0; i < Game.Singleton.GameScene.Entities.Count; i++)
                {
                    Entity explodedEntity = Game.Singleton.GameScene.Entities[i];
                    if (explodedEntity is IDamageable &&
                        Vector2.Distance(Position, explodedEntity.Position) <= SelfDestructionDamageRadius)
                    {
                        explodedEntity.DoesExist = false;
                    }
                }
                break;
            }

            case ItemType.DoubleFire:
            {
                GotDoubleFireTime = Time.Now;
                break;
            }

            default: break;
        }
    }

    void Shoot(Vector2 direction)
    {
        Vector2 velocity = direction;
        velocity *= new Vector2(1f, 2f);
        velocity = Vector2.Normalize(velocity);
        velocity *= Projectile.Speed;
        velocity *= new Vector2(1, 0.5f);

        Game.Connection.Send(new RPCMessage()
        {
            ObjectId = NetworkId,
            RPCId = RPC_Shoot,
            Details = Utils.Serialize(writer =>
            {
                writer.Write(Position);
                writer.Write(velocity);
            })
        });

        if (Game.IsServer || Game.IsOffline)
        {
            Game.Singleton.GameScene.SpawnEntity(new Particles(ParticleConfigs.GetShoot(Vector2.Normalize(velocity)), Utils.Random)
            {
                Position = Position,
            });

            Game.Singleton.GameScene.SpawnEntity(new Projectile(this)
            {
                Position = Position,
                Velocity = velocity,
                SpawnedAt = Time.Now
            });
        }

        LastShot = Time.Now;
    }

    public void Damage(float amount)
    {
        if (!Game.IsServer && !Game.IsOffline) return;

        if (Game.IsServer)
        {
            Game.Connection.Send(new RPCMessage()
            {
                ObjectId = NetworkId,
                RPCId = RPC_Damage,
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
            if (!Enum.IsDefined(info.Color.Value)) color = CharColor.White;
            else color = (byte)info.Color.Value;

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
        if (Time.Now - GotRapidFireTime < RapidFireTime)
        {
            return RapidFireReload;
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

        switch (message.RPCId)
        {
            case RPC_Shoot:
            {
                Vector2 projectilePosition = reader.ReadVector2();
                Vector2 velocity = reader.ReadVector2();

                Game.Singleton.GameScene.SpawnEntity(new Particles(ParticleConfigs.GetShoot(Vector2.Normalize(velocity)), Utils.Random)
                {
                    Position = Position,
                });

                Game.Singleton.GameScene.SpawnEntity(new Projectile(this)
                {
                    Position = projectilePosition,
                    Velocity = velocity,
                    SpawnedAt = Time.Now
                });
                break;
            }

            case RPC_Damage:
                Damage(reader.ReadSingle());
                break;

            case RPC_Powerup:
                ItemType item = (ItemType)reader.ReadByte();
                UsePowerup(item);
                break;
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
