namespace YeahGame
{
    public class Player
    {
        public Vector2 Position;

        public void Update()
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
        }

        public void Render()
        {
            Game.Renderer[Position] = (ConsoleChar)'○';
        }
    }    
}
