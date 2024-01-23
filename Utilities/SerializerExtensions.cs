using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace YeahGame;

public static class SerializerExtensions
{
    public static void Write(this BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    public static void Write<T>(this BinaryWriter _writer, IReadOnlyCollection<T> values, Action<BinaryWriter, T> writer)
    {
        _writer.Write(values.Count);
        foreach (T item in values)
        { writer.Invoke(_writer, item); }
    }

    public static void Write<T>(this BinaryWriter _writer, IReadOnlyCollection<T> values, Action<T> writer)
    {
        _writer.Write(values.Count);
        foreach (T item in values)
        { writer.Invoke(item); }
    }

    public static void Write<T>(this BinaryWriter writer, IReadOnlyCollection<T> values) where T : ISerializable
    {
        writer.Write(values.Count);
        foreach (T item in values)
        { item.Serialize(writer); }
    }

    public static Vector2 ReadVector2(this BinaryReader reader)
    {
        Vector2 value = default;
        value.X = reader.ReadSingle();
        value.Y = reader.ReadSingle();
        return value;
    }

    public static void Write(this BinaryWriter writer, IPAddress value)
    {
        byte[] bytes = value.GetAddressBytes();
        writer.Write(checked((byte)bytes.Length));
        writer.Write(bytes);
    }

    public static IPAddress ReadIPAddress(this BinaryReader reader)
    {
        byte length = reader.ReadByte();
        byte[] buffer = reader.ReadBytes(length);
        return new IPAddress(buffer);
    }

    public static void Write(this BinaryWriter writer, IPEndPoint value)
    {
        writer.Write(value.Address);
        writer.Write(checked((ushort)value.Port));
    }

    public static IPEndPoint ReadIPEndPoint(this BinaryReader reader)
    {
        IPAddress address = reader.ReadIPAddress();
        ushort port = reader.ReadUInt16();
        return new IPEndPoint(address, port);
    }

    public static void WriteNullable<T>(this BinaryWriter _writer, T? value, Action<T> writer)
    {
        if (value != null)
        {
            _writer.Write((byte)1);
            writer.Invoke(value);
        }
        else
        {
            _writer.Write((byte)0);
        }
    }

    public static T? ReadNullable<T>(this BinaryReader _reader, Func<T> reader)
    {
        byte notNull = _reader.ReadByte();
        if (notNull != 0)
        {
            return reader.Invoke();
        }
        else
        {
            return default;
        }
    }

    public static T[] ReadCollection<T>(this BinaryReader _reader, Func<BinaryReader, T> reader)
    {
        int count = _reader.ReadInt32();
        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        { result[i] = reader.Invoke(_reader); }
        return result;
    }

    public static T[] ReadCollection<T>(this BinaryReader _reader, Func<T> reader)
    {
        int count = _reader.ReadInt32();
        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        { result[i] = reader.Invoke(); }
        return result;
    }

    public static T[] ReadCollection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(this BinaryReader _reader) where T : ISerializable
    {
        int count = _reader.ReadInt32();
        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        {
            T item = Activator.CreateInstance<T>();
            item.Deserialize(_reader);
            result[i] = item;
        }
        return result;
    }
}
