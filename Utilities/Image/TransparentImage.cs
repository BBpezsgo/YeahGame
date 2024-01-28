namespace YeahGame;

using Color = (byte R, byte G, byte B);
using TransparentColor = (byte R, byte G, byte B, byte A);

public readonly struct TransparentImage
{
    public readonly TransparentColor[] Data;
    public readonly int Width;
    public readonly int Height;

    public TransparentColor this[int x, int y] => Data[x + (Width * y)];
    public TransparentColor this[Coord point] => Data[point.X + (Width * point.Y)];

    public TransparentColor GetPixelWithUV(Vector2 uv, Vector2 point)
    {
        Vector2 transformedPoint = point / uv;
        transformedPoint *= new Vector2(Width, Height);
        Coord imageCoord = new((int)transformedPoint.X, (int)transformedPoint.Y);
        return this[imageCoord];
    }

    public TransparentImage(TransparentColor[] data, int width, int height)
    {
        Data = data;
        Width = width;
        Height = height;
    }

    public TransparentImage(Color[] data, int width, int height)
    {
        Data = new TransparentColor[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            Data[i] = new TransparentColor(data[i].R, data[i].G, data[i].B, byte.MaxValue);
        }
        Width = width;
        Height = height;
    }

    public static explicit operator TransparentImage(Image image) => new(image.Data, image.Width, image.Height);
    public static explicit operator Image(TransparentImage image) => new(image.Data, image.Width, image.Height);

    public TransparentImage Duplicate()
    {
        TransparentColor[] data = new TransparentColor[Data.Length];
        Array.Copy(Data, data, Data.Length);
        return new TransparentImage(data, Width, Height);
    }
}
