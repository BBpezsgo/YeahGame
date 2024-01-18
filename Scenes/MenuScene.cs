using System.Net;

namespace YeahGame;

public class MenuScene : Scene
{
    public override string Name => "Menu";

    static readonly ConsoleRenderer.ButtonStyle ButtonStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Hover = CharColor.Make(CharColor.Black, CharColor.White),
        Down = CharColor.Make(CharColor.Black, CharColor.BrightCyan),
    };

    static readonly ConsoleRenderer.TextFieldStyle TextFieldStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Active = CharColor.Make(CharColor.Black, CharColor.White),
    };

    ConsoleRenderer.TextField InputSocket = new("127.0.0.1");
    string? InputSocketError = null;

    ConsoleRenderer.TextField InputName = new("Bruh");

    public override void Load()
    {
        base.Load();
    }

    public override void Unload()
    {
        base.Unload();
    }

    public override void Render()
    {
        SmallRect box = Layout.Center(new Coord(30, 10), new SmallRect(default, Game.Renderer.Rect));

        Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);

        Game.Renderer.Text(box.Left + 2, box.Top + 2, "Socket:");

        Game.Renderer.InputField(
            new SmallRect((short)(box.Left + 2), (short)(box.Top + 3), (short)(box.Width - 4), 1),
            TextFieldStyle,
            ref InputSocket);

        Game.Renderer.Text(box.Left + 2, box.Top + 4, InputSocketError, CharColor.BrightRed);

        Game.Renderer.Text(box.Left + 2, box.Top + 4, "Username:");

        Game.Renderer.InputField(
            new SmallRect((short)(box.Left + 2), (short)(box.Top + 5), (short)(box.Width - 4), 1),
            TextFieldStyle,
            ref InputName);

        if (Game.Renderer.Button(
            Layout.Center(
                new Coord(10, 1),
                new SmallRect(box.Left, (short)(box.Top + 6), box.Width, 1)
                ),
            "Connect",
            ButtonStyle))
        {
            InputSocketError = null;
            Game.Connection.LocalUserInfo = new PlayerInfo()
            {
                Username = InputName.Value.ToString(),
            };

            if (IPAddress.TryParse(InputSocket.Value.ToString(), out IPAddress? address))
            { Game.Connection.Client(address, 5000); }
            else
            { InputSocketError = "Invalid input"; }
        }

        if (Game.Renderer.Button(
            Layout.Center(
                new Coord(10, 1),
                new SmallRect(box.Left, (short)(box.Top + 7), box.Width, 1)
                ),
            "Host",
            ButtonStyle))
        {
            InputSocketError = null;
            Game.Connection.LocalUserInfo = new PlayerInfo()
            {
                Username = InputName.Value.ToString(),
            };

            if (IPAddress.TryParse(InputSocket.Value.ToString(), out IPAddress? address))
            { Game.Connection.Server(address, 5000); }
            else
            { InputSocketError = "Invalid input"; }
        }
    }

    public override void Tick()
    {

    }
}
