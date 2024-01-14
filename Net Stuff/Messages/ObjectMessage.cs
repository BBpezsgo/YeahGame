
namespace YeahGame.Messages;

public class ObjectMessage : Message
{
    public int ObjectId;

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        ObjectId = reader.ReadInt32();
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(ObjectId);
    }
}
