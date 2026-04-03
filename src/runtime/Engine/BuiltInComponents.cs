using AssemblyEngine.Core;

namespace AssemblyEngine.Engine;

/// <summary>
/// Built-in component for rendering sprites.
/// </summary>
public class SpriteComponent : Component
{
    public int SpriteId { get; set; } = -1;
    public bool AlphaBlend { get; set; } = true;
    public Vector2 Offset { get; set; }

    public void LoadSprite(string path)
    {
        SpriteId = Graphics.LoadSprite(path);
    }

    public override void Draw()
    {
        if (SpriteId < 0) return;
        var pos = Entity.Position + Offset;
        Graphics.DrawSprite(SpriteId, pos, AlphaBlend);
    }
}

/// <summary>
/// Built-in component for simple box collision detection.
/// </summary>
public class BoxCollider : Component
{
    public float Width { get; set; }
    public float Height { get; set; }
    public Vector2 Offset { get; set; }

    public Rectangle Bounds => new(
        Entity.Position.X + Offset.X,
        Entity.Position.Y + Offset.Y,
        Width, Height);

    public bool Overlaps(BoxCollider other) =>
        Bounds.Intersects(other.Bounds);
}

/// <summary>
/// Built-in component for simple velocity-based movement.
/// </summary>
public class VelocityComponent : Component
{
    public Vector2 Velocity { get; set; }
    public float Drag { get; set; }

    public override void Update(float deltaTime)
    {
        Entity.Position += Velocity * deltaTime;
        if (Drag > 0)
            Velocity = Velocity * (1f - Drag * deltaTime);
    }
}
