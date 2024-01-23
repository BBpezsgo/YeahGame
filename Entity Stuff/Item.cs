using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public enum ItemType : byte
{
    Item1,
    Item2,
}

public class Item : NetworkEntity
{
    public ItemType Type;

    public override EntityPrototype Prototype => EntityPrototype.Item;

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;

        switch (Type)
        {
            case ItemType.Item1:
                Game.Renderer[Position] = new ConsoleChar('1', CharColor.White);
                break;
            case ItemType.Item2:
                Game.Renderer[Position] = new ConsoleChar('2', CharColor.White);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public override void Update()
    {

    }

    #region Networking

    public override void HandleRPC(RPCMessage rpcMessage)
    {

    }

    public override void SyncDown(ObjectSyncMessage message, IPEndPoint source)
    {

    }

    protected override void SyncUp(BinaryWriter writer)
    {

    }

    public override void NetworkSerialize(BinaryWriter writer)
    {
        writer.Write(Position);
        writer.Write((byte)Type);
    }

    public override void NetworkDeserialize(BinaryReader reader)
    {
        Position = reader.ReadVector2();
        Type = (ItemType)reader.ReadByte();
    }

    #endregion
}
