using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace YeahGame.Messages;

public class ChatMessage : ReliableMessage
{
    public required bool SourceIsServer { get; set; }
    public required IPEndPoint? Source { get; set; }
    public required float Time { get; set; }
    public required string Content { get; set; }

    public ChatMessage() : base(MessageType.ChatMessage) { }

    [SetsRequiredMembers]
    public ChatMessage(BinaryReader reader) : this()
    {
        Content = null!;
        Deserialize(reader);
    }

    public override void Deserialize(BinaryReader reader)
    {
        base.Deserialize(reader);
        SourceIsServer = reader.ReadBoolean();
        Source = reader.ReadNullable(reader.ReadIPEndPoint);
        Time = reader.ReadSingle();
        Content = reader.ReadString();
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(SourceIsServer);
        writer.WriteNullable(Source, writer.Write);
        writer.Write(Time);
        writer.Write(Content);
    }

    public override string ToString() => $"{{ {(Source is null ? "null" : Source.ToString())} {TimeSpan.FromSeconds(Time)} \"{Content}\" }} {base.ToString()}";

    public override ChatMessage Copy() => new()
    {
        Index = Index,
        
        ShouldAck = ShouldAck,
        Callback = Callback,

        SourceIsServer = SourceIsServer,
        Source = Source,
        Time = Time,
        Content = Content,
    };
}
