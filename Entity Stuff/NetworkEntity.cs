namespace YeahGame;

public abstract class NetworkEntity : Entity
{
    public int NetworkId;

    public abstract EntityPrototype Prototype { get; }

    public abstract void HandleMessage(Messages.ObjectMessage message);

    public abstract void NetworkSerialize(BinaryWriter writer);
    public abstract void NetworkDeserialize(BinaryReader reader);

    protected void SendMessage(Messages.ObjectMessage message)
    {
        message.ObjectId = NetworkId;
        Game.Singleton.Connection.Send(message);
    }

    public void NetworkSpawn(EntityPrototype entityPrototype)
    {
        if (!Game.Singleton.Connection.IsServer) return;

        using MemoryStream stream = new();
        BinaryWriter writer = new(stream);
        NetworkSerialize(writer);
        writer.Flush();
        writer.Close();
        byte[] details = stream.ToArray();

        Game.Singleton.Connection.Send(new Messages.ObjectControlMessage()
        {
            Type = Messages.MessageType.Object,
            Kind = Messages.ObjectControlMessageKind.Spawn,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = entityPrototype,
        });
    }

    public void NetworkDestroy()
    {
        if (!Game.Singleton.Connection.IsServer) return;

        Game.Singleton.Connection.Send(new Messages.ObjectControlMessage()
        {
            Type = Messages.MessageType.Object,
            Kind = Messages.ObjectControlMessageKind.Destroy,
            ObjectId = NetworkId,
        });
    }

    public void NetworkInfo(EntityPrototype entityPrototype)
    {
        if (!Game.Singleton.Connection.IsServer) return;

        using MemoryStream stream = new();
        BinaryWriter writer = new(stream);
        NetworkSerialize(writer);
        writer.Flush();
        writer.Close();
        byte[] details = stream.ToArray();

        Game.Singleton.Connection.Send(new Messages.ObjectControlMessage()
        {
            Type = Messages.MessageType.Object,
            Kind = Messages.ObjectControlMessageKind.Info,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = entityPrototype,
        });
    }
}
