
namespace YeahGame.Messages;

public class ObjectSyncMessage : Message
{
    public int ObjectId;
    public byte[] Details = Array.Empty<byte>();

    public ObjectSyncMessage() : base(MessageType.ObjectSync) { }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        ObjectId = reader.ReadInt32();
        int detailsLength = reader.ReadInt32();
        Details = reader.ReadBytes(detailsLength);
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(ObjectId);
        writer.Write(Details.Length);
        writer.Write(Details);
    }

    public override string ToString() => $"{{ {ObjectId} }} {base.ToString()}";
}
