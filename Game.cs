namespace YeahGame;

public class Game
{
    static Game? singleton;
    readonly ConsoleRenderer renderer;
    public static ConsoleRenderer Renderer => singleton!.renderer;

    public Player player = new();

    public Game()
    {
        renderer = new ConsoleRenderer();
        singleton = this;
    }

    public void Start()
    {
        bool wasResized = false;

        ConsoleListener.KeyEvent += Keyboard.Feed;
        ConsoleListener.MouseEvent += Mouse.Feed;
        ConsoleListener.WindowBufferSizeEvent += _ => wasResized = true;

        ConsoleListener.Start();

        while (true)
        {
            Time.Tick();
            Keyboard.Tick();
            Mouse.Tick();

            if (wasResized)
            {
                renderer.RefreshBufferSize();
                wasResized = false;
            }
            else
            {
                renderer.ClearBuffer();
            }

            player.Update();
            player.Render();

            renderer.Render();
        }
    }
}
