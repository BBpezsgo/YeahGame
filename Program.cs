using System.Runtime.Versioning;
using Win32.Gdi32;

namespace YeahGame;

public class Program
{
    static void Main(string[] args)
    {
        // {
        //     bool wasResized = false;
        //     ConsoleListener.KeyEvent += Keyboard.Feed;
        //     ConsoleListener.MouseEvent += Mouse.Feed;
        //     ConsoleListener.WindowBufferSizeEvent += _ => wasResized = true;
        // 
        //     ConsoleHandler.Setup();
        //     ConsoleListener.Start();
        // 
        //     ConsoleRenderer renderer = new();
        // 
        //     int imgw = 64;
        //     int imgh = 64 / 2;
        //     ConsoleChar[] img = new ConsoleChar[imgw * imgh];
        //     for (int y = 0; y < imgh; y++)
        //     {
        //         for (int x = 0; x < imgw; x++)
        //         {
        //             img[x + (y * imgw)] = new ConsoleChar(' ', 0, CharColor.To4bitIRGB(System.Drawing.Color.FromArgb(x * 255 / imgw, y * 255 / imgh, 0)));
        //         }
        //     }
        //     ConsoleImage image = new(img, imgw, imgh);
        // 
        //     while (true)
        //     {
        //         Keyboard.Tick();
        //         Mouse.Tick();
        // 
        //         if (wasResized)
        //         {
        //             renderer.RefreshBufferSize();
        //             wasResized = false;
        //         }
        //         else
        //         {
        //             renderer.ClearBuffer();
        //         }
        // 
        //         renderer.Image(Mouse.RecordedConsolePosition - new Coord(image.Width + 4, (short)0), image);
        //         renderer.Render();
        //     }
        //     ConsoleListener.Stop();
        //     return;
        // }
        // const int WindowWidth = 640;
        // const int WindowHeight = 480;
        // 
        // const int RenderWidth = WindowWidth / 2;
        // const int RenderHeight = WindowHeight / 2;
        // 
        // Image fontImage = Image.LoadFile(@"C:\Users\bazsi\Desktop\Unity Stuff\Computer (removed from Nothing3D project)\Font3.png", default).Value;
        // BitmapFont<uint> font = BitmapFont.FromAny<ValueTuple<byte, byte, byte>, uint>(fontImage.Data.AsSpan(), fontImage.Width, fontImage.Height, 8, 8, v => GdiColor.Make(v.Item1, v.Item2, v.Item3));
        // 
        // using WindowRenderer renderer = new(RenderWidth, RenderHeight, WindowWidth, WindowHeight);
        // 
        // bool done = false;
        // while (!done && renderer.Tick())
        // {
        //     Time.Tick();
        // 
        //     done |= Win32.LowLevel.User32.GetAsyncKeyState((int)Win32.LowLevel.VirtualKeyCode.ESCAPE) != 0;
        // 
        //     renderer.Text(0, 0, "Hello", font);
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
        //     System.Diagnostics.Debug.WriteLine($"{(int)(1f / Time.Delta)}");
        // }
        // 
        // return;

#if SERVER
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener(false));
#endif

        /*
        ConnectionBase<PlayerInfo> connection;

        Console.WriteLine("What kind of connection to use? (udp/ws)");
        while (true)
        {
            Console.Write(" > ");
            string? input = Console.ReadLine();

            if (string.Equals(input, "udp"))
            {
                connection = new UdpConnection<PlayerInfo>();
                break;
            }

            if (string.Equals(input, "ws"))
            {
                connection = new WebSocketConnection<PlayerInfo>();
                break;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Invalid input \"{input}\"");
            Console.ResetColor();
        }
        */

        Game game = new(
#if !SERVER
            OperatingSystem.IsWindows() ? new ConsoleRenderer() : new AnsiRenderer()
#endif
            );
        game.Start(args);
    }
}
