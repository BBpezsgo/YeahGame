using System.Net;

namespace YeahGame;

public abstract class NetworkEntity : Entity
{
    public int NetworkId;

    public abstract EntityPrototype Prototype { get; }

    public abstract void HandleMessage(Messages.ObjectSyncMessage message);

    public abstract void NetworkSerialize(BinaryWriter writer);
    public abstract void NetworkDeserialize(BinaryReader reader);

    protected void SendMessage(Messages.ObjectSyncMessage message)
    {
        message.ObjectId = NetworkId;
        Game.Singleton.Connection.Send(message);
    }

    protected void SendSyncMessage(byte[] details)
    {
        Game.Singleton.Connection.Send(new Messages.ObjectSyncMessage()
        {
            Details = details,
            ObjectId = NetworkId,
        });
    }

    public void NetworkSpawn()
    {
        if (!Game.Singleton.Connection.IsServer) return;

        byte[] details = Utils.Serialize(NetworkSerialize);
        Game.Singleton.Connection.Send(new Messages.ObjectControlMessage()
        {
            Kind = Messages.ObjectControlMessageKind.Spawn,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = Prototype,
        });
    }

    public void NetworkDestroy()
    {
        if (!Game.Singleton.Connection.IsServer) return;

        Game.Singleton.Connection.Send(new Messages.ObjectControlMessage()
        {
            Kind = Messages.ObjectControlMessageKind.Destroy,
            ObjectId = NetworkId,
        });
    }

    public void NetworkDestroy(IPEndPoint destination)
    {
        if (!Game.Singleton.Connection.IsServer) return;

        Game.Singleton.Connection.SendTo(new Messages.ObjectControlMessage()
        {
            Kind = Messages.ObjectControlMessageKind.Destroy,
            ObjectId = NetworkId,
        }, destination);
    }

    public void NetworkInfo()
    {
        if (!Game.Singleton.Connection.IsServer) return;

        byte[] details = Utils.Serialize(NetworkSerialize);
        Game.Singleton.Connection.Send(new Messages.ObjectControlMessage()
        {
            Kind = Messages.ObjectControlMessageKind.Info,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = Prototype,
        });
    }

    public void NetworkInfo(IPEndPoint destination)
    {
        if (!Game.Singleton.Connection.IsServer) return;

        byte[] details = Utils.Serialize(NetworkSerialize);
        Game.Singleton.Connection.SendTo(new Messages.ObjectControlMessage()
        {
            Kind = Messages.ObjectControlMessageKind.Info,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = Prototype,
        }, destination);
    }
}
