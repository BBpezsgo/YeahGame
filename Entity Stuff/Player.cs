using System;
using YeahGame.Messages;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace YeahGame;

public class Player : NetworkEntity
{
    const float Speed = 10;

    float LastShot = Time.Now;
    public string? Owner;

    public override EntityPrototype Prototype => EntityPrototype.Player;

    public override void Update()
    {
        if (Game.Singleton.TryGetLocalPlayer(out Player? localPlayer) &&
            localPlayer == this)
        {
            if (Keyboard.IsKeyPressed('W'))
            { Position.Y -= Speed * 0.5f * Time.Delta; }
            if (Keyboard.IsKeyPressed('S'))
            { Position.Y += Speed * 0.5f * Time.Delta; }
            if (Keyboard.IsKeyPressed('A'))
            { Position.X -= Speed * Time.Delta; }
            if (Keyboard.IsKeyPressed('D'))
            { Position.X += Speed * Time.Delta; }

            Position.X = Math.Clamp(Position.X, 0, Game.Renderer.Width - 1);
            Position.Y = Math.Clamp(Position.Y, 0, Game.Renderer.Height - 1);

            if (Mouse.IsPressed(MouseButton.Left) &&
                Time.Now - LastShot > 0.5f)
            {
                Vector2 velocity = Mouse.RecordedConsolePosition - Position;
                velocity = Vector2.Normalize(velocity);
                velocity *= Projectile.Speed;
                velocity *= new Vector2(1, 0.5f);

                Projectile newProjectile = new()
                {
                    Position = Position,
                    Velocity = velocity,
                    SpawnedAt = Time.Now
                };
                Game.Singleton.projectiles.Add(newProjectile);

                LastShot = Time.Now;
            }
            using MemoryStream stream = new();
            BinaryWriter writer = new(stream);
            writer.Write(Position.X);
            writer.Write(Position.Y);
            writer.Flush();
            writer.Close();


            Game.Singleton.Connection.Send(new ObjectMessage()
            {
                Details = stream.ToArray(),
                Type = MessageType.Object,
                ObjectId = NetworkId
            });
        }
    }

    public override void Render()
    {
        if (!Game.Renderer.IsVisible(Position)) return;
        Game.Renderer[Position] = (ConsoleChar)'○';
    }

    public override void HandleMessage(ObjectMessage message)
    {
        using MemoryStream stream = new(message.Details);
        using BinaryReader reader = new(stream);
        Position.X = reader.ReadSingle();
        Position.Y = reader.ReadSingle();
    }

    public override void NetworkSerialize(BinaryWriter writer)
    {
        writer.Write(Owner ?? string.Empty);

    }

    public override void NetworkDeserialize(BinaryReader reader)
    {
        Owner = reader.ReadString();
    }
}
