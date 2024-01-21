using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Win32.Common;

namespace YeahGame;

public class MenuScene : Scene
{
    public override string Name => "Menu";

    ConsoleInputField InputSocket = new("127.0.0.1:5555");
    string? InputSocketError = null;
    ConsoleInputField InputName = new("Bruh");

    public string? ExitReason;

    public override void Load()
    {
        base.Load();
    }

    public override void Unload()
    {
        base.Unload();
        ExitReason = null;
    }

    public override void Render()
    {
        if (ExitReason is not null)
        {
            SmallRect box = Layout.Center(new SmallSize(30, 6), new SmallRect(default, Game.Renderer.Rect));

            Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);
            box = box.Margin(1);

            int center = Layout.Center(box.Width, ExitReason);
            Game.Renderer.Text(box.Left + center, box.Top + 1, ExitReason);

            if (Game.Renderer.Button(new SmallRect(box.X, box.Top + 3, box.Width, 1), "OK", Styles.ButtonStyle))
            {
                ExitReason = null;
            }
        }
        else if (Game.Connection.State != ConnectionState.None)
        {
            SmallRect box = Layout.Center(new SmallSize(30, 4), new SmallRect(default, Game.Renderer.Rect));

            Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);
            box = box.Margin(1);

            string text = Game.Connection.State switch
            {
                ConnectionState.Hosting => "Hosting",
                ConnectionState.Connecting => "Connecting ...",
                ConnectionState.Connected => "Connected",
                _ => "Bruh",
            };
            int center = Layout.Center(box.Width, text);
            Game.Renderer.Text(box.Left + center, box.Top + 1, text);
        }
        else
        {
            SmallRect box = Layout.Center(new SmallSize(30, 11), new SmallRect(default, Game.Renderer.Rect));

            Game.Renderer.Box(box, CharColor.Black, CharColor.White, Ascii.BoxSides);
            box = box.Margin(1, 2);
            int y = 1;

            Game.Renderer.Text(box.Left, box.Top + y++, "Socket:");

            Game.Renderer.InputField(
                new SmallRect(box.Left, box.Top + y++, box.Width, 1),
                Styles.TextFieldStyle,
                ref InputSocket);

            Game.Renderer.Text(box.Left, box.Top + y++, InputSocketError, CharColor.BrightRed);

            Game.Renderer.Text(box.Left, box.Top + y++, "Username:");

            Game.Renderer.InputField(
                new SmallRect(box.Left, box.Top + y++, box.Width, 1),
                Styles.TextFieldStyle,
                ref InputName);

            y++;

            if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Connect", Styles.ButtonStyle))
            {
                InputSocketError = null;
                Game.Connection.LocalUserInfo = new PlayerInfo()
                {
                    Username = InputName.Value.ToString(),
                };

                if (TryParseSocket(InputSocket.Value.ToString(), out IPAddress? address, out ushort port, out string? error))
                {
                    try
                    { Game.Connection.StartClient(address, port); }
                    catch (Exception)
                    { InputSocketError = "Bruh"; }
                }
                else
                { InputSocketError = error; }
            }

            if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Host", Styles.ButtonStyle))
            {
                InputSocketError = null;
                Game.Connection.LocalUserInfo = new PlayerInfo()
                {
                    Username = InputName.Value.ToString(),
                };

                if (TryParseSocket(InputSocket.Value.ToString(), out IPAddress? address, out ushort port, out string? error))
                {
                    try
                    { Game.Connection.StartHost(address, port); }
                    catch (Exception)
                    { InputSocketError = "Bruh"; }
                }
                else
                { InputSocketError = error; }
            }
        }
    }

    public override void Tick()
    {

    }

    public static bool TryParseSocket(
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
