
using System.Diagnostics.CodeAnalysis;

namespace YeahGame.Messages;

public enum ObjectControlMessageKind : byte
{
    /// <summary>
    /// Server -> Client
    /// </summary>
    Spawn = 1,

    /// <summary>
    /// Server -> Client
    /// </summary>
    Destroy = 2,

    /// <summary>
    /// Client -> Server
    /// </summary>
    NotFound = 3,

    /// <summary>
    /// Server -> Client
    /// </summary>
    Info = 4,
}

public class ObjectControlMessage : Message
{
    public required ObjectControlMessageKind Kind { get; set; }
    public int ObjectId { get; set; }
    public EntityPrototype EntityPrototype { get; set; }
    public byte[] Details { get; set; }

    public ObjectControlMessage() : base(MessageType.ObjectControl)
    {
        Details = Array.Empty<byte>();
    }

    [SetsRequiredMembers]
    public ObjectControlMessage(BinaryReader reader) : this() => Deserialize(reader);

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

    public override string ToString() => Kind switch
    {
        ObjectControlMessageKind.Spawn => $"{{ {Kind} {EntityPrototype} {ObjectId} }} {base.ToString()}",
        ObjectControlMessageKind.Destroy => $"{{ {Kind} {ObjectId} }} {base.ToString()}",
        ObjectControlMessageKind.NotFound => $"{{ {Kind} {ObjectId} }} {base.ToString()}",
        ObjectControlMessageKind.Info => $"{{ {Kind} {EntityPrototype} {ObjectId} }} {base.ToString()}",
        _ => throw new NotImplementedException(),
    };
}
