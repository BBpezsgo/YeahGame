using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace YeahGame.Messages;

public class InfoRequestMessage : Message
{
    public required bool FromServer { get; set; }
    public required IPEndPoint? From { get; set; }

    public InfoRequestMessage() : base(MessageType.InfoRequest) { }

    [SetsRequiredMembers]
    public InfoRequestMessage(BinaryReader reader) : this() => Deserialize(reader);

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(FromServer);
        if (From is null)
        {
            writer.Write((byte)0);
        }
        else
        {
            writer.Write((byte)1);
            writer.Write(From.ToString());
        }
    }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        FromServer = reader.ReadBoolean();
        bool isNull = reader.ReadByte() == 0;
        if (!isNull)
        {
            string from = reader.ReadString();
            From = IPEndPoint.Parse(from);
        }
    }

    public override string ToString() => $"{{ {FromServer} {(From is null ? "null" : From.ToString())} }} {base.ToString()}";
}
