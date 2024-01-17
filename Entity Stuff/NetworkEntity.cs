using YeahGame.Messages;

namespace YeahGame;

public abstract class NetworkEntity : Entity
{
    public int NetworkId;

    public abstract EntityPrototype Prototype { get; }

    public abstract void HandleMessage(Messages.ObjectSyncMessage message);

    public abstract void NetworkSerialize(BinaryWriter writer);
    public abstract void NetworkDeserialize(BinaryReader reader);

    protected void SendSyncMessage(byte[] details)
    {
        Game.Connection.Send(new ObjectSyncMessage()
        {
            Details = details,
            ObjectId = NetworkId,
        });
    }

    public abstract void HandleRPC(RPCmessage rpcMessage);
}
