
namespace YeahGame.Messages;

public class RPCmessage : Message
{
    public int ObjectId;
    public int RPCId;
    public byte[] Details = Array.Empty<byte>();

    public RPCmessage() : base(MessageType.RPC) { }

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
