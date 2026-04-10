using AssemblyEngine.Core;

namespace RtsSample;

internal enum RtsStructureType
{
    Building,
    DefenseTower,
    WarFactory,
    Reactor
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

    public string Label => Type switch
    {
        RtsStructureType.Building => "Barracks",
        RtsStructureType.WarFactory => "War Factory",
        RtsStructureType.Reactor => "Nuclear Reactor",
        _ => "Gattling Cannon"
    };

    public Color FillColor => Type switch
    {
        RtsStructureType.Building => new Color(139, 26, 26),
        RtsStructureType.WarFactory => new Color(90, 90, 105),
        RtsStructureType.Reactor => new Color(180, 160, 50),
        _ => new Color(100, 60, 60)
    };

    public Color AccentColor => Type switch
    {
        RtsStructureType.Building => new Color(255, 80, 80),
        RtsStructureType.WarFactory => new Color(180, 180, 200),
        RtsStructureType.Reactor => new Color(255, 220, 80),
        _ => new Color(255, 120, 100)
    };

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
                HalfSize = new Vector2(32f, 24f);
                Radius = 38f;
                MaxHealth = 300f;
                AttackRange = 0f;
                AttackDamage = 0f;
                AttackInterval = 0f;
                DetectionRange = 0f;
                break;

            case RtsStructureType.WarFactory:
                HalfSize = new Vector2(40f, 30f);
                Radius = 46f;
                MaxHealth = 400f;
                AttackRange = 0f;
                AttackDamage = 0f;
                AttackInterval = 0f;
                DetectionRange = 0f;
                break;

            case RtsStructureType.Reactor:
                HalfSize = new Vector2(24f, 24f);
                Radius = 32f;
                MaxHealth = 200f;
                AttackRange = 0f;
                AttackDamage = 0f;
                AttackInterval = 0f;
                DetectionRange = 0f;
                break;

            default:
                HalfSize = new Vector2(18f, 18f);
                Radius = 26f;
                MaxHealth = 200f;
                AttackRange = 200f;
                AttackDamage = 18f;
                AttackInterval = 0.45f;
                DetectionRange = 240f;
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