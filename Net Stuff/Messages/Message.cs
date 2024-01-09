namespace YeahGame.Messages;

public enum MessageType : byte
{
    CONTROL = 1,
}

public abstract class Message : ISerializable
{
    public MessageType Type;

    public virtual void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Type);
    }

    public virtual void Deserialize(BinaryReader reader)
    {
        Type = (MessageType)reader.ReadByte();
    }
}
