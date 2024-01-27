namespace YeahGame;

public class PlayerInfo : ISerializable
{
    public Changed<string> Username;
    public ImmutableChanged<List<ItemType>> Items = new List<ItemType>();

    public void Deserialize(BinaryReader reader)
    {
        Bitfield _changedBitfield = reader.ReadByte();

        if (_changedBitfield[0])
        { Username = reader.ReadString(); }
        Username.WasChanged = false;

        if (_changedBitfield[1])
        { Items = new List<ItemType>(reader.ReadCollection(static v => (ItemType)v.ReadByte())); }
        Items.WasChanged = false;
    }

    public void Serialize(BinaryWriter writer)
    {
        Bitfield _changedBitfield = 0;
        _changedBitfield[0] = Username.WasChanged;
        _changedBitfield[1] = Items.WasChanged;
        writer.Write((byte)_changedBitfield);

        if (Username.WasChanged)
        { writer.Write(Username); }
        Username.WasChanged = false;

        if (Items.WasChanged)
        { writer.Write(Items.Value, static (v, item) => v.Write((byte)item)); }
        Items.WasChanged = false;
    }
}
