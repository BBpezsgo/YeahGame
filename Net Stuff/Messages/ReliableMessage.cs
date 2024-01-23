namespace YeahGame.Messages;

public abstract class ReliableMessage : Message
{
    public bool ShouldAck;
    public Action? Callback;

    protected ReliableMessage(MessageType messageType) : base(messageType) { }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        ShouldAck = reader.ReadBoolean();
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(ShouldAck);
    }

    public override string ToString() => $"{{ {ShouldAck} }} {base.ToString()}";

    public abstract override ReliableMessage Copy();
}
