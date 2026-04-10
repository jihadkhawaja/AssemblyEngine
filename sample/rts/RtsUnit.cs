using AssemblyEngine.Core;

namespace RtsSample;

internal enum RtsUnitRole
{
    Worker,
    Guard,
    TankHunter,
    Battlemaster,
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
        RtsUnitRole.Worker => "Supply Truck",
        RtsUnitRole.Guard => "Red Guard",
        RtsUnitRole.TankHunter => "Tank Hunter",
        RtsUnitRole.Battlemaster => "Battlemaster",
        _ => "GLA Fighter"
    };

    public string Callsign => Role switch
    {
        RtsUnitRole.Worker => $"STK-{Id:00}",
        RtsUnitRole.Guard => $"RDG-{Id:00}",
        RtsUnitRole.TankHunter => $"THR-{Id:00}",
        RtsUnitRole.Battlemaster => $"BTL-{Id:00}",
        _ => $"GLA-{Id:00}"
    };

    public Color FillColor => Role switch
    {
        RtsUnitRole.Worker => new Color(85, 107, 47),
        RtsUnitRole.Guard => new Color(139, 26, 26),
        RtsUnitRole.TankHunter => new Color(160, 82, 45),
        RtsUnitRole.Battlemaster => new Color(58, 74, 40),
        _ => new Color(196, 160, 96)
    };

    public Color AccentColor => Role switch
    {
        RtsUnitRole.Worker => new Color(180, 200, 140),
        RtsUnitRole.Guard => new Color(255, 80, 80),
        RtsUnitRole.TankHunter => new Color(255, 180, 120),
        RtsUnitRole.Battlemaster => new Color(160, 200, 120),
        _ => new Color(255, 220, 170)
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
                Radius = 14f;
                Speed = 88f;
                MaxHealth = 120f;
                AttackRange = 0f;
                AttackDamage = 0f;
                AttackInterval = 0f;
                DetectionRange = 0f;
                CarryCapacity = 300;
                break;

            case RtsUnitRole.Guard:
                Radius = 8f;
                Speed = 78f;
                MaxHealth = 60f;
                AttackRange = 140f;
                AttackDamage = 12f;
                AttackInterval = 0.65f;
                DetectionRange = 200f;
                CarryCapacity = 0;
                break;

            case RtsUnitRole.TankHunter:
                Radius = 8f;
                Speed = 72f;
                MaxHealth = 70f;
                AttackRange = 170f;
                AttackDamage = 28f;
                AttackInterval = 1.4f;
                DetectionRange = 220f;
                CarryCapacity = 0;
                break;

            case RtsUnitRole.Battlemaster:
                Radius = 16f;
                Speed = 105f;
                MaxHealth = 280f;
                AttackRange = 180f;
                AttackDamage = 35f;
                AttackInterval = 1.2f;
                DetectionRange = 260f;
                CarryCapacity = 0;
                break;

            default:
                Radius = 10f;
                Speed = 82f;
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