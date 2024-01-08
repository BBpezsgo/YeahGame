namespace YeahGame;

public static class Utils
{
    public static byte[] Serialize<T>(T data) where T : ISerializable<T>
    {
        using MemoryStream stream = new();
        BinaryWriter writer = new(stream);
        data.Serialize(writer);
        writer.Flush();
        writer.Close();
        return stream.ToArray();
    }
}
