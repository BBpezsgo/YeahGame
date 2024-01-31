using Win32.Gdi32;

namespace YeahGame;

public ref struct ParticlesSpawnConfig
{
    public Vector2 InitialLocalDirection;
    public Interval<float> InitialLocalVelocity;
    public Interval<float> Spread;

    public readonly (Vector2 Position, Vector2 Velocity) Calc(Random random)
    {
        Vector2 position = default;
        Vector2 velocity = InitialLocalDirection * random.Next(Interval.GetFixed(InitialLocalVelocity));

        if (Spread != 0f)
        {
            float spread = random.Next(Interval.GetFixed(Spread));
            velocity.X = velocity.X * MathF.Cos(spread) - velocity.Y * MathF.Sin(spread);
            velocity.Y = velocity.X * MathF.Sin(spread) + velocity.Y * MathF.Cos(spread);
        }

        return (position, velocity);
    }
}

public ref struct ParticlesConfig
{
    public Interval<int> ParticleCount;
    public ReadOnlySpan<char> Characters;
    public Interval<GdiColor> Color;
    public Interval<float> Lifetime;
    public float Damp;
    public ParticlesSpawnConfig SpawnConfig;
}

public class Particles : Entity
{
    struct Particle
    {
        public bool IsAlive;
        public Vector2 LocalPosition;
        public Vector2 LocalVelocity;
        public float BornAt;
        public float LifeTime;
    }

    readonly Particle[] _particles;
    readonly char[] _characters;
    readonly Interval<GdiColor> _color;
    readonly float _damp;

    public Particles(ParticlesConfig config, Random random)
    {
        IsSolid = false;

        _particles = new Particle[Math.Max(0, random.Next(Interval.GetFixed(config.ParticleCount)))];
        _characters = config.Characters.ToArray();
        _color = config.Color;
        _damp = config.Damp;

        for (int i = 0; i < _particles.Length; i++)
        {
            ref Particle particle = ref _particles[i];
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
        for (int i = 0; i < _particles.Length; i++)
        {
            ref Particle particle = ref _particles[i];
            if (!particle.IsAlive) continue;

            Coord position = (particle.LocalPosition + Position).Round();
            if (!Game.Renderer.IsVisible(position)) continue;

            float t = (Time.Now - particle.BornAt) / particle.LifeTime;
            if (t < 0f || t >= 1f) continue;

            char character = _characters[(int)(t * _characters.Length)];
            GdiColor color = (_color.Min * (1f - t)) + (_color.Max * t);
            byte charColor = CharColor.To4bitIRGB(color);

            Game.Renderer[position] = new ConsoleChar(character, charColor, CharColor.Black);
        }
    }

    public override void Update()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            ref Particle particle = ref _particles[i];
            if (!particle.IsAlive) continue;

            if (Time.Now - particle.BornAt >= particle.LifeTime)
            {
                particle.IsAlive = false;
                continue;
            }

            particle.LocalVelocity *= _damp;
            particle.LocalPosition += particle.LocalVelocity * Time.Delta;
        }
    }
}
