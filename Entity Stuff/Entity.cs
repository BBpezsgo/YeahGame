namespace YeahGame;

public abstract class Entity
{
    public Vector2 Position;
    public bool DoesExist = true;
    public bool IsSolid = true;

    public abstract void Update();
    public abstract void Render();

    public virtual void OnDestroy() { }
}
