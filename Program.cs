using System.Diagnostics;
using System.Net;

namespace YeahGame;

internal class Program
{
    static void Main(string[] args)
    {
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
        }

        return;

        Game game = new();
        game.Start();
    }
}
