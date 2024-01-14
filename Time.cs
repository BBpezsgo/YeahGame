namespace YeahGame;

public static class Time
{
    const double TargetFPS = 30;

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

        double targetDeltaTime = 1d / TargetFPS;
        if (deltaTime < targetDeltaTime)
        { Thread.Sleep((int)((targetDeltaTime - deltaTime) * 1000d)); }
    }
}
