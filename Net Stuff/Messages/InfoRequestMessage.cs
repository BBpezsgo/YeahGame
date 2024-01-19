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
        writer.WriteNullable(From, writer.Write);
    }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        FromServer = reader.ReadBoolean();
        From = reader.ReadNullable(reader.ReadIPEndPoint);
    }

    public override string ToString() => $"{{ {FromServer} {(From is null ? "null" : From.ToString())} }} {base.ToString()}";
}
