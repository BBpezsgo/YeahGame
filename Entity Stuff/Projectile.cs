namespace YeahGame;

public class Projectile : Entity
{
    public const float Speed = 25f;
    public const int Damage = 1;
    public const float Lifetime = 5f;

    public Vector2 Velocity;
    public float SpawnedAt;
    public Entity Owner;

    public Projectile(Entity owner)
    {
        IsSolid = false;
        Owner = owner;
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;
        Game.Renderer[Position] = (ConsoleChar)'•';
    }

    public override void Update()
    {
        if (Time.Now - SpawnedAt > Lifetime)
        {
            DoesExist = false;
            return;
        }

        Vector2 lastPos = Position;
        Position += Velocity * Time.Delta;

        Vector2 deltaPos = Position - lastPos;

        bool hit = Utils.TilemapRaycast(
            lastPos,
            Vector2.Normalize(Velocity), deltaPos.Length(),
            out Vector2 intersection,
            (x, y) =>
            {
                if (Game.Singleton.GameScene.Map is null)
                { return false; }

                if (x < 0 || y < 0 || x >= Game.Singleton.GameScene.MapWidth || y >= Game.Singleton.GameScene.MapHeight)
                { return false; }

                return Game.Singleton.GameScene.Map[x + y * Game.Singleton.GameScene.MapWidth];
            });

        if (hit)
        {
            DoesExist = false;
            return;
        }

        foreach (Entity entity in Game.Singleton.GameScene.Entities)
        {
            if (entity == Owner) continue;
            if (!entity.IsSolid) continue;

            Vector2 p = Utils.Point2LineDistance(lastPos, Position, entity.Position);
            if (Vector2.Distance(p, entity.Position) <= 1.5f &&
                Vector2.Distance(lastPos + (deltaPos * .5f), entity.Position) <= MathF.Max(1f, deltaPos.Length()))
            {
                if (entity is Player player) player.Damage(Damage);
                if (entity is Tester tester) tester.Damage(Damage);

                DoesExist = false;
                return;
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        Game.Singleton.GameScene.SpawnEntity(new Particles(ParticleConfigs.GetImpact(-Vector2.Normalize(Velocity)), Utils.Random)
        {
            Position = Position,
        });
    }
}
