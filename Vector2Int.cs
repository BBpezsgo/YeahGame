namespace YeahGame;

public struct Vector2Int
{
    public int X;
    public int Y;

    public Vector2Int(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static implicit operator Vector2(Vector2Int v) => new(v.X, v.Y);
    public static implicit operator Coord(Vector2Int v) => new(v.X, v.Y);
    public static implicit operator Win32.Common.Point(Vector2Int v) => new(v.X, v.Y);

    public static explicit operator Vector2Int(Vector2 v) => new((int)v.X, (int)v.Y);

    public static Vector2Int operator +(Vector2Int a, Vector2Int b)
    {
        a.X += b.X;
        a.Y += b.Y;
        return a;
    }

    public static Vector2Int operator -(Vector2Int a, Vector2Int b)
    {
        a.X -= b.X;
        a.Y -= b.Y;
        return a;
    }
}

public static class VectorExtensions
{
    public static Vector2Int Round(this Vector2 vector) => new((int)MathF.Round(vector.X), (int)MathF.Round(vector.Y));
}
