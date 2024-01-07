namespace YeahGame;

public static class Time
{
    static float lastTime;
    static float now;
    static float deltaTime;

    public static float Now => now;
    public static float Delta => deltaTime;

    public static void Tick()
    {
        now = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
        deltaTime = now - lastTime;
        lastTime = now;
    }
}
