using AssemblyEngine.Core;

namespace RtsSample;

internal enum RtsStructureType
{
    Building,
    DefenseTower
}

internal sealed class RtsStructure
{
    private static int _nextId;

    public static void ResetIds() => Interlocked.Exchange(ref _nextId, 0);

    public int Id { get; }

    public RtsStructureType Type { get; }

    public Vector2 Position { get; }

    public Vector2 HalfSize { get; }

    public float Radius { get; }

    public float MaxHealth { get; set; }

    public float Health { get; set; }

    public float AttackRange { get; set; }

    public float AttackDamage { get; set; }

    public float AttackInterval { get; set; }

    public float AttackCooldown { get; set; }

    public float DetectionRange { get; set; }

    public bool IsAlive => Health > 0f;

    public string Label => Type == RtsStructureType.Building ? "Structure" : "Defense Tower";

    public Color FillColor => Type == RtsStructureType.Building
        ? new Color(124, 98, 76)
        : new Color(104, 110, 138);

    public Color AccentColor => Type == RtsStructureType.Building
        ? new Color(255, 220, 156)
        : new Color(246, 246, 177);

    public RtsStructure(RtsStructureType type, Vector2 position)
        : this(ClaimNextId(), type, position)
    {
    }

    internal RtsStructure(int id, RtsStructureType type, Vector2 position)
    {
        Id = ClaimId(id);
        Type = type;
        Position = position;

        switch (type)
        {
            case RtsStructureType.Building:
                HalfSize = new Vector2(28f, 22f);
                Radius = 34f;
                MaxHealth = 240f;
                AttackRange = 0f;
                AttackDamage = 0f;
                AttackInterval = 0f;
                DetectionRange = 0f;
                break;

            default:
                HalfSize = new Vector2(18f, 18f);
                Radius = 26f;
                MaxHealth = 160f;
                AttackRange = 182f;
                AttackDamage = 13f;
                AttackInterval = 0.86f;
                DetectionRange = 214f;
                break;
        }

        Health = MaxHealth;
    }

    private static int ClaimNextId() => Interlocked.Increment(ref _nextId);

    private static int ClaimId(int id)
    {
        while (true)
        {
            var current = Volatile.Read(ref _nextId);
            if (id <= current)
                return id;

            if (Interlocked.CompareExchange(ref _nextId, id, current) == current)
                return id;
        }
    }
}