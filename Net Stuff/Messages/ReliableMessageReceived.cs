using System.Diagnostics.CodeAnalysis;

namespace YeahGame.Messages;

public class ReliableMessageReceived : Message
{
    public required uint AckIndex { get; set; }

    public ReliableMessageReceived() : base(MessageType.ReliableMessageReceived) { }

    [SetsRequiredMembers]
    public ReliableMessageReceived(BinaryReader reader) : this() => Deserialize(reader);

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        AckIndex = reader.ReadUInt32();
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(AckIndex);
    }

    public override string ToString() => $"{{ {AckIndex} }} {base.ToString()}";

    public override ReliableMessageReceived Copy() => new()
    {
        Index = Index,
        AckIndex = AckIndex,
    };
}
