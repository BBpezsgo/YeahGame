namespace YeahGame
{
    public class Player : Entity
    {
        

        public override void Update()
        {
            if (Keyboard.IsKeyPressed('W'))
            {
                Position.Y -= 10 * Time.Delta;
            }
            if (Keyboard.IsKeyPressed('S'))
            {
                Position.Y += 10 * Time.Delta;
            }
            if (Keyboard.IsKeyPressed('A'))
            {
                Position.X -= 10 * Time.Delta;
            }
            if (Keyboard.IsKeyPressed('D'))
            {
                Position.X += 10 * Time.Delta;
            }
            Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
            Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);

            if (Mouse.IsPressed(MouseButton.Left))
            {
                Vector2 velocity = Mouse.RecordedConsolePosition - Position;
                velocity = Vector2.Normalize(velocity);
                velocity *= 15;
                Projectile newProjectile = new();
                newProjectile.Position = Position;
                newProjectile.Velocity = velocity;
                newProjectile.SpawnedAt = Time.Now;
                Game.Singleton.projectiles.Add(newProjectile);
            }
        }

        public override void Render()
        {
            if (!Game.Renderer.IsVisible(Position)) return;
            Game.Renderer[Position] = (ConsoleChar)'○';
        }
    }    
}
