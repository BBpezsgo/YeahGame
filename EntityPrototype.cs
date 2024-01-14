namespace YeahGame;

public enum EntityPrototype : byte
{
    Player,
}

public static partial class Utils
{
    public static EntityPrototype GetEntityPrototype<T>()
    {
        if (typeof(T) == typeof(Player)) return EntityPrototype.Player;

        throw new NotImplementedException();
    }
}