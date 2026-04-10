namespace RtsSample;

public sealed partial class RtsGameScript
{
    private RtsGameSnapshot CreateSnapshot()
    {
        return new RtsGameSnapshot(
            _oreStockpile,
            _hqHealth,
            _waveIndex,
            _nextWaveTimer,
            _missionTime,
            _bannerTimer,
            _bannerTitle,
            _bannerSubtitle,
            _victory,
            _gameOver,
            _rallyPoint,
            _commandPulsePosition,
            _commandPulseTimer,
            _navigationPulsePosition,
            _navigationPulseTimer,
            _units.Select(unit => new RtsUnitSnapshot(
                unit.Id,
                unit.Role,
                unit.Position,
                unit.MoveTarget,
                unit.AimDirection,
                unit.HasMoveTarget,
                unit.Radius,
                unit.Speed,
                unit.MaxHealth,
                unit.Health,
                unit.AttackRange,
                unit.AttackDamage,
                unit.AttackInterval,
                unit.AttackCooldown,
                unit.DetectionRange,
                unit.CarryOre,
                unit.CarryCapacity,
                unit.HarvestProgress,
                unit.AssignedNodeIndex,
                unit.ReturningToBase,
                unit.OrderType)).ToArray(),
            _structures.Select(structure => new RtsStructureSnapshot(
                structure.Id,
                structure.Type,
                structure.Position,
                structure.HalfSize,
                structure.Radius,
                structure.MaxHealth,
                structure.Health,
                structure.AttackRange,
                structure.AttackDamage,
                structure.AttackInterval,
                structure.AttackCooldown,
                structure.DetectionRange,
                structure.UnderConstruction,
                structure.ConstructionProgress,
                structure.ConstructionTime)).ToArray(),
            _resourceNodes.Select(node => new RtsResourceNodeSnapshot(node.Name, node.Position, node.Radius, node.RemainingOre)).ToArray(),
            _productionQueue.Select(order => new RtsProductionOrderSnapshot(order.Type, order.Label, order.ReservedSite, order.RemainingTime)).ToArray(),
            _shotEffects.Select(effect => new RtsShotEffectSnapshot(effect.From, effect.To, effect.Color, effect.RemainingTime)).ToArray());
    }

    private void ApplySnapshot(RtsGameSnapshot snapshot)
    {
        _oreStockpile = snapshot.OreStockpile;
        _hqHealth = snapshot.HeadquartersHealth;
        _waveIndex = snapshot.WaveIndex;
        _nextWaveTimer = snapshot.NextWaveTimer;
        _missionTime = snapshot.MissionTime;
        _bannerTimer = snapshot.BannerTimer;
        _bannerTitle = snapshot.BannerTitle;
        _bannerSubtitle = snapshot.BannerSubtitle;
        _victory = snapshot.Victory;
        _gameOver = snapshot.GameOver;
        _rallyPoint = snapshot.RallyPoint;
        _commandPulsePosition = snapshot.CommandPulsePosition;
        _commandPulseTimer = snapshot.CommandPulseTimer;
        _navigationPulsePosition = snapshot.NavigationPulsePosition;
        _navigationPulseTimer = snapshot.NavigationPulseTimer;

        _units.Clear();
        foreach (var unitSnapshot in snapshot.Units)
        {
            var unit = new RtsUnit(unitSnapshot.Id, unitSnapshot.Role, unitSnapshot.Position)
            {
                MoveTarget = unitSnapshot.MoveTarget,
                AimDirection = unitSnapshot.AimDirection,
                HasMoveTarget = unitSnapshot.HasMoveTarget,
                Radius = unitSnapshot.Radius,
                Speed = unitSnapshot.Speed,
                MaxHealth = unitSnapshot.MaxHealth,
                Health = unitSnapshot.Health,
                AttackRange = unitSnapshot.AttackRange,
                AttackDamage = unitSnapshot.AttackDamage,
                AttackInterval = unitSnapshot.AttackInterval,
                AttackCooldown = unitSnapshot.AttackCooldown,
                DetectionRange = unitSnapshot.DetectionRange,
                CarryOre = unitSnapshot.CarryOre,
                CarryCapacity = unitSnapshot.CarryCapacity,
                HarvestProgress = unitSnapshot.HarvestProgress,
                AssignedNodeIndex = unitSnapshot.AssignedNodeIndex,
                ReturningToBase = unitSnapshot.ReturningToBase,
                OrderType = unitSnapshot.OrderType,
                Selected = _localSelectedUnitIds.Contains(unitSnapshot.Id)
            };
            _units.Add(unit);
        }

        _structures.Clear();
        foreach (var structureSnapshot in snapshot.Structures)
        {
            var structure = new RtsStructure(structureSnapshot.Id, structureSnapshot.Type, structureSnapshot.Position)
            {
                MaxHealth = structureSnapshot.MaxHealth,
                Health = structureSnapshot.Health,
                AttackRange = structureSnapshot.AttackRange,
                AttackDamage = structureSnapshot.AttackDamage,
                AttackInterval = structureSnapshot.AttackInterval,
                AttackCooldown = structureSnapshot.AttackCooldown,
                DetectionRange = structureSnapshot.DetectionRange,
                UnderConstruction = structureSnapshot.UnderConstruction,
                ConstructionProgress = structureSnapshot.ConstructionProgress,
                ConstructionTime = structureSnapshot.ConstructionTime
            };
            _structures.Add(structure);
        }

        _resourceNodes.Clear();
        foreach (var resourceNode in snapshot.ResourceNodes)
            _resourceNodes.Add(new RtsResourceNode(resourceNode.Name, resourceNode.Position, resourceNode.RemainingOre, resourceNode.Radius));

        _productionQueue.Clear();
        foreach (var order in snapshot.ProductionQueue)
        {
            var productionOrder = new ProductionOrder(order.Type, Math.Max(order.RemainingTime, 0.01f), order.ReservedSite)
            {
                RemainingTime = order.RemainingTime
            };
            _productionQueue.Add(productionOrder);
        }

        _shotEffects.Clear();
        foreach (var effect in snapshot.ShotEffects)
            _shotEffects.Add(new ShotEffect(effect.From, effect.To, effect.Color, effect.RemainingTime));
    }
}