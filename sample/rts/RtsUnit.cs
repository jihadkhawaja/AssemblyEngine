using AssemblyEngine.Core;

namespace RtsSample;

internal enum RtsUnitRole
{
    Worker,
    Guard,
    Raider
}

internal enum RtsUnitOrderType
{
    Idle,
    Move,
    Harvest
}

internal sealed class RtsUnit
{
    private static int _nextId;

    public static void ResetIds() => Interlocked.Exchange(ref _nextId, 0);

    public int Id { get; }

    public RtsUnitRole Role { get; }

    public Vector2 Position { get; set; }

    public Vector2 MoveTarget { get; set; }

    public Vector2 AimDirection { get; set; } = Vector2.Right;

    public bool HasMoveTarget { get; set; }

    public bool Selected { get; set; }

    public float Radius { get; set; }

    public float Speed { get; set; }

    public float MaxHealth { get; set; }

    public float Health { get; set; }

    public float AttackRange { get; set; }

    public float AttackDamage { get; set; }

    public float AttackInterval { get; set; }

    public float AttackCooldown { get; set; }

    public float DetectionRange { get; set; }

    public int CarryOre { get; set; }

    public int CarryCapacity { get; set; }

    public float HarvestProgress { get; set; }

    public int AssignedNodeIndex { get; set; } = -1;

    public bool ReturningToBase { get; set; }

    public RtsUnitOrderType OrderType { get; set; }

    public bool IsEnemy => Role == RtsUnitRole.Raider;

    public bool IsPlayerControlled => !IsEnemy;

    public bool IsAlive => Health > 0f;

    public string Label => Role switch
    {
        RtsUnitRole.Worker => "Worker",
        RtsUnitRole.Guard => "Guard",
        _ => "Raider"
    };

    public string Callsign => Role switch
    {
        RtsUnitRole.Worker => $"WRK-{Id:00}",
        RtsUnitRole.Guard => $"GRD-{Id:00}",
        _ => $"RDR-{Id:00}"
    };

    public Color FillColor => Role switch
    {
        RtsUnitRole.Worker => new Color(111, 210, 255),
        RtsUnitRole.Guard => new Color(140, 255, 179),
        _ => new Color(255, 120, 105)
    };

    public Color AccentColor => Role switch
    {
        RtsUnitRole.Worker => new Color(215, 244, 255),
        RtsUnitRole.Guard => new Color(216, 255, 224),
        _ => new Color(255, 216, 170)
    };

    public RtsUnit(RtsUnitRole role, Vector2 position)
        : this(ClaimNextId(), role, position)
    {
    }

    internal RtsUnit(int id, RtsUnitRole role, Vector2 position)
    {
        Id = ClaimId(id);
        Role = role;
        Position = position;
        MoveTarget = position;

        switch (role)
        {
            case RtsUnitRole.Worker:
                Radius = 10f;
                Speed = 98f;
                MaxHealth = 48f;
                AttackRange = 0f;
                AttackDamage = 0f;
                AttackInterval = 0f;
                DetectionRange = 0f;
                CarryCapacity = 40;
                break;

            case RtsUnitRole.Guard:
                Radius = 12f;
                Speed = 112f;
                MaxHealth = 90f;
                AttackRange = 156f;
                AttackDamage = 15f;
                AttackInterval = 0.72f;
                DetectionRange = 228f;
                CarryCapacity = 0;
                break;

            default:
                Radius = 11f;
                Speed = 92f;
                MaxHealth = 76f;
                AttackRange = 20f;
                AttackDamage = 9f;
                AttackInterval = 1.08f;
                DetectionRange = 250f;
                CarryCapacity = 0;
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