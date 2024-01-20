using System.Diagnostics.CodeAnalysis;

namespace YeahGame.Messages;

public enum NetControlMessageKind : byte
{
    HEY_IM_CLIENT_PLS_REPLY,
    HEY_CLIENT_IM_SERVER,
    IM_THERE,
    PING,
    PONG,
    ARE_U_SERVER,
    YES_IM_SERVER,
}

public class NetControlMessage : Message
{
    public required NetControlMessageKind Kind { get; set; }

    [SetsRequiredMembers]
    public NetControlMessage(NetControlMessageKind kind) : base(MessageType.Control)
    {
        Kind = kind;
    }

    public NetControlMessage() : base(MessageType.Control) { }

    [SetsRequiredMembers]
    public NetControlMessage(BinaryReader reader) : this() => Deserialize(reader);

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
