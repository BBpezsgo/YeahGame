namespace YeahGame.Messages;

public enum MessageType : byte
{
    Control = 1,
    ObjectSync = 2,
    ObjectControl = 3,
    RPC = 4,
    InfoResponse = 5,
    InfoRequest = 6,
}

public abstract class Message : ISerializable
{
    MessageType _type;
    /// <summary>
    /// <b>DO NOT SET IT!</b><br/>
    /// It is handled by the <see cref="Connection"/>
    /// </summary>
    public uint Index;

    public MessageType Type => _type;

    public Message() { }
    protected Message(MessageType type) => _type = type;

    public virtual void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)_type);
        writer.Write(Index);
    }

    public virtual void Deserialize(BinaryReader reader)
    {
        _type = (MessageType)reader.ReadByte();
        Index = reader.ReadUInt32();
    }

    public override string ToString() => $"{{ {_type} }}";
}
