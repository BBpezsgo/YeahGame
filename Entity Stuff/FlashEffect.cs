namespace YeahGame;

public class FlashEffect : Entity
{
    const int Size = 3;
    const float MaxLifetime = .2f;

    readonly float CreatedAt;

    public FlashEffect()
    {
        IsSolid = false;
        CreatedAt = Time.Now;
    }

    public override void Update()
    {
        if (Time.Now - CreatedAt >= MaxLifetime)
        {
            DoesExist = false;
        }
    }

    public override void Render()
    {
        float lifetime = Math.Clamp((Time.Now - CreatedAt) / MaxLifetime, 0f, 1f);

        float width = Size * (1f - lifetime);
        float height = Size * lifetime;

        for (float x = -width; x <= width; x++)
        {
            float v = width - Math.Abs(x);
            v *= v;
            int _v = (int)v;

            for (int y = -_v; y < _v; y++)
            {
                Vector2 point = new(Position.X + x, Position.Y + y);
                if (Game.Renderer.IsVisible(point))
                { Game.Renderer[point] = new ConsoleChar(Ascii.BlockShade[2], CharColor.White); }
            }
        }

        for (float y = -height; y < height; y++)
        {
            float v = height - Math.Abs(y);
            v *= v;
            int _v = (int)v;

            for (int x = -_v; x <= _v; x++)
            {
                Vector2 point = new(Position.X + x, Position.Y + y);
                if (Game.Renderer.IsVisible(point))
                { Game.Renderer[point] = new ConsoleChar(Ascii.BlockShade[2], CharColor.White); }
            }
        }
    }
}
