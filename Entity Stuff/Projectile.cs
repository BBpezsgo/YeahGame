namespace YeahGame;

public class Projectile : Entity
{
    public const float Speed = 25f;

    public const int Damage = 1;

    public Vector2 Velocity;
    public float SpawnedAt;
    public Entity Owner;

    public Projectile(Entity owner)
    {
        Owner = owner;
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;
        Game.Renderer[Position] = (ConsoleChar)'•';
    }

    public override void Update()
    {
        float lifetime = Time.Now - SpawnedAt;

        if (lifetime > 5)
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
            if (entity is Projectile) continue;

            Vector2 p = Utils.Point2LineDistance(lastPos, Position, entity.Position);
            if (Vector2.Distance(p, entity.Position) < 1f && Vector2.Distance(lastPos + (deltaPos * .5f), entity.Position) < deltaPos.Length())
            {
                hit = true;
                if (entity is Player player)
                {
                    player.Damage(Damage);
                }
                break;
            }
        }

        if (hit)
        {
            DoesExist = false;
            return;
        }
    }
}
