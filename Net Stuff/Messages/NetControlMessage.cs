namespace YeahGame.Messages;

public enum NetControlMessageKind : byte
{
    HEY_IM_CLIENT_PLS_REPLY,
    HEY_CLIENT_IM_SERVER,
}

public class NetControlMessage : Message, ISerializable<NetControlMessage>
{
    public NetControlMessageKind Kind;

    public NetControlMessage(NetControlMessageKind kind)
    {
        Type = MessageType.CONTROL;
        Kind = kind;
    }

    public NetControlMessage()
    {
        Type = MessageType.CONTROL;
    }

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
