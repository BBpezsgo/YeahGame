namespace YeahGame;

public static class Styles
{
    public static readonly ConsoleSelectBoxStyle SelectBoxStyle= new()
    {
        LabelNormal = CharColor.Make(CharColor.Black, CharColor.Silver),
        LabelHover = CharColor.Make(CharColor.Black, CharColor.White),
        LabelDown = CharColor.Make(CharColor.Black, CharColor.White),
        LabelActive = CharColor.Make(CharColor.Black, CharColor.White),

        ButtonNormal = CharColor.Make(CharColor.Black, CharColor.Silver),
        ButtonHover = CharColor.Make(CharColor.Black, CharColor.White),
        ButtonDown = CharColor.Make(CharColor.Black, CharColor.BrightCyan),

        LeftChar = '<',
        RightChar = '>',
    };

    public static readonly ConsoleButtonStyle ButtonStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Silver),
        Hover = CharColor.Make(CharColor.Black, CharColor.White),
        Down = CharColor.Make(CharColor.Black, CharColor.BrightCyan),
    };

    public static readonly ConsoleButtonStyle DisabledButtonStyle = new()
    {
        Normal = CharColor.Make(CharColor.Black, CharColor.Gray),
        Hover = CharColor.Make(CharColor.Black, CharColor.Gray),
        Down = CharColor.Make(CharColor.Black, CharColor.Gray),
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
