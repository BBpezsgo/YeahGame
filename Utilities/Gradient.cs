using Win32.Gdi32;

namespace YeahGame;

public readonly struct Gradient
{
    readonly GdiColor[] Colors;

    public Gradient(params GdiColor[] colors)
    {
        Colors = colors;
    }

    public GdiColor Get(float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        float valueRatio = t * Colors.Length;

        int stopIndex = (int)valueRatio;
        if (stopIndex + 1 >= Colors.Length) return Colors[^1];

        return Lerp(Colors[stopIndex], Colors[stopIndex + 1], valueRatio % 1f);
    }

    static GdiColor Lerp(GdiColor pointA, GdiColor pointB, float normalValue) => pointA + ((pointB - pointA) * normalValue);
}
