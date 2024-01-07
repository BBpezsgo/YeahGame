using System;
using System.Diagnostics.CodeAnalysis;
using Win32;

namespace YeahGame;

public class Game
{
    static Game? singleton;

    readonly ConsoleRenderer renderer;

    public static ConsoleRenderer Renderer => singleton!.renderer;

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

            renderer[5, 5] = new ConsoleChar('X');
            renderer.Render();
        }
    }
}
