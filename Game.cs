using Win32;

namespace YeahGame;

public class Game
{
    public void Start()
    {
        ConsoleRenderer renderer = new();

        while (true)
        {
            renderer.ClearBuffer();

            renderer[5, 5] = new ConsoleChar('X');
            renderer.Render();
        }
    }
}
