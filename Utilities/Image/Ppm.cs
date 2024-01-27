using System.Text;

namespace YeahGame;

using Color = (byte R, byte G, byte B);
using TransparentColor = (byte R, byte G, byte B, byte A);

public static class Ppm
{
    static readonly char[] Whitespace = new char[] { '\r', '\n', '\t', ' ' };

    static void ConsumeAll(string text, ref int i, params char[] v)
    {
        while (Consume(text, ref i, v)) ;
    }

    static bool Consume(string text, ref int i, params char[] v)
    {
        if (i >= text.Length) return false;
        for (int j = 0; j < v.Length; j++)
        {
            if (text[i] == v[j])
            {
                i++;
                return true;
            }
        }
        return false;
    }

    static bool Consume(string text, ref int i, char v)
    {
        if (i >= text.Length) return false;
        if (text[i] == v)
        {
            i++;
            return true;
        }
        return false;
    }

    static string? ConsumeUntil(string text, ref int i, params char[] v)
    {
        int i_ = text.Length - 1;
        for (int j = 0; j < v.Length; j++)
        {
            if (text.IndexOf(v[j], i) != -1)
            {
                i_ = Math.Min(i_, text.IndexOf(v[j], i));
            }
        }
        if (i_ < 0) return null;
        string result = text[i..i_];
        i = i_;
        return result;
    }

    static string? ConsumeUntil(string text, ref int i, char v)
    {
        int i_ = text.IndexOf(v);
        if (i_ < 0) return null;
        string result = text[i..i_];
        i = i_;
        return result;
    }

    static void ConsumeJunk(string text, ref int i)
    {
        ConsumeAll(text, ref i, Whitespace);
        if (i >= text.Length) return;
        if (text[i] == '#')
        {
            ConsumeUntil(text, ref i, '\r', '\n');
            ConsumeAll(text, ref i, Whitespace);
        }
    }

    static int? ExpectInt(string text, ref int i)
    {
        string? v = ConsumeUntil(text, ref i, Whitespace);
        if (v == null) return null;
        if (!int.TryParse(v, out int result)) return null;
        return result;
    }

    /// <exception cref="FileNotFoundException"/>
    /// <exception cref="FormatException"/>
    /// <exception cref="Exception"/>
    public static Image LoadFile(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException($"File not found", file);
        string data = File.ReadAllText(file, Encoding.ASCII);
        int i = 0;

        string? magicNumber = ConsumeUntil(data, ref i, Whitespace);
        ConsumeJunk(data, ref i);
        int? width = ExpectInt(data, ref i);
        ConsumeJunk(data, ref i);
        int? height = ExpectInt(data, ref i);
        ConsumeJunk(data, ref i);
        int? maxRgbValue = ExpectInt(data, ref i) ?? 255;
        ConsumeJunk(data, ref i);

        if (!width.HasValue) throw new FormatException($"Invalid width value");
        if (!height.HasValue) throw new FormatException($"Invalid height value");
        if (maxRgbValue == 0) throw new Exception($"Invalid maxRgbValue value {maxRgbValue}");

        int width_ = width.Value;
        int height_ = height.Value;
        int maxRgbValue_ = maxRgbValue.Value;

        Color[] result = new Color[width_ * height_];

        for (int j = 0; j < result.Length; j++)
        {
            int? r = ExpectInt(data, ref i);
            ConsumeJunk(data, ref i);
            int? g = ExpectInt(data, ref i);
            ConsumeJunk(data, ref i);
            int? b = ExpectInt(data, ref i);
            ConsumeJunk(data, ref i);

            if (!r.HasValue) continue;
            if (!g.HasValue) continue;
            if (!b.HasValue) continue;

            int r_ = Math.Clamp(r.Value, 0, maxRgbValue_);
            int g_ = Math.Clamp(g.Value, 0, maxRgbValue_);
            int b_ = Math.Clamp(b.Value, 0, maxRgbValue_);

            result[j] = new Color((byte)(((float)r_ / (float)maxRgbValue_) * byte.MaxValue), (byte)(((float)g_ / (float)maxRgbValue_) * byte.MaxValue), (byte)(((float)b_ / (float)maxRgbValue_) * byte.MaxValue));
        }

        return new Image(result, width_, height_);
    }

    public static void SaveFile(Image? image, string file)
    {
        if (!image.HasValue) return;
        Image image_ = image.Value;

        const char EOL = '\n';

        System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;

        StringBuilder builder = new();
        builder.Append("P3");
        builder.Append(EOL);

        builder.Append(image_.Width.ToString(ic));
        builder.Append(EOL);

        builder.Append(image_.Height.ToString(ic));
        builder.Append(EOL);

        builder.Append(byte.MaxValue.ToString(ic));
        builder.Append(EOL);

        for (int y = 0; y < image_.Height; y++)
        {
            for (int x = 0; x < image_.Width; x++)
            {
                (byte r, byte g, byte b) = image_[x, y];

                builder.Append(r.ToString(ic));
                builder.Append(' ');
                builder.Append(g.ToString(ic));
                builder.Append(' ');
                builder.Append(b.ToString(ic));
                builder.Append(EOL);
            }
        }

        File.WriteAllText(file, builder.ToString(), Encoding.ASCII);
    }
}
