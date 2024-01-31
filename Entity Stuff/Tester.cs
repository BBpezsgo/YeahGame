using YeahGame.Messages;

namespace YeahGame;

public class Tester : NetworkEntity, IDamageable
{
    public override EntityPrototype Prototype => EntityPrototype.Tester;

    float HP = 1f;
    Vector2 Target = Utils.Random.NextVector2(new Vector2(0f, 0f), new Vector2(50, 50));

    public Tester()
    {
        IsSolid = true;
    }

    public override void Render()
    {
        if (Game.Renderer.IsVisible(Position)) Game.Renderer[Position] = (ConsoleChar)'o';
        // if (Game.Renderer.IsVisible(Target)) Game.Renderer[Target] = new ConsoleChar('x', CharColor.BrightBlue);
    }

    public override void Update()
    {
        if (!Game.IsServer && !Game.IsOffline) return;

        // if (Vector2.Distance(Position, Target) < 1f)
        // {
        //     Target = Utils.Random.NextVector2(new Vector2(0f, 0f), new Vector2(50, 50));
        // }
        // else
        // {
        //     Position += Vector2.Normalize(Target - Position) * (Time.Delta * 1f);
        // }

        if (Game.Singleton.GameScene.ShouldSync)
        { SyncUp(); }
    }

    public void Damage(float amount)
    {
        if (!Game.IsServer && !Game.IsOffline) return;

        HP -= amount;

        if (HP <= 0f)
        { DoesExist = false; }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        Game.Singleton.GameScene.SpawnEntity(new Particles(ParticleConfigs.GetDeath(new Win32.Gdi32.GdiColor(255, 255, 255)), Utils.Random)
        {
            Position = Position,
        });
    }

    #region Networking

    public override void SyncDown(ObjectSyncMessage message, System.Net.IPEndPoint source)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        Position = reader.ReadVector2();
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
}
