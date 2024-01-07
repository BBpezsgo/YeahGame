namespace YeahGame;

public class Game
{
    static Game? singleton;
    readonly ConsoleRenderer renderer;
    public static ConsoleRenderer Renderer => singleton!.renderer;

    public static Game Singleton => singleton!;

    public Player player = new();

    public List<Projectile> projectiles = new();

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
        ConsoleHandler.Setup();
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
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile currentProjectile = projectiles[i];
                currentProjectile.Update();
                if (currentProjectile.DoesExist != true)
                {
                    projectiles.RemoveAt(i);
                }
            }

            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i].Render();
            }

            player.Render();

            renderer.Render();
        }
    }
}
