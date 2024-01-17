namespace YeahGame;

public abstract class Scene
{
    public bool IsLoaded { get; private set; }
    public abstract string Name { get; }

    public virtual void Load()
    {
        IsLoaded = true;
    }

    public virtual void Unload()
    {
        IsLoaded = false;
    }

    public abstract void Tick();

    public abstract void Render();
}
