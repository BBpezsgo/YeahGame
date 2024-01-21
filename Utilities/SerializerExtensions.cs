using System.Net;

namespace YeahGame;

public static class SerializerExtensions
{
    public static void Write(this BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
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
}
