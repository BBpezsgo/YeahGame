using System.Diagnostics.CodeAnalysis;

namespace YeahGame.Messages;

public class RPCMessage : Message
{
    public required int ObjectId { get; set; }
    public required int RPCId { get; set; }
    public byte[] Details { get; set; }

    public RPCMessage() : base(MessageType.RPC)
    {
        Details = Array.Empty<byte>();
    }

    [SetsRequiredMembers]
    public RPCMessage(BinaryReader reader) : this() => Deserialize(reader);

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        ObjectId = reader.ReadInt32();
        RPCId = reader.ReadInt32();
        int detailsLength = reader.ReadInt32();
        Details = reader.ReadBytes(detailsLength);
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(ObjectId);
        writer.Write(RPCId);
        writer.Write(Details.Length);
        writer.Write(Details);
    }

    public override string ToString() => $"{{ {ObjectId} }} {base.ToString()}";
}
