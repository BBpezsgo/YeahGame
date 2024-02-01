using System.Diagnostics.CodeAnalysis;
using Win32.Gdi32;

namespace YeahGame;

public static partial class Utils
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    public const bool IsDebug =
#if DEBUG
        true;
#else
        false;
#endif

    public static readonly Random Random = new(69);

    public static Vector2 RotateVectorDeg(Vector2 vector, float deg)
        => RotateVector(vector, deg * Utils.Deg2Rad);
    public static Vector2 RotateVector(Vector2 vector, float rad)
    {
        vector.X = vector.X * MathF.Cos(rad) - vector.Y * MathF.Sin(rad);
        vector.Y = vector.X * MathF.Sin(rad) + vector.Y * MathF.Cos(rad);
        return vector;
    }

    public static void FastBlur(Span2D<GdiColor> img, int radius)
    {
        if (radius < 1) { return; }

        int w = img.Width;
        int h = img.Height;
        int wm = w - 1;
        int hm = h - 1;
        int wh = w * h;
        int div = radius + radius + 1;
        int[] r = new int[wh];
        int[] g = new int[wh];
        int[] b = new int[wh];
        int rsum, gsum, bsum, x, y, i, yp, yi, yw;
        GdiColor p, p1, p2;
        int[] vmin = new int[Math.Max(w, h)];
        int[] vmax = new int[Math.Max(w, h)];

        int[] dv = new int[256 * div];
        for (i = 0; i < 256 * div; i++){
            dv[i] = i / div;
        }

        yw = yi = 0;

        for (y = 0; y < h; y++)
        {
            rsum = gsum = bsum = 0;
            for(i = -radius; i <= radius; i++)
            {
                p = img.Span[yi + Math.Min(wm, Math.Max(i, 0))];
                rsum += p.R;
                gsum += p.G;
                bsum += p.B;
            }
            for (x = 0; x < w; x++)
            {
                r[yi] = dv[rsum];
                g[yi] = dv[gsum];
                b[yi] = dv[bsum];

                if ( y == 0)
                {
                    vmin[x] = Math.Min( x + radius + 1, wm);
                    vmax[x] = Math.Max( x - radius, 0);
                }
                p1 = img.Span[yw + vmin[x]];
                p2 = img.Span[yw + vmax[x]];

                rsum += p1.R - p2.R;
                gsum += p1.G - p2.G;
                bsum += p1.B - p2.B;
                yi++;
            }
            yw += w;
        }

        for (x = 0; x < w; x++)
        {
            rsum = gsum = bsum = 0;
            yp = -radius * w;
            for(i = -radius; i <= radius; i++)
            {
                yi = Math.Max(0, yp) + x;
                rsum += r[yi];
                gsum += g[yi];
                bsum += b[yi];
                yp += w;
            }
            yi = x;
            for (y = 0; y < h; y++)
            {
                img.Span[yi] = (GdiColor)unchecked(((uint)0xff000000 | ((uint)dv[rsum]<<16) | ((uint)dv[gsum]<<8) |(uint) dv[bsum]));
                if(x == 0)
                {
                    vmin[y]=Math.Min(y+radius+1,hm)*w;
                    vmax[y]=Math.Max(y-radius,0)*w;
                }
                p1 = unchecked((uint)(x + vmin[y]));
                p2 = unchecked((uint)(x + vmax[y]));

                rsum+=r[p1]-r[p2];
                gsum+=g[p1]-g[p2];
                bsum+=b[p1]-b[p2];

                yi+=w;
            }
        }
    }

    public static string FormatMemorySize(int byteCount)
    {
        string postfix = "bytes";
        float value = byteCount;

        if (value > 512)
        {
            value /= 1024f;
            postfix = "kb";
        }

        if (value > 512)
        {
            value /= 1024f;
            postfix = "mb";
        }

        if (value > 512)
        {
            value /= 1024f;
            postfix = "gb";
        }

        return $"{value:0.#} {postfix}";
    }

    public static byte[] Serialize<T>(T data)
        where T : ISerializable
        => Serialize(data.Serialize);

    public static byte[] Serialize(Action<BinaryWriter> serializer)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        serializer.Invoke(writer);
        writer.Flush();
        writer.Close();
        return stream.ToArray();
    }

    public static T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(byte[] buffer)
        where T : ISerializable
    {
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        T data = Activator.CreateInstance<T>();
        data.Deserialize(reader);
        return data;
    }

    public static T Deserialize<T>(T data, byte[] buffer)
        where T : ISerializable
    {
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        data.Deserialize(reader);
        return data;
    }

    public static void Deserialize(byte[] buffer, Action<BinaryReader> deserializer)
    {
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        deserializer.Invoke(reader);
    }

    /// <summary>
    /// Source: <see href="https://stackoverflow.com/a/3122532"/>
    /// </summary>
    public static Vector2 Point2LineDistance(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 a2p = p - a;
        Vector2 a2b = b - a;
        float atb2 = a2b.LengthSquared();
        float atp_dot_atb = Vector2.Dot(a2p, a2b);
        float t = atp_dot_atb / atb2;
        return a + (a2b * t);
    }

    /// <summary>
    /// Source: <see href="https://www.youtube.com/watch?v=NbSee-XM7WA">javidx9</see>
    /// </summary>
    public static bool TilemapRaycast(Vector2 rayStart, Vector2 rayDir, float maxDistance, out Vector2 intersection, Func<int, int, bool> checker)
    {
        Vector2 rayUnitStepSize = new(
            MathF.Sqrt(1f + (rayDir.Y / rayDir.X) * (rayDir.Y / rayDir.X)),
            MathF.Sqrt(1f + (rayDir.X / rayDir.Y) * (rayDir.X / rayDir.Y))
            );

        Vector2Int mapCheck = (Vector2Int)rayStart;
        Vector2 rayLength1D;

        Vector2Int step;

        if (rayDir.X < 0)
        {
            step.X = -1;
            rayLength1D.X = (rayStart.X - mapCheck.X) * rayUnitStepSize.X;
        }
        else
        {
            step.X = 1;
            rayLength1D.X = (mapCheck.X + 1 - rayStart.X) * rayUnitStepSize.X;
        }

        if (rayDir.Y < 0)
        {
            step.Y = -1;
            rayLength1D.Y = (rayStart.Y - mapCheck.Y) * rayUnitStepSize.Y;
        }
        else
        {
            step.Y = 1;
            rayLength1D.Y = (mapCheck.Y + 1 - rayStart.Y) * rayUnitStepSize.Y;
        }

        bool tileFound = false;
        float distance = 0f;
        while (!tileFound && distance < maxDistance)
        {
            if (rayLength1D.X < rayLength1D.Y)
            {
                mapCheck.X += step.X;
                distance = rayLength1D.X;
                rayLength1D.X += rayUnitStepSize.X;
            }
            else
            {
                mapCheck.Y += step.Y;
                distance = rayLength1D.Y;
                rayLength1D.Y += rayUnitStepSize.Y;
            }

            if (checker.Invoke(mapCheck.X, mapCheck.Y))
            {
                tileFound = true;
            }
        }

        if (tileFound)
        { intersection = rayStart + rayDir * distance; }
        else
        { intersection = default; }

        return tileFound;
    }

    public static bool[] LoadMap(string str, out int width)
    {
        width = -1;
        int y = 0;
        List<bool> row = new();
        List<bool> res = new();
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c == '\r') continue;
            if (c == '\n')
            {
                res.AddRange(row);

                if (width != -1 && width != row.Count)
                { throw new NotImplementedException(); }

                width = row.Count;
                row.Clear();
                y++;
                continue;
            }
            row.Add(c != ' ');
        }

        if (row.Count > 0)
        {
            res.AddRange(row);

            if (width != -1 && width != row.Count)
            { throw new NotImplementedException(); }
        }

        return res.ToArray();
    }
}
