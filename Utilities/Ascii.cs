using Win32.Common;

namespace YeahGame;

public static class Ascii
{
    public struct Blocks
    {
        public const char Left = '▌';
        public const char Right = '▐';
        public const char Top = '▀';
        public const char Bottom = '▄';
        public const char Full = '█';
    }

    public static readonly char[] CircleNumbersOutline = new char[]
    {
        '⓪',
        '①',
        '②',
        '③',
        '④',
        '⑤',
        '⑥',
        '⑦',
        '⑧',
        '⑨',
        '⑩',
        '⑪',
        '⑫',
        '⑬',
        '⑭',
        '⑮',
        '⑯',
        '⑰',
        '⑱',
        '⑲',
        '⑳',
    };

    public static readonly char[] CircleNumbersFilled = new char[]
    {
        '⓿',
        '❶',
        '❷',
        '❸',
        '❹',
        '❺',
        '❻',
        '❼',
        '❽',
        '❾',
        '❿',
        '⓫',
        '⓬',
        '⓭',
        '⓮',
        '⓯',
        '⓰',
        '⓱',
        '⓲',
        '⓳',
        '⓴',
    };

    public static readonly char[] BlockShade = new char[]
    {
        '░',
        '▒',
        '▓',
    };

    public static readonly char[] CircleOutline = new char[]
    {
        '˚',
        '°',
        'o',
        '○',
        'O',
    };

    public static readonly char[] CircleFilled = new char[]
    {
        '·',
        '•',
        '●',
    };

    public static readonly char[] ShadeShort1 = new char[]
    {
        '`',
        '.',
        ',',
        '\'',
        '"',
        '-',
        '+',
        '&',
        '$',
        '#',
        '@',
    };

    public static readonly char[] ShadeShort2 = new char[]
    {
        '.',
        ':',
        '-',
        '=',
        '+',
        '*',
        '#',
        '%',
        '@',
    };

    public static readonly char[] ShadeMedium = new char[]
    {
        '.',
        '\'',
        '`',
        '^',
        '"',
        ',',
        ':',
        ';',
        'I',
        'l',
        '!',
        'i',
        '>',
        '<',
        '~',
        '+',
        '_',
        '-',
        '?',
        ']',
        '[',
        '}',
        '{',
        '1',
        ')',
        '(',
        '|',
        '\\',
        '/',
        't',
        'f',
        'j',
        'r',
        'x',
        'n',
        'u',
        'v',
        'c',
        'z',
        'X',
        'Y',
        'U',
        'J',
        'C',
        'L',
        'Q',
        '0',
        'O',
        'Z',
        'm',
        'w',
        'q',
        'p',
        'd',
        'b',
        'k',
        'h',
        'a',
        'o',
        '*',
        '#',
        'M',
        'W',
        '&',
        '8',
        '%',
        'B',
        '@',
        '$',
    };

    public static readonly char[] ShadeLong = new char[] {
        '`',
        '.',
        '-',
        '\'',
        ':',
        '_',
        ',',
        '^',
        '=',
        ';',
        '>',
        '<',
        '+',
        '!',
        'r',
        'c',
        '*',
        '/',
        'z',
        '?',
        's',
        'L',
        'T',
        'v',
        ')',
        'J',
        '7',
        '(',
        '|',
        'F',
        'i',
        '{',
        'C',
        '}',
        'f',
        'I',
        '3',
        '1',
        't',
        'l',
        'u',
        '[',
        'n',
        'e',
        'o',
        'Z',
        '5',
        'Y',
        'x',
        'j',
        'y',
        'a',
        ']',
        '2',
        'E',
        'S',
        'w',
        'q',
        'k',
        'P',
        '6',
        'h',
        '9',
        'd',
        '4',
        'V',
        'p',
        'O',
        'G',
        'b',
        'U',
        'A',
        'K',
        'X',
        'H',
        'm',
        '8',
        'R',
        'D',
        '#',
        '$',
        'B',
        'g',
        '0',
        'M',
        'N',
        'W',
        'Q',
        '%',
        '&',
        '@',
    };

    public static readonly float[] ShadeLongV = new float[]
    {
        0.0751f,
        0.0829f,
        0.0848f,
        0.1227f,
        0.1403f,
        0.1559f,
        0.1850f,
        0.2183f,
        0.2417f,
        0.2571f,
        0.2852f,
        0.2902f,
        0.2919f,
        0.3099f,
        0.3192f,
        0.3232f,
        0.3294f,
        0.3384f,
        0.3609f,
        0.3619f,
        0.3667f,
        0.3737f,
        0.3747f,
        0.3838f,
        0.3921f,
        0.3960f,
        0.3984f,
        0.3993f,
        0.4075f,
        0.4091f,
        0.4101f,
        0.4200f,
        0.4230f,
        0.4247f,
        0.4274f,
        0.4293f,
        0.4328f,
        0.4382f,
        0.4385f,
        0.4420f,
        0.4473f,
        0.4477f,
        0.4503f,
        0.4562f,
        0.4580f,
        0.4610f,
        0.4638f,
        0.4667f,
        0.4686f,
        0.4693f,
        0.4703f,
        0.4833f,
        0.4881f,
        0.4944f,
        0.4953f,
        0.4992f,
        0.5509f,
        0.5567f,
        0.5569f,
        0.5591f,
        0.5602f,
        0.5602f,
        0.5650f,
        0.5776f,
        0.5777f,
        0.5818f,
        0.5870f,
        0.5972f,
        0.5999f,
        0.6043f,
        0.6049f,
        0.6093f,
        0.6099f,
        0.6465f,
        0.6561f,
        0.6595f,
        0.6631f,
        0.6714f,
        0.6759f,
        0.6809f,
        0.6816f,
        0.6925f,
        0.7039f,
        0.7086f,
        0.7235f,
        0.7302f,
        0.7332f,
        0.7602f,
        0.7834f,
        0.8037f,
        0.9999f,
    };

