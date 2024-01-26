﻿using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Win32.Common;

namespace YeahGame;

public class MenuScene : Scene
{
    public override string Name => "Menu";

    readonly ConsoleInputField InputSocket = new(Biscuit.Socket);
    string? InputSocketError = null;

    readonly ConsoleInputField InputName = new(Biscuit.Username);

    const string ConnectionType_UDP = "UDP";
    const string ConnectionType_WebSocket = "WebSocket";

    readonly ConsoleSelectBox<string> ConnectionType = new(ConnectionType_UDP, ConnectionType_WebSocket);

    public string? ExitReason;

    public override void Load()
    {
        base.Load();
        InputSocket.Value = new System.Text.StringBuilder(Biscuit.Socket);
        InputName.Value = new System.Text.StringBuilder(Biscuit.Username);
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
            SmallRect box = Layout.Center(new SmallSize(30, 6), new SmallRect(default, Game.Renderer.Size));

            Game.Renderer.Box(box, CharColor.Black, CharColor.White, in Ascii.BoxSides);
            box = box.Margin(1);

            int center = Layout.Center(box.Width, ExitReason);
            Game.Renderer.Text(box.Left + center, box.Top + 1, ExitReason);

            if (Game.Renderer.Button(new SmallRect(box.X, box.Top + 3, box.Width, 1), "OK", Styles.ButtonStyle))
            {
                ExitReason = null;
            }
        }
        else if (Game.HasConnection &&
                 Game.Connection.State != ConnectionState.None)
        {
            string text = Game.Connection.State switch
            {
                ConnectionState.Hosting => "Hosting",
                ConnectionState.Connecting => $"Connecting to {Game.Connection.RemoteEndPoint} ...",
                ConnectionState.Connected => "Connected",
                _ => "Bruh",
            };

            SmallRect box = Layout.Center(new SmallSize(Math.Max(30, text.Length + 5), 4), new SmallRect(default, Game.Renderer.Size));
            Game.Renderer.Box(box, CharColor.Black, CharColor.White, in Ascii.BoxSides);
            box = box.Margin(1);

            int center = Layout.Center(box.Width, text);
            Game.Renderer.Text(box.Left + center, box.Top + 1, text);
        }
        else
        {
            SmallRect box = Layout.Center(new SmallSize(30, 14), new SmallRect(default, Game.Renderer.Size));

            Game.Renderer.Box(box, CharColor.Black, CharColor.White, in Ascii.BoxSides);
            box = box.Margin(1, 2);
            int y = 1;

            Game.Renderer.Text(box.Left, box.Top + y++, "Socket:");

            Game.Renderer.InputField(
                new SmallRect(box.Left, box.Top + y++, box.Width, 1),
                Styles.InputFieldStyle,
                InputSocket);

            Game.Renderer.Text(box.Left, box.Top + y++, InputSocketError, CharColor.BrightRed);

            Game.Renderer.Text(box.Left, box.Top + y++, "Username:");

            Game.Renderer.InputField(
                new SmallRect(box.Left, box.Top + y++, box.Width, 1),
                Styles.InputFieldStyle,
                InputName);

            y++;

            Game.Renderer.Text(box.Left, box.Top + y++, "Connection Type:");

            if (Game.Renderer.SelectBox(
                new SmallRect(box.Left, box.Top + y++, box.Width, 1),
                ConnectionType))
            {
                Game.Connection = ConnectionType.SelectedItem switch
                {
                    ConnectionType_UDP => new UdpConnection<PlayerInfo>(),
                    ConnectionType_WebSocket => new WebSocketConnection<PlayerInfo>(),
                    _ => null!,
                };
                Game.Singleton.SetupConnectionListeners(Game.Connection);
            }

            y++;

            if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Offline", Styles.ButtonStyle))
            {
                InputSocketError = null;

                Game.Connection ??= new UdpConnection<PlayerInfo>();
                Game.Singleton.SetupConnectionListeners(Game.Connection);
                Game.Connection.LocalUserInfo = new PlayerInfo()
                {
                    Username = InputName.Value.ToString(),
                };

                Biscuit.Username = InputName.Value.ToString();

                Game.IsOffline = true;
            }

            if (!Game.HasConnection)
            {
                if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Connect", Styles.DisabledButtonStyle))
                {
                    InputSocketError = "Select a connection type";
                }
            }
            else if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Connect", Styles.ButtonStyle))
            {
                InputSocketError = null;
                Game.Connection.LocalUserInfo = new PlayerInfo()
                {
                    Username = InputName.Value.ToString(),
                };

                Game.IsOffline = false;

                if (TryParseSocket(InputSocket.Value.ToString(), out IPEndPoint? endPoint, out string? error))
                {
                    try
                    {
                        Game.Connection.StartClient(endPoint);

                        Biscuit.Socket = InputSocket.Value.ToString();
                        Biscuit.Username = InputName.Value.ToString();
                    }
                    catch (SocketException socketException)
                    { InputSocketError = socketException.SocketErrorCode.ToString(); }
                    catch (Exception exception)
                    { InputSocketError = exception.Message; }
                }
                else
                { InputSocketError = error; }
            }

            if (OperatingSystem.IsBrowser())
            {
                if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Host", Styles.DisabledButtonStyle))
                {
                    InputSocketError = "Not Supported";
                }
            }
            else
            {
                if (!Game.HasConnection)
                {
                    if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Host", Styles.DisabledButtonStyle))
                    {
                        InputSocketError = "Select a connection type";
                    }
                }
                else if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Host", Styles.ButtonStyle))
                {
                    InputSocketError = null;
                    Game.Connection.LocalUserInfo = new PlayerInfo()
                    {
                        Username = InputName.Value.ToString(),
                    };

                    Game.IsOffline = false;

                    if (TryParseSocket(InputSocket.Value.ToString(), out IPEndPoint? endPoint, out string? error))
                    {
                        try
                        {
                            Game.Connection.StartHost(endPoint);

                            Biscuit.Socket = InputSocket.Value.ToString();
                            Biscuit.Username = InputName.Value.ToString();
                        }
                        catch (SocketException socketException)
                        { InputSocketError = socketException.SocketErrorCode.ToString(); }
                        catch (Exception exception)
                        { InputSocketError = exception.Message; }
                    }
                    else
                    { InputSocketError = error; }
                }
            }
        }
    }

    public override void Tick()
    {
#if SERVER
        while (true)
        {
            string defaultUsername = Biscuit.Username ?? "SERVER";
            string defaultSocket = Biscuit.Socket ?? "127.0.0.1";
            string defaultConnectionType = "udp";

            Console.WriteLine($"Enter a username (default is \"{defaultUsername}\"):");
            Console.Write(" > ");
            string? username = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(username)) username = defaultUsername;

            Console.WriteLine($"Username is \"{username}\"");

            IPEndPoint? socket;
            Console.WriteLine($"Enter a socket (default is \"{defaultSocket}\"):");
            while (true)
            {
                Console.Write(" > ");
                string? _socket = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(_socket)) _socket = defaultSocket;

                if (TryParseSocket(_socket, out socket, out string? error))
                { break; }

                Console.WriteLine($"Failed to parse socket \"{_socket}\":");
                Console.WriteLine(error);
            }

            Console.WriteLine($"Socket is \"{socket}\"");

            ConnectionBase<PlayerInfo> connection;
            Console.WriteLine($"Enter a connection type (default is \"{defaultConnectionType}\"):");
            while (true)
            {
                Console.Write(" > ");
                string? _connectionType = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(_connectionType)) _connectionType = defaultConnectionType;

                _connectionType = _connectionType.Trim().ToLowerInvariant();

                if (_connectionType == "udp" ||
                    _connectionType == "u")
                {
                    connection = new UdpConnection<PlayerInfo>();
                    Console.WriteLine($"Connection type is UDP");
                    break;
                }
                
                if (_connectionType == "ws" ||
                    _connectionType == "websocket" ||
                    _connectionType == "web" ||
                    _connectionType == "w")
                {
                    connection = new WebSocketConnection<PlayerInfo>();
                    Console.WriteLine($"Connection type is WebSocket");
                    break;
                }

                Console.WriteLine($"Invalid connection type \"{_connectionType}\"");
            }

            Game.Connection = connection;
            Game.Singleton.SetupConnectionListeners(connection);

            try
            {
                Game.Connection.StartHost(socket);

                Biscuit.Username = username.ToString();
                Biscuit.Socket = socket.ToString();

                break;
            }
            catch (Exception exception)
            { Console.WriteLine(exception); }
        }
#endif
    }

    public static bool TryParseSocket(
        string input,
        [NotNullWhen(true)] out IPEndPoint? endPoint,
        [NotNullWhen(false)] out string? error)
    {
        endPoint = default;
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

        if (!ushort.TryParse(right, out ushort port))
        {
            error = "Bad formatted port";
            return false;
        }

        if (port == 0)
        {
            error = "Invalid port";
            return false;
        }

        if (!IPAddress.TryParse(left, out IPAddress? address))
        {
            if (OperatingSystem.IsBrowser())
            {
                error = "Sadly you cannot do DNS lookup in the browser";
                return false;
            }
            else
            {
                IPHostEntry entry = Dns.GetHostEntry(left.ToString(), AddressFamily.InterNetwork);
                if (entry.AddressList.Length == 0)
                {
                    error = "Hostname not found";
                    return false;
                }
                address = entry.AddressList[0];
            }
        }

        endPoint = new IPEndPoint(address, port);
        return true;
    }
}
