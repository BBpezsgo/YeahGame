using System.Diagnostics.CodeAnalysis;

namespace YeahGame;

public static class Utils
{
    public static byte[] Serialize<T>(T data) where T : ISerializable
    {
        using MemoryStream stream = new();
        BinaryWriter writer = new(stream);
        data.Serialize(writer);
        writer.Flush();
        writer.Close();
        return stream.ToArray();
    }

    public static T Deserialize<T>(T data, byte[] buffer) where T : ISerializable
    {
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        data.Deserialize(reader);
        return data;
    }

    public static T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(byte[] buffer) where T : ISerializable
        => Utils.Deserialize(Activator.CreateInstance<T>(), buffer);
}
