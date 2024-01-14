
namespace YeahGame.Messages;

public class ObjectMessage : Message
{
    public int ObjectId;
    public byte[] Details = Array.Empty<byte>();

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
}
