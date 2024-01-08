namespace YeahGame;

public class Projectile : Entity
{
    public const float Speed = 25f;

    public Vector2 Velocity;
    public float SpawnedAt;

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;
        Game.Renderer[Position] = (ConsoleChar)'*';
    }

    public override void Update()
    {
        float lifetime = Time.Now - SpawnedAt;
        if (lifetime > 5)
        {
            DoesExist = false;
            return;
        }
        Position += Velocity * Time.Delta;
    }
}
