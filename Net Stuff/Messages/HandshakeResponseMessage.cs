using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace YeahGame.Messages;

public class HandshakeResponseMessage : Message
{
    public required IPEndPoint ThisIsYou { get; set; }

    public HandshakeResponseMessage() : base(MessageType.HandshakeResponse) { }

    [SetsRequiredMembers]
    public HandshakeResponseMessage(BinaryReader reader) : this()
    {
        ThisIsYou = null!;
        Deserialize(reader);
    }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        ThisIsYou = reader.ReadIPEndPoint();
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(ThisIsYou);
    }

    public override string ToString() => $"{{ {ThisIsYou} }} {base.ToString()}";
}
