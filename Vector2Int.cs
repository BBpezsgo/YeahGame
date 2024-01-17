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
    public static explicit operator Vector2Int(Vector2 v) => new((int)v.X, (int)v.Y);
}
