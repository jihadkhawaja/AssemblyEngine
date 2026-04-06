using AssemblyEngine.Core;

namespace RtsSample;

internal sealed class RtsResourceNode
{
    public string Name { get; }

    public Vector2 Position { get; }

    public float Radius { get; }

    public int RemainingOre { get; set; }

    public bool IsDepleted => RemainingOre <= 0;

    public RtsResourceNode(string name, Vector2 position, int remainingOre, float radius = 30f)
    {
        Name = name;
        Position = position;
        RemainingOre = remainingOre;
        Radius = radius;
    }
}