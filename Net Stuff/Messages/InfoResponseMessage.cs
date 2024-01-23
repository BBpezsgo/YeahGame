using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace YeahGame.Messages;

public class InfoResponseMessage : ReliableMessage
{
    public required bool IsServer { get; set; }
    public required IPEndPoint? Source { get; set; }
    public byte[] Details { get; set; }

    public InfoResponseMessage() : base(MessageType.InfoResponse)
    {
        Details = Array.Empty<byte>();
    }

    [SetsRequiredMembers]
    public InfoResponseMessage(BinaryReader reader) : this() => Deserialize(reader);

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        IsServer = reader.ReadBoolean();
        Source = reader.ReadNullable(reader.ReadIPEndPoint);
        int detailsLength = reader.ReadInt32();
        Details = reader.ReadBytes(detailsLength);
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(IsServer);
        writer.WriteNullable(Source, writer.Write);
        writer.Write(Details.Length);
        writer.Write(Details);
    }

    public override string ToString() => $"{{ {(Source is null ? "null" : Source.ToString())} }} {base.ToString()}";

    public override InfoResponseMessage Copy() => new()
    {
        Index = Index,

        ShouldAck = ShouldAck,
        Callback = Callback,

        IsServer = IsServer,
        Source = Source,
        Details = Details,
    };
}
