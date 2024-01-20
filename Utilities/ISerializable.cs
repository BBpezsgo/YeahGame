namespace YeahGame;

public interface ISerializable
{
    public void Serialize(BinaryWriter writer);
    public void Deserialize(BinaryReader reader);
}
