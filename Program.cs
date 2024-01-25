using System.Runtime.Versioning;

namespace YeahGame;

public class Program
{
    [SupportedOSPlatform("windows")]
    static void Main(string[] args)
    {
        // const int WindowWidth = 640;
        // const int WindowHeight = 480;
        // 
        // const int RenderWidth = WindowWidth / 2;
        // const int RenderHeight = WindowHeight / 2;
        // 
        // Image font = Image.LoadFile("font.png", 0).Value;
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
        //     renderer.DrawString(0, 0, "Hello", font);
        // 
        //     // for (int x = 0; x < renderer.Width; x++)
        //     // {
        //     //     for (int y = 0; y < renderer.Height; y++)
        //     //     {
        //     //         renderer[x, y] = new Win32.Gdi32.GdiColor(y / (float)renderer.Height, x / (float)renderer.Width, 0);
        //     //     }
        //     // }
        // 
        //     renderer.Render();
        // 
        //     Debug.WriteLine($"{(int)(1f / Time.Delta)}");
        // }

        ConnectionBase<PlayerInfo> connection;

        Console.WriteLine("What kind of connection to use? (udp/ws)");
        while (true)
        {
            Console.Write(" > ");
            string? input = Console.ReadLine();

            if (string.Equals(input, "udp"))
            {
                connection = new UdpConnection<PlayerInfo>();
                Console.Clear();
                break;
            }

            if (string.Equals(input, "ws"))
            {
                connection = new WebSocketConnection<PlayerInfo>();
                Console.Clear();
                break;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Invalid input \"{input}\"");
            Console.ResetColor();
        }

        Game game = new(new ConsoleRenderer(), connection);
        game.Start(args);
    }
}
