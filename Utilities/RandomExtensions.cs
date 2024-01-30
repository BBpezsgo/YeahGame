namespace YeahGame;

public static class RandomExtensions
{
    public static float NextSingle(this Random random, float min, float max)
        => (random.NextSingle() * (max - min)) + min;

    public static Vector2 NextVector2(this Random random, float maxWidth, float maxHeight) => new(
        (random.NextSingle() - .5f) * maxWidth,
        (random.NextSingle() - .5f) * maxHeight);

    public static Vector2 NextVector2(this Random random, Vector2 min, Vector2 max) => new(
        random.NextSingle(min.X, max.X),
        random.NextSingle(min.Y, max.Y));

    public static Vector2 NextDirection(this Random random) => new(
        MathF.Cos(random.NextSingle(0f, MathF.PI * 2f)),
        MathF.Sin(random.NextSingle(0f, MathF.PI * 2f)));

    public static int Next(this Random random, Interval<int> range) => random.Next(range.Min, range.Max);
    public static float Next(this Random random, Interval<float> range) => random.NextSingle(range.Min, range.Max);
    public static long Next(this Random random, Interval<long> range) => random.NextInt64(range.Min, range.Max);
}
