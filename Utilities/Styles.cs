namespace YeahGame;

public static class Styles
{
    public static readonly ConsoleButtonStyle ButtonStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Hover = CharColor.Make(CharColor.Black, CharColor.White),
        Down = CharColor.Make(CharColor.Black, CharColor.BrightCyan),
    };

    public static readonly ConsoleInputFieldStyle InputFieldStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Active = CharColor.Make(CharColor.Black, CharColor.White),
    };

    public static readonly ConsoleDropdownStyle DropdownStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Hover = CharColor.Make(CharColor.Black, CharColor.White),
        Down = CharColor.Make(CharColor.Black, CharColor.BrightCyan),
        ActiveChar = '▼',
        InactiveChar = '►',
    };
}
