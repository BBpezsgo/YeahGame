namespace YeahGame;

public static partial class Utils
{
    public static byte[] Serialize<T>(T data)
        where T : ISerializable
        => Serialize(data.Serialize);

    public static byte[] Serialize(Action<BinaryWriter> serializer)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        serializer.Invoke(writer);
        writer.Flush();
        writer.Close();
        return stream.ToArray();
    }

    public static T Deserialize<T>(T data, byte[] buffer)
        where T : ISerializable
    {
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        data.Deserialize(reader);
        return data;
    }

    public static void Deserialize(byte[] buffer, Action<BinaryReader> deserializer)
    {
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        deserializer.Invoke(reader);
    }
}

public static class Extensions
{
    public static void UpdateAll<T>(this List<T> entities)
        where T : Entity
    {
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            T player = entities[i];
            player.Update();
            if (player.DoesExist != true)
            {
                entities.RemoveAt(i);
            }
        }
    }

    public static void RenderAll<T>(this List<T> entities)
        where T : Entity
    {
        for (int i = 0; i < entities.Count; i++)
        { entities[i].Render(); }
    }
}
