namespace YeahGame;

public class Player : Entity
{
    const float Speed = 10;

    float LastShot = Time.Now;

    public override void Update()
    {
        if (Keyboard.IsKeyPressed('W')) Position.Y -= Speed * 0.5f * Time.Delta;
        if (Keyboard.IsKeyPressed('S')) Position.Y += Speed * 0.5f * Time.Delta;
        if (Keyboard.IsKeyPressed('A')) Position.X -= Speed * Time.Delta;
        if (Keyboard.IsKeyPressed('D')) Position.X += Speed * Time.Delta;

        Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
        Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);

        if (Mouse.IsPressed(MouseButton.Left) &&
            Time.Now - LastShot > 0.5f)
        {
            Vector2 velocity = Mouse.RecordedConsolePosition - Position;
            velocity = Vector2.Normalize(velocity);
            velocity *= Projectile.Speed;
            velocity *= new Vector2(1, 0.5f);

            Projectile newProjectile = new()
            {
                Position = Position,
                Velocity = velocity,
                SpawnedAt = Time.Now
            };
            Game.Singleton.projectiles.Add(newProjectile);

            LastShot = Time.Now;
        }
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;
        Game.Renderer[Position] = (ConsoleChar)'○';
    }
}
