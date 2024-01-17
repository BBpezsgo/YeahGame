using Win32.Gdi32;

namespace YeahGame;

public class Program
{
    static void RenderEffect(Renderer renderer)
    {
        for (int x = 0; x < renderer.Width; x++)
        {
            for (int y = 0; y < renderer.Height; y++)
            {
                renderer[x, y] = new GdiColor(y / (float)renderer.Height, x / (float)renderer.Width, 0);
            }
        }
    }

    static void Main(string[] args)
    {
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

        /*
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        {
            Connection connection = new();
            char input = Console.ReadKey().KeyChar;
            Console.WriteLine();

            connection.OnClientConnected += (client, phase) => Console.WriteLine($"Client {client} connecting: {phase}");
            connection.OnClientDisconnected += (client) => Console.WriteLine($"Client {client} disconnected");
            connection.OnConnectedToServer += (phase) => Console.WriteLine($"Connected to server: {phase}");
            connection.OnDisconnectedFromServer += () => Console.WriteLine($"Disconnected from server");

            if (input == 's')
            {
                connection.Server(IPAddress.Parse("127.0.0.1"), 5000);
            }
            else
            {
                connection.Client(IPAddress.Parse("127.0.0.1"), 5000);
            }

            while (true)
            {
                connection.Tick();
                Thread.Sleep(50);
            }
            return;
        }
        */

        Game game = new();
        game.Start();
    }
}
