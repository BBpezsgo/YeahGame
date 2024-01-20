namespace YeahGame;

public static class Time
{
    const double TargetFPS = 30;

    static double lastTime;
    static double now;
    static double deltaTime;

    /// <summary>
    /// Elapsed seconds since midnight at the start of the tick
    /// </summary>
    public static float Now => (float)now;

    /// <summary>
    /// Elapsed seconds since the last tick
    /// </summary>
    public static float Delta => (float)deltaTime;

    /// <summary>
    /// Elapsed seconds since midnight
    /// </summary>
    public static double NowNoCache => DateTime.UtcNow.TimeOfDay.TotalSeconds;

    /// <summary>
    /// Frames per second
    /// </summary>
    public static float FPS => 1f / (float)deltaTime;

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
