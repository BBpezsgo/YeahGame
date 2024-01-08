using System.Net;

namespace YeahGame;

internal class Program
{
    static void Main(string[] args)
    {
        {
            Connection connection = new();
            char input = Console.ReadKey().KeyChar;
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
                connection.Receive();
            }
        }

        return;

        Game game = new();
        game.Start();
    }
}
