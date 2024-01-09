namespace YeahGame;

public static class Time
{
    static double lastTime;
    static double now;
    static double deltaTime;

    public static float Now => (float)now;
    public static float Delta => (float)deltaTime;

    public static double NowNoCache => DateTime.UtcNow.TimeOfDay.TotalSeconds;

    public static void Tick()
    {
        now = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        deltaTime = now - lastTime;
        lastTime = now;
    }
}
