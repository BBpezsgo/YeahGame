using System.Diagnostics.CodeAnalysis;

namespace YeahGame.Messages;

public class HandshakeRequestMessage : Message
{
    public HandshakeRequestMessage() : base(MessageType.HandshakeRequest) { }

    [SetsRequiredMembers]
    public HandshakeRequestMessage(BinaryReader reader) : this() => Deserialize(reader);
}
