
namespace YeahGame.Messages;

public enum ObjectControlMessageKind : byte
{
    /// <summary>
    /// Server -> Client
    /// </summary>
    Spawn,

    /// <summary>
    /// Server -> Client
    /// </summary>
    Destroy,

    /// <summary>
    /// Client -> Server
    /// </summary>
    NotFound,

    /// <summary>
    /// Server -> Client
    /// </summary>
    Info,
}

public class ObjectControlMessage : Message
{
    public ObjectControlMessageKind Kind;
    public int ObjectId;
    public EntityPrototype EntityPrototype;
    public byte[] Details = Array.Empty<byte>();

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        Kind = (ObjectControlMessageKind)reader.ReadByte();
        ObjectId = reader.ReadInt32();
        EntityPrototype = (EntityPrototype)reader.ReadByte();
        int detailsLength = reader.ReadInt32();
        Details = reader.ReadBytes(detailsLength);
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write((byte)Kind);
        writer.Write(ObjectId);
        writer.Write((byte)EntityPrototype);
        writer.Write(Details.Length);
        writer.Write(Details);
    }
}
