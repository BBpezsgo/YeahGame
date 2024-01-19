using YeahGame.Messages;

namespace YeahGame;

public abstract class NetworkEntity : Entity
{
    public int NetworkId;

    public abstract EntityPrototype Prototype { get; }

    public abstract void NetworkSerialize(BinaryWriter writer);
    public abstract void NetworkDeserialize(BinaryReader reader);

    protected void SyncUp()
    {
        if (!Game.Singleton.GameScene.ShouldSync) return;

        byte[] data = Utils.Serialize(SyncUp);
        if (data.Length == 0) return;
        Game.Connection.Send(new ObjectSyncMessage()
        {
            Details = data,
            ObjectId = NetworkId,
        });
    }
    protected abstract void SyncUp(BinaryWriter writer);
    public abstract void SyncDown(ObjectSyncMessage message, System.Net.IPEndPoint source);

    public abstract void HandleRPC(RPCMessage rpcMessage);
}
