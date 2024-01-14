namespace YeahGame.Messages;

public enum NetControlMessageKind : byte
{
    HEY_IM_CLIENT_PLS_REPLY,
    HEY_CLIENT_IM_SERVER,
    IM_THERE,
    PING,
    PONG,
}

public class NetControlMessage : Message
{
    public NetControlMessageKind Kind;

    public NetControlMessage(NetControlMessageKind kind) : base(MessageType.Control)
    {
        Kind = kind;
    }

    public NetControlMessage() : base(MessageType.Control) { }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write((byte)Kind);
    }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        Kind = (NetControlMessageKind)reader.ReadByte();
    }
}
