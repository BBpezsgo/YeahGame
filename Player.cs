namespace YeahGame
{
    public class Player : Entity
    {
        float LastShot = Time.Now;
        const float Speed = 10;
        public override void Update()
        {
            if (Keyboard.IsKeyPressed('W'))
            {
                Position.Y -= Speed * 0.5f * Time.Delta;
            }
            if (Keyboard.IsKeyPressed('S'))
            {
                Position.Y += Speed * 0.5f * Time.Delta;
            }
            if (Keyboard.IsKeyPressed('A'))
            {
                Position.X -= Speed * Time.Delta;
            }
            if (Keyboard.IsKeyPressed('D'))
            {
                Position.X += Speed * Time.Delta;
            }
            Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
            Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);

            if (Mouse.IsPressed(MouseButton.Left)&&
                Time.Now - LastShot > 0.5f)
            {
                Vector2 velocity = Mouse.RecordedConsolePosition - Position;
                velocity = Vector2.Normalize(velocity);
                velocity *= 25;
                velocity *= new Vector2(1, 0.5f);
                Projectile newProjectile = new();
                newProjectile.Position = Position;
                newProjectile.Velocity = velocity;
                newProjectile.SpawnedAt = Time.Now;
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
}