    public static readonly SideCharacters<char> BoxSides = new('┌', '┐', '┘', '└', '─', '│');
    public static readonly SideCharacters<char> PanelSides = new('╒', '═', '╕', '│', '┘', '─', '└', '│');
    public static readonly SideCharacters<char> BoxSidesDouble = new('╔', '╗', '╝', '╚', '═', '║');
    public static readonly SideCharacters<char> BoxSidesShadow = new('┌', '─', '╖', '║', '╝', '═', '╘', '│');

    public static readonly char[] Stars = new char[]
    {
        '\'',
        '*',
        '¤',
        '✶',
    };

    static readonly char[] LineSegment = new char[]
    {
        '^',

        '/',
        '⁄',

        '>',

        '\\',
        '\\',

        '|',

        '/',
        '⁄',

        '<',

        '\\',
        '\\',
    };

    public static readonly char[] HorizontalLines = new char[]
    {
        '‐',
        '‒',
        '‒',
        '–',
        '—',
        '―',
    };

    public static readonly char[] Loading = new char[]
    {
        '|',
        '/',
        '-',
        '\\',
    };

    public static readonly char[] Stuff1 = "¨`¯´˚˙˘˜˝΄΅·´῾῭΅`῝῞῟῁῀᾿᾽῏῎῍՚՛՜՝՞՟‘’‛“”‟\"'".ToCharArray();

    public static readonly Dictionary<char, char> UpperIndex = new() {
        { '0', (char)0x2070 },
        { '1', (char)0x00b9 },
        { '2', (char)0x00b2 },
        { '3', (char)0x00b3 },
        { '4', (char)0x2074 },
        { '5', (char)0x2075 },
        { '6', (char)0x2076 },
        { '7', (char)0x2077 },
        { '8', (char)0x2078 },
        { '9', (char)0x2079 },

        { '+', (char)0x207a },
        { '-', (char)0x207b },
        { '=', (char)0x207c },
        { '(', (char)0x207d },
        { ')', (char)0x207e },
        { '~', (char)0x02dc },

        { 'a', (char)0x0363 },
        // { 'a', (char)0x1d43 },
        { 'b', (char)0x1d47 },
        { 'c', (char)0x0368 },
        // { 'd', (char)0x1d48 },
        { 'd', (char)0x0369 },
        // { 'e', (char)0x1d49 },
        { 'e', (char)0x0364 },
        { 'g', (char)0x1d4d },
        // { 'h', (char)0x036a },
        { 'h', (char)0x02b0 },
        // { 'i', (char)0x0365 },
        { 'i', (char)0x2071 },
        { 'j', (char)0x02b2 },
        { 'k', (char)0x1d4f },
        { 'l', (char)0x02e1 },
        // { 'm', (char)0x1d50 },
        { 'm', (char)0x036b },
        { 'n', (char)0x207f },
        // { 'o', (char)0x1d52 },
        { 'o', (char)0x0366 },
        { 'p', (char)0x1d56 },
        // { 'r', (char)0x036c },
        { 'r', (char)0x02b3 },
        { 's', (char)0x02e2 },
        // { 't', (char)0x1d57 },
        { 't', (char)0x036d },
        // { 'u', (char)0x1d58 },
        { 'u', (char)0x0367 },
        // { 'v', (char)0x1d5b },
        { 'v', (char)0x036e },
        { 'w', (char)0x02b7 },
        // { 'x', (char)0x036f },
        { 'x', (char)0x02e3 },
        { 'y', (char)0x02b8 },

        { 'A', (char)0x1d2c },
        { 'B', (char)0x1d2e },
        { 'D', (char)0x1d30 },
        { 'E', (char)0x1d31 },
        { 'G', (char)0x1d33 },
        { 'H', (char)0x1d34 },
        { 'I', (char)0x1d35 },
        { 'J', (char)0x1d36 },
        { 'K', (char)0x1d37 },
        { 'L', (char)0x1d38 },
        { 'M', (char)0x1d39 },
        { 'N', (char)0x1d3a },
        { 'O', (char)0x1d3c },
        { 'P', (char)0x1d3e },
        { 'R', (char)0x1d3f },
        { 'T', (char)0x1d40 },
        { 'U', (char)0x1d41 },
        { 'W', (char)0x1d42 },
    };

    public static string ToUpperIndex(string value)
    {
        Ascii.ToUpperIndex(value, out string result);
        return result;
    }
    public static bool ToUpperIndex(string value, out string result)
    {
        Span<char> _result = stackalloc char[value.Length];
        bool success = true;
        for (int i = 0; i < value.Length; i++)
        {
            if (UpperIndex.TryGetValue(value[i], out char upperIndex))
            {
                _result[i] = upperIndex;
            }
            else
            {
                _result[i] = value[i];
                success = false;
            }
        }

        result = new string(_result);
        return success;
    }

    // public static char DirectionLine(Vector dir)
    //     => DirectionLine(Vector.ToDeg(dir));
    // public static char DirectionLine(float deg)
    // {
    //     deg += 90f + 10f;
    //     Vector.ClampAngle(ref deg);
    //     deg /= 360f;
    // 
    //     int i = (int)MathF.Round(deg * (LineSegment.Length - 1));
    // 
    //     if (i < 0) i += (LineSegment.Length - 1);
    //     if (i >= LineSegment.Length) i -= (LineSegment.Length - 1);
    // 
    //     return LineSegment[i];
    // }
}
