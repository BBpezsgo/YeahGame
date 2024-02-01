using Win32.Gdi32;

namespace YeahGame;

public ref struct GasParticlesSpawnConfig
{
    public Vector2 InitialLocalDirection;
    public Interval<float> InitialLocalVelocity;
    public Interval<float> Spread;

    public readonly (Vector2 Position, Vector2 Velocity) Calc(Random random)
    {
        Vector2 position = default;
        Vector2 velocity = InitialLocalDirection;

        if (velocity == default)
        {
            if (Spread != 0f)
            {
                float spread = random.Next(Interval.GetFixed(Spread));
                velocity.X = MathF.Cos(spread);
                velocity.Y = MathF.Sin(spread);
            }
        }
        else
        {
            if (Spread != 0f)
            {
                float spread = random.Next(Interval.GetFixed(Spread));
                velocity.X = velocity.X * MathF.Cos(spread) - velocity.Y * MathF.Sin(spread);
                velocity.Y = velocity.X * MathF.Sin(spread) + velocity.Y * MathF.Cos(spread);
            }
        }

        velocity *= random.Next(Interval.GetFixed(InitialLocalVelocity));

        return (position, velocity);
    }
}

public ref struct GasParticlesConfig
{
    public Interval<int> ParticleCount;
    public ReadOnlySpan<char> Characters;
    public Gradient Color;
    public Interval<float> Lifetime;
    public float Damp;
    public GasParticlesSpawnConfig SpawnConfig;
}

public class GasParticles : Entity
{
    struct GasParticle
    {
        public bool IsAlive;
        public Vector2 LocalPosition;
        public Vector2 LocalVelocity;
        public float BornAt;
        public float LifeTime;
    }

    readonly GasParticle[] _particles;
    readonly char[] _characters;
    readonly Gradient _color;
    readonly float _damp;

    public GasParticles(GasParticlesConfig config, Random random)
    {
        IsSolid = false;

        _particles = new GasParticle[Math.Max(0, random.Next(Interval.GetFixed(config.ParticleCount)))];
        _characters = config.Characters.ToArray();
        _color = config.Color;
        _damp = config.Damp;

        for (int i = 0; i < _particles.Length; i++)
        {
            ref GasParticle particle = ref _particles[i];
            particle.IsAlive = true;
            particle.BornAt = Time.Now;
            particle.LifeTime = random.Next(Interval.GetFixed(config.Lifetime));
            (Vector2 position, Vector2 velocity) = config.SpawnConfig.Calc(random);
            particle.LocalPosition = position;
            particle.LocalVelocity = velocity;
        }
    }

    public override void Render()
    {
        Span2D<GdiColor> map = new(stackalloc GdiColor[16 * 16], 16, 16);
        Coord mapHalfSize = new Coord(map.Width, map.Height) / 2;

        for (int i = 0; i < _particles.Length; i++)
        {
            ref GasParticle particle = ref _particles[i];
            if (!particle.IsAlive) continue;

            Coord pos = (Coord)(particle.LocalPosition + mapHalfSize);
            if (!map.IsVisible(pos)) continue;

            float t = (Time.Now - particle.BornAt) / particle.LifeTime;
            if (t < 0f || t > 1f) continue;

            GdiColor color = _color.Get(t);
            map[pos.X, pos.Y] += color;
        }

        Utils.FastBlur(map, 3);

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Coord p = (Coord)Position.Round() - mapHalfSize + new Coord(x, y);
                if (!Game.Renderer.IsVisible(p)) continue;

                ref ConsoleChar c = ref Game.Renderer[p];
                c.Background = CharColor.To4bitIRGB(map[x, y]);
            }
        }
    }

    public override void Update()
    {
        bool hasAlive = false;
        for (int i = 0; i < _particles.Length; i++)
        {
            ref GasParticle particle = ref _particles[i];
            if (!particle.IsAlive) continue;

            if (Time.Now - particle.BornAt >= particle.LifeTime)
            {
                particle.IsAlive = false;
                continue;
            }

            hasAlive = true;

            particle.LocalVelocity *= _damp;
            particle.LocalPosition += particle.LocalVelocity * Time.Delta;
        }

        if (!hasAlive) DoesExist = false;
    }
}
