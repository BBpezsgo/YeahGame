using System.Diagnostics.CodeAnalysis;

namespace YeahGame.Messages;

public enum NetControlMessageKind : byte
{
    PING,
    PONG,
}

public class NetControlMessage : ReliableMessage
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

    public override NetControlMessage Copy() => new()
    {
        Index = Index,
        
        ShouldAck = ShouldAck,
        Callback = Callback,

        Kind = Kind,
    };
}
