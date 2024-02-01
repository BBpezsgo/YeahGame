using System.Collections.Frozen;
using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public enum ItemType : byte
{
    RapidFire,
    SuicideBomber,
    DoubleFire,
}

public class Item : NetworkEntity
{
    const bool ShouldMeMysterious = false;

    public static readonly ItemType[] ItemTypes = Enum.GetValues<ItemType>();
    public static readonly FrozenDictionary<ItemType, string> ItemNames = new Dictionary<ItemType, string>()
    {
        { ItemType.RapidFire, "Rapid Fire" },
        { ItemType.SuicideBomber, "Suicide Bomber" },
        { ItemType.DoubleFire, "Double Fire" },
    }.ToFrozenDictionary();

    public ItemType Type;
    public override EntityPrototype Prototype => EntityPrototype.Item;

    public Item()
    {
        IsSolid = false;
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;

        if (ShouldMeMysterious || !Utils.IsDebug)
        {
            Game.Renderer[Position] = new ConsoleChar('?', CharColor.White);
        }
        else
        {
            Game.Renderer[Position] = Type switch
            {
                ItemType.RapidFire => new ConsoleChar('R', CharColor.White),
                ItemType.SuicideBomber => new ConsoleChar('S', CharColor.White),
                ItemType.DoubleFire => new ConsoleChar('D', CharColor.White),
                _ => throw new NotImplementedException(),
            };
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
