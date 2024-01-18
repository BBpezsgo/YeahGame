using System.Diagnostics.CodeAnalysis;
using System.Net;

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

    public static T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(byte[] buffer)
        where T : ISerializable
    {
        using MemoryStream stream = new(buffer);
        using BinaryReader reader = new(stream);
        T data = Activator.CreateInstance<T>();
        data.Deserialize(reader);
        return data;
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

    /// <summary>
    /// Source: <see href="https://stackoverflow.com/a/3122532"/>
    /// </summary>
    public static Vector2 Point2LineDistance(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 a2p = p - a;
        Vector2 a2b = b - a;
        float atb2 = a2b.LengthSquared();
        float atp_dot_atb = Vector2.Dot(a2p, a2b);
        float t = atp_dot_atb / atb2;
        return a + (a2b * t);
    }

    /// <summary>
    /// Source: <see href="https://www.youtube.com/watch?v=NbSee-XM7WA">javidx9</see>
    /// </summary>
    public static bool TilemapRaycast(Vector2 rayStart, Vector2 rayDir, float maxDistance, out Vector2 intersection, Func<int, int, bool> checker)
    {
        Vector2 rayUnitStepSize = new(
            MathF.Sqrt(1f + (rayDir.Y / rayDir.X) * (rayDir.Y / rayDir.X)),
            MathF.Sqrt(1f + (rayDir.X / rayDir.Y) * (rayDir.X / rayDir.Y))
            );

        Vector2Int mapCheck = (Vector2Int)rayStart;
        Vector2 rayLength1D;

        Vector2Int step;

        if (rayDir.X < 0)
        {
            step.X = -1;
            rayLength1D.X = (rayStart.X - mapCheck.X) * rayUnitStepSize.X;
        }
        else
        {
            step.X = 1;
            rayLength1D.X = (mapCheck.X + 1 - rayStart.X) * rayUnitStepSize.X;
        }

        if (rayDir.Y < 0)
        {
            step.Y = -1;
            rayLength1D.Y = (rayStart.Y - mapCheck.Y) * rayUnitStepSize.Y;
        }
        else
        {
            step.Y = 1;
            rayLength1D.Y = (mapCheck.Y + 1 - rayStart.Y) * rayUnitStepSize.Y;
        }

        bool tileFound = false;
        float distance = 0f;
        while (!tileFound && distance < maxDistance)
        {
            if (rayLength1D.X < rayLength1D.Y)
            {
                mapCheck.X += step.X;
                distance = rayLength1D.X;
                rayLength1D.X += rayUnitStepSize.X;
            }
            else
            {
                mapCheck.Y += step.Y;
                distance = rayLength1D.Y;
                rayLength1D.Y += rayUnitStepSize.Y;
            }

            if (checker.Invoke(mapCheck.X, mapCheck.Y))
            {
                tileFound = true;
            }
        }

        if (tileFound)
        { intersection = rayStart + rayDir * distance; }
        else
        { intersection = default; }

        return tileFound;
    }

    public static bool[] LoadMap(string str, out int width)
    {
        width = -1;
        int y = 0;
        List<bool> row = new();
        List<bool> res = new();
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c == '\r') continue;
            if (c == '\n')
            {
                res.AddRange(row);

                if (width != -1 && width != row.Count)
                { throw new NotImplementedException(); }

                width = row.Count;
                row.Clear();
                y++;
                continue;
            }
            row.Add(c != ' ');
        }

        if (row.Count > 0)
        {
            res.AddRange(row);

            if (width != -1 && width != row.Count)
            { throw new NotImplementedException(); }
        }

        return res.ToArray();
    }
}

public static class Extensions
{
    #region Random

    public static float NextSingle(this Random random, float min, float max)
        => (random.NextSingle() * (max - min)) + min;

    public static Vector2 NextVector2(this Random random, float maxWidth, float maxHeight) => new(
        (random.NextSingle() - .5f) * maxWidth,
        (random.NextSingle() - .5f) * maxHeight);

    public static Vector2 NextVector2(this Random random, Vector2 min, Vector2 max) => new(
        random.NextSingle(min.X, max.X),
        random.NextSingle(min.Y, max.Y));

    #endregion

    #region Serializing

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

    #endregion
}
