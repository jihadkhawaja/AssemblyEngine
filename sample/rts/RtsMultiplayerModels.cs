using AssemblyEngine.Core;

namespace RtsSample;

internal static class RtsMultiplayerChannel
{
    public const string Name = "rts";
}

internal static class RtsMultiplayerMessageTypes
{
    public const string Snapshot = "snapshot";
    public const string QueueProduction = "queueProduction";
    public const string IssueOrder = "issueOrder";
    public const string SetRallyPoint = "setRallyPoint";
    public const string Restart = "restart";
}

internal enum RtsSessionMode
{
    SinglePlayer,
    Host,
    Client
}

internal sealed record RtsSessionStartPayload(RtsGameSnapshot Snapshot);

internal sealed record RtsGameSnapshot(
    int OreStockpile,
    float HeadquartersHealth,
    int OreStockpileP2,
    float HeadquartersHealthP2,
    int WaveIndex,
    float NextWaveTimer,
    float MissionTime,
    float BannerTimer,
    string BannerTitle,
    string BannerSubtitle,
    bool Victory,
    bool GameOver,
    int WinnerTeam,
    Vector2 RallyPoint,
    Vector2 RallyPointP2,
    Vector2 CommandPulsePosition,
    float CommandPulseTimer,
    Vector2 NavigationPulsePosition,
    float NavigationPulseTimer,
    RtsUnitSnapshot[] Units,
    RtsStructureSnapshot[] Structures,
    RtsResourceNodeSnapshot[] ResourceNodes,
    RtsProductionOrderSnapshot[] ProductionQueue,
    RtsProductionOrderSnapshot[] ProductionQueueP2,
    RtsShotEffectSnapshot[] ShotEffects);

internal sealed record RtsUnitSnapshot(
    int Id,
    RtsUnitRole Role,
    int Team,
    Vector2 Position,
    Vector2 MoveTarget,
    Vector2 AimDirection,
    bool HasMoveTarget,
    float Radius,
    float Speed,
    float MaxHealth,
    float Health,
    float AttackRange,
    float AttackDamage,
    float AttackInterval,
    float AttackCooldown,
    float DetectionRange,
    int CarryOre,
    int CarryCapacity,
    float HarvestProgress,
    int AssignedNodeIndex,
    bool ReturningToBase,
    RtsUnitOrderType OrderType);

internal sealed record RtsStructureSnapshot(
    int Id,
    RtsStructureType Type,
    int Team,
    Vector2 Position,
    Vector2 HalfSize,
    float Radius,
    float MaxHealth,
    float Health,
    float AttackRange,
    float AttackDamage,
    float AttackInterval,
    float AttackCooldown,
    float DetectionRange,
    bool UnderConstruction,
    float ConstructionProgress,
    float ConstructionTime);

internal sealed record RtsResourceNodeSnapshot(string Name, Vector2 Position, float Radius, int RemainingOre);

internal sealed record RtsProductionOrderSnapshot(RtsProductionType Type, string Label, Vector2? ReservedSite, float RemainingTime);

internal sealed record RtsShotEffectSnapshot(Vector2 From, Vector2 To, Color Color, float RemainingTime);

internal sealed record RtsQueueProductionCommand(RtsProductionType Type, Vector2? ReservedSite);

internal sealed record RtsIssueOrderCommand(int[] UnitIds, Vector2 Target);

internal sealed record RtsSetRallyPointCommand(Vector2 RallyPoint);

internal sealed record RtsRestartCommand(bool Immediate = true);