using System.Numerics;

namespace FpsSample;

internal sealed class FpsEnemy
{
    public FpsEnemy(Vector3 position, float speed, float hoverPhase)
    {
        Position = position;
        Speed = speed;
        HoverPhase = hoverPhase;
    }

    public Vector3 Position { get; set; }

    public float Speed { get; }

    public float HoverPhase { get; set; }

    public float AttackCooldown { get; set; }

    public bool Alive { get; set; } = true;
}