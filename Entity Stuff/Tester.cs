using YeahGame.Messages;

namespace YeahGame;

public class Tester : NetworkEntity
{
    public override EntityPrototype Prototype => EntityPrototype.Tester;

    Vector2 Target = Random.Shared.NextVector2(new Vector2(0f, 0f), new Vector2(Game.Singleton.GameScene.MapWidth, Game.Singleton.GameScene.MapHeight));

    #region Networking

    public override void SyncDown(ObjectSyncMessage message, System.Net.IPEndPoint source)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        Position.X = reader.ReadSingle();
        Position.Y = reader.ReadSingle();
    }

    public override void HandleRPC(RPCMessage rpcMessage)
    {

    }

    public override void NetworkDeserialize(BinaryReader reader)
    {

    }

    public override void NetworkSerialize(BinaryWriter writer)
    {

    }

    protected override void SyncUp(BinaryWriter writer)
    {
        writer.Write(Position);
    }

    #endregion

    public override void Render()
    {
        if (Game.Renderer.IsVisible(Position)) Game.Renderer[Position] = (ConsoleChar)'o';
        // if (Game.Renderer.IsVisible(Target)) Game.Renderer[Target] = new ConsoleChar('x', CharColor.BrightBlue);
    }

    public override void Update()
    {
        if (Game.IsServer)
        {
            if (Vector2.Distance(Position, Target) < 1f)
            {
                Target = Random.Shared.NextVector2(new Vector2(0f, 0f), new Vector2(Game.Singleton.GameScene.MapWidth, Game.Singleton.GameScene.MapHeight));
            }
            else
            {
                Vector2 dir = (Target - Position);
                dir = Vector2.Normalize(dir);
                dir *= Time.Delta * 1f;
                Position += dir;
            }

            SyncUp();
        }
    }
}
