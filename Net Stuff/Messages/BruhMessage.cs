using System.Diagnostics.CodeAnalysis;

namespace YeahGame.Messages;

public class BruhMessage : ReliableMessage
{
    public BruhMessage() : base(MessageType.Bruh) { }

    [SetsRequiredMembers]
    public BruhMessage(BinaryReader reader) : this()
    {
        Deserialize(reader);
    }

    public override BruhMessage Copy() => new()
    {
        Index = Index,

        ShouldAck = ShouldAck,
        Callback = Callback,
    };
}
