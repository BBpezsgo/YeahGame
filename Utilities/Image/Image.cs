using System.Diagnostics.CodeAnalysis;

namespace YeahGame;

using Color = (byte R, byte G, byte B);
using TransparentColor = (byte R, byte G, byte B, byte A);

public readonly struct Image
{
    public readonly Color[] Data;
    public readonly int Width;
    public readonly int Height;

    public Color this[int x, int y] => Data[x + (Width * y)];
    public Color this[Coord point] => Data[point.X + (Width * point.Y)];

    public Color GetPixelWithUV(Vector2 uv, Vector2 point)
    {
        Vector2 transformedPoint = point / uv;
        transformedPoint *= new Vector2(Width, Height);
        Coord imageCoord = new((int)transformedPoint.X, (int)transformedPoint.Y);
        return this[imageCoord];
    }

    public Image(Color[] data, int width, int height)
    {
        Data = data;
        Width = width;
        Height = height;
    }

    public Image(TransparentColor[] data, int width, int height)
    {
        Data = new Color[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            Data[i] = new Color(data[i].R, data[i].G, data[i].B);
        }
        Width = width;
        Height = height;
    }

    public Image Duplicate()
    {
        Color[] data = new Color[Data.Length];
        Array.Copy(Data, data, Data.Length);
        return new Image(data, Width, Height);
    }

    public Color NormalizedSample(float texU, float texV)
    {
        int x = (int)(texU * Width);
        int y = (int)(texV * Height);

        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);

        return this[x, y];
    }

    [return: NotNullIfNotNull(nameof(imgFile))]
    public static Image? LoadFile(string? imgFile, Color background)
    {
        if (imgFile == null) return null;
        string extension = Path.GetExtension(imgFile);
        if (extension.Length <= 1) throw new NotImplementedException();
        extension = extension.ToLowerInvariant();
        return extension switch
        {
            ".png" => Png.LoadFile(imgFile, background),
            ".ppm" => Ppm.LoadFile(imgFile),
            _ => throw new NotImplementedException($"Unknown image file extension \"{extension}\""),
        };
    }

    [return: NotNullIfNotNull(nameof(imgFile))]
    public static TransparentImage? LoadFile(string? imgFile)
    {
        if (imgFile == null) return null;
        string extension = Path.GetExtension(imgFile);
        if (extension.Length <= 1) throw new NotImplementedException();
        extension = extension.ToLowerInvariant();
        return extension switch
        {
            ".png" => Png.LoadFile(imgFile),
            ".ppm" => (TransparentImage)Ppm.LoadFile(imgFile),
            _ => throw new NotImplementedException($"Unknown image file extension \"{extension}\""),
        };
    }

    public static explicit operator ConsoleImage(Image image)
    {
        ConsoleChar[] data = new ConsoleChar[image.Width * image.Height];

        for (int i = 0; i < data.Length; i++)
        {
            (byte r, byte g, byte b) = image.Data[i];
            data[i] = new ConsoleChar(' ', 0, CharColor.To4bitIRGB(System.Drawing.Color.FromArgb(r, g, b)));
        }

        return new ConsoleImage(data, (short)image.Width, (short)image.Height);
    }
}
