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
}
