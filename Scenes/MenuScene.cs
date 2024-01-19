using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using YeahGame.Messages;

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

    ConsoleRenderer.TextField InputSocket = new("127.0.0.1:5555");
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
        SmallRect box = Layout.Center(new Coord(30, 11), new SmallRect(default, Game.Renderer.Rect));

        Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);

        Game.Renderer.Text(box.Left + 2, box.Top + 2, "Socket:");

        Game.Renderer.InputField(
            new SmallRect((short)(box.Left + 2), (short)(box.Top + 3), (short)(box.Width - 4), 1),
            TextFieldStyle,
            ref InputSocket);

        Game.Renderer.Text(box.Left + 2, box.Top + 4, InputSocketError, CharColor.BrightRed);

        Game.Renderer.Text(box.Left + 2, box.Top + 5, "Username:");

        Game.Renderer.InputField(
            new SmallRect((short)(box.Left + 2), (short)(box.Top + 6), (short)(box.Width - 4), 1),
            TextFieldStyle,
            ref InputName);

        if (Game.Renderer.Button(
            Layout.Center(
                new Coord(10, 1),
                new SmallRect(box.Left, (short)(box.Top + 7), box.Width, 1)
                ),
            "Connect",
            ButtonStyle))
        {
            InputSocketError = null;
            Game.Connection.LocalUserInfo = new PlayerInfo()
            {
                Username = InputName.Value.ToString(),
            };

            if (TryParseSocket(InputSocket.Value.ToString(), out IPAddress? address, out ushort port, out string? error))
            {
                try
                { Game.Connection.Client(address, port); }
                catch (Exception)
                { InputSocketError = "Bruh"; }
            }
            else
            { InputSocketError = error; }
        }

        if (Game.Renderer.Button(
            Layout.Center(
                new Coord(10, 1),
                new SmallRect(box.Left, (short)(box.Top + 8), box.Width, 1)
                ),
            "Host",
            ButtonStyle))
        {
            InputSocketError = null;
            Game.Connection.LocalUserInfo = new PlayerInfo()
            {
                Username = InputName.Value.ToString(),
            };

            if (TryParseSocket(InputSocket.Value.ToString(), out IPAddress? address, out ushort port, out string? error))
            {
                try
                { Game.Connection.Server(address, port); }
                catch (Exception)
                { InputSocketError = "Bruh"; }
            }
            else
            { InputSocketError = error; }
        }
    }

    public override void Tick()
    {

    }

    static bool TryParseSocket(
        string input,
        [NotNullWhen(true)] out IPAddress? address,
        [NotNullWhen(true)] out ushort port,
        [NotNullWhen(false)] out string? error)
    {
        address = default;
        port = default;
        error = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = $"Input is empty";
            return false;
        }

        int colonIndex;
        if ((colonIndex = input.IndexOf(':')) == -1)
        {
            error = $"Input must contain ':'";
            return false;
        }

        ReadOnlySpan<char> left = input.AsSpan()[..colonIndex];
        ReadOnlySpan<char> right = input.AsSpan()[(colonIndex + 1)..];

        if (!IPAddress.TryParse(left, out address))
        {
            IPHostEntry entry = Dns.GetHostEntry(left.ToString(), AddressFamily.InterNetwork);
            if (entry.AddressList.Length == 0)
            {
                error = "Hostname not found";
                return false;
            }
            address = entry.AddressList[0];
        }

        if (!ushort.TryParse(right, out port))
        {
            error = "Bad formatted port";
            return false;
        }

        if (port == 0)
        {
            error = "Invalid port";
            return false;
        }

        return true;
    }
}
