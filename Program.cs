namespace YeahGame;

public class Program
{
    static void Main(string[] args)
    {
        // static void RenderEffect(Renderer renderer)
        // {
        //     for (int x = 0; x < renderer.Width; x++)
        //     {
        //         for (int y = 0; y < renderer.Height; y++)
        //         {
        //             renderer[x, y] = new GdiColor(y / (float)renderer.Height, x / (float)renderer.Width, 0);
        //         }
        //     }
        // }

        // const int WindowWidth = 640;
        // const int WindowHeight = 480;
        // 
        // const int RenderWidth = WindowWidth / 4;
        // const int RenderHeight = WindowHeight / 4;
        // 
        // using Renderer renderer = new(RenderWidth, RenderHeight, WindowWidth, WindowHeight);
        // 
        // bool done = false;
        // while (!done && renderer.Tick())
        // {
        //     Time.Tick();
        // 
        //     done |= Win32.LowLevel.User32.GetAsyncKeyState(Win32.LowLevel.VirtualKeyCode.ESCAPE) != 0;
        // 
        //     RenderEffect(renderer);
        //     renderer.Render();
        // 
        //     Console.WriteLine($"{(int)(1f / Time.Delta)}");
        // }

        Game game = new();
        game.Start();
    }
}
