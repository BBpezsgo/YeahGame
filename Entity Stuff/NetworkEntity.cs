using System.Net;
using YeahGame.Messages;

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
        Game.Connection.Send(message);
    }

    protected void SendSyncMessage(byte[] details)
    {
        Game.Connection.Send(new ObjectSyncMessage()
        {
            Details = details,
            ObjectId = NetworkId,
        });
    }

    public void NetworkSpawn()
    {
        if (!Game.IsServer) return;

        byte[] details = Utils.Serialize(NetworkSerialize);
        Game.Connection.Send(new ObjectControlMessage()
        {
            Kind = ObjectControlMessageKind.Spawn,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = Prototype,
        });
    }

    public void NetworkDestroy()
    {
        if (!Game.IsServer) return;

        Game.Connection.Send(new ObjectControlMessage()
        {
            Kind = ObjectControlMessageKind.Destroy,
            ObjectId = NetworkId,
        });
    }

    public void NetworkDestroy(IPEndPoint destination)
    {
        if (!Game.IsServer) return;

        Game.Connection.SendTo(new ObjectControlMessage()
        {
            Kind = ObjectControlMessageKind.Destroy,
            ObjectId = NetworkId,
        }, destination);
    }

    public void NetworkInfo()
    {
        if (!Game.IsServer) return;

        byte[] details = Utils.Serialize(NetworkSerialize);
        Game.Connection.Send(new ObjectControlMessage()
        {
            Kind = ObjectControlMessageKind.Info,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = Prototype,
        });
    }

    public void NetworkInfo(IPEndPoint destination)
    {
        if (!Game.IsServer) return;

        byte[] details = Utils.Serialize(NetworkSerialize);
        Game.Connection.SendTo(new ObjectControlMessage()
        {
            Kind = ObjectControlMessageKind.Info,
            ObjectId = NetworkId,
            Details = details,
            EntityPrototype = Prototype,
        }, destination);
    }

    public abstract void HandleRPC(RPCmessage rpcMessage);
}
