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

    readonly ConsoleInputField InputName = new("Bruh");

    readonly ConsoleSelectBox<string> SelectBox = new("Item 1", "Item 2", "Item 3");

    public string? ExitReason;

    public override void Load()
    {
        base.Load();
        InputSocket.Value = new System.Text.StringBuilder(Biscuit.Socket);
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
        else if (Game.Connection.State != ConnectionState.None)
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
            SmallRect box = Layout.Center(new SmallSize(30, 13), new SmallRect(default, Game.Renderer.Size));

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

            Game.Renderer.SelectBox(
                new SmallRect(box.Left, box.Top + y++, box.Width, 1),
                SelectBox);

            y++;

            if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Offline", Styles.ButtonStyle))
            {
                InputSocketError = null;
                Game.Connection.LocalUserInfo = new PlayerInfo()
                {
                    Username = InputName.Value.ToString(),
                };

                Game.IsOffline = true;
            }

            if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Connect", Styles.ButtonStyle))
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
                    }
                    catch (SocketException socketException)
                    { InputSocketError = socketException.SocketErrorCode.ToString(); }
                    catch (Exception exception)
                    { InputSocketError = exception.Message; }
                }
                else
                { InputSocketError = error; }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Host", Styles.ButtonStyle))
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
            else
            {
                if (Game.Renderer.Button(new SmallRect(box.Left, box.Top + y++, box.Width, 1), "Host", Styles.DisabledButtonStyle))
                {
                    InputSocketError = "Not Supported";
                }
            }
        }
    }

    public override void Tick()
    {

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
