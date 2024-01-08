namespace YeahGame;

public interface ISerializable<T> where T : ISerializable<T>
{
    public void Serialize(BinaryWriter writer);
    public void Deserialize(BinaryReader reader);
}
