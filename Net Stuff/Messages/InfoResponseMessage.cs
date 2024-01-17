namespace YeahGame.Messages;

public class InfoResponseMessage : Message
{
    public byte[] Details = Array.Empty<byte>();

    public InfoResponseMessage() : base(MessageType.InfoResponse) { }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        int detailsLength = reader.ReadInt32();
        Details = reader.ReadBytes(detailsLength);
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(Details.Length);
        writer.Write(Details);
    }
}
