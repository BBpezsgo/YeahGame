using Win32.Gdi32;

namespace YeahGame;

public static class ParticleConfigs
{
    public static ParticlesConfig GetShoot(Vector2 direction) => new()
    {
        Characters = "\"'`¨˟˖",
        Color = (GdiColor.White, GdiColor.Yellow),
        Damp = .999f,
        Lifetime = (.1f, .4f),
        ParticleCount = (2, 4),
        SpawnConfig = new ParticlesSpawnConfig()
        {
            InitialLocalDirection = direction,
            InitialLocalVelocity = (4.5f, 15f),
            Spread = (-(Utils.Deg2Rad * 30f), +(Utils.Deg2Rad * 30f)),
        },
    };

    public static ParticlesConfig GetDeath(GdiColor color) => new()
    {
        Characters = "ᴼᵒ◦. ",
        Color = (color, GdiColor.Black),
        Damp = .997f,
        Lifetime = (3, 6),
        ParticleCount = (5, 20),
        SpawnConfig = new ParticlesSpawnConfig()
        {
            InitialLocalDirection = Vector2.One,
            InitialLocalVelocity = (5f, 15f),
            Spread = (0f, 360f),
        },
    };

    public static ParticlesConfig GetImpact(Vector2 direction) => new()
    {
        Characters = ",.",
        Color = new GdiColor(255, 255, 255),
        Damp = 1f,
        Lifetime = .4f,
        ParticleCount = (2, 4),
        SpawnConfig = new ParticlesSpawnConfig()
        {
            InitialLocalDirection = direction,
            InitialLocalVelocity = (4.5f, 15f),
            Spread = (-(Utils.Deg2Rad * 10f), +(Utils.Deg2Rad * 10f)),
        },
    };
}
