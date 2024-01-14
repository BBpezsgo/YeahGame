namespace YeahGame.Messages;

public enum MessageType : byte
{
    Control = 1,
    ObjectSync = 2,
    ObjectControl = 3,
}

public abstract class Message : ISerializable
{
    MessageType _type;

    public MessageType Type => _type;

    public Message() { }
    protected Message(MessageType type) => _type = type;

    public virtual void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)_type);
    }

    public virtual void Deserialize(BinaryReader reader)
    {
        _type = (MessageType)reader.ReadByte();
    }

    public override string ToString() => $"{{ {_type} }}";
}
