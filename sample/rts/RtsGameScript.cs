using AssemblyEngine.Core;
using AssemblyEngine.Scripting;

namespace RtsSample;

public sealed partial class RtsGameScript : GameScript
{
    private const float WorldWidth = 2200f;
    private const float WorldHeight = 1400f;
    private const float HeadquartersHalfWidth = 58f;
    private const float HeadquartersHalfHeight = 46f;
    private const float HeadquartersMaxHealth = 1000f;
    private const float EnemyBeaconHalfWidth = 52f;
    private const float EnemyBeaconHalfHeight = 40f;
    private const int VictoryOreGoalValue = 10000;
    private const int WorkerCost = 600;
    private const int GuardCost = 300;
    private const int TankHunterCost = 400;
    private const int BattlemasterCost = 800;
    private const int BuildingCost = 500;
    private const int DefenseTowerCost = 1200;
    private const int WarFactoryCost = 2000;
    private const int ReactorCost = 1000;
    private const int QueueLimit = 6;
    private const int SalvagePerRaider = 50;
    private const int HarvestAmount = 60;
    private const float HarvestInterval = 0.78f;
    private const float CameraPanSpeed = 540f;
    private const float EdgeScrollMargin = 18f;
    private const float WaveInterval = 18f;
    private const float SelectionThreshold = 8f;
    private const float MinimapWidth = 160f;
    private const float MinimapHeight = 160f;
    private const float MinimapMargin = 16f;
    private const float MinimapBottomOffset = 16f;
    private const float CommandPulseDuration = 0.9f;
    private const float NavigationPulseDuration = 0.72f;
    private static readonly Vector2 HeadquartersPosition = new(320f, 1060f);
    private static readonly Vector2 HeadquartersPositionP2 = new(1880f, 340f);
    private static readonly Vector2 EnemyBeaconPosition = new(1910f, 210f);
    private static readonly Vector2[] EnemySpawnPoints =
    [
        new Vector2(1850f, 170f),
        new Vector2(1980f, 250f),
        new Vector2(2070f, 150f),
        new Vector2(1760f, 310f)
    ];
    private static readonly Vector2[] BuildingSitePositions =
    [
        new Vector2(550f, 1180f),
        new Vector2(980f, 1080f),
        new Vector2(1410f, 1150f),
        new Vector2(420f, 940f),
        new Vector2(780f, 850f),
        new Vector2(1200f, 950f)
    ];
    private static readonly Vector2[] DefenseTowerSitePositions =
    [
        new Vector2(760f, 1030f),
        new Vector2(1090f, 820f),
        new Vector2(1460f, 610f),
        new Vector2(1740f, 900f),
        new Vector2(580f, 780f),
        new Vector2(1300f, 720f)
    ];
    private static readonly Vector2[] WarFactorySitePositions =
    [
        new Vector2(650f, 1100f),
        new Vector2(1100f, 1000f),
        new Vector2(850f, 920f)
    ];
    private static readonly Vector2[] ReactorSitePositions =
    [
        new Vector2(480f, 1060f),
        new Vector2(900f, 960f),
        new Vector2(1300f, 1050f),
        new Vector2(700f, 820f)
    ];
    private static readonly string[] BlockingHudElementIds =
    [
        "top-bar",
        "center-message",
        "command-bar",
        "help-panel"
    ];

    private readonly Random _random = new();
    private readonly List<RtsStructure> _structures = [];
    private readonly List<RtsUnit> _units = [];
    private readonly List<RtsResourceNode> _resourceNodes = [];
    private readonly List<ShotEffect> _shotEffects = [];
    private readonly List<ProductionOrder> _productionQueue = [];
    private RtsProductionType? _activePlacementType;
    private RtsAudioScript _audio = null!;
    private Vector2 _cameraPosition;
    private Vector2 _commandPulsePosition;
    private Vector2 _navigationPulsePosition;
    private Vector2 _rallyPoint;
    private Vector2 _rallyPointP2;
    private Vector2 _selectionStartScreen;
    private Vector2 _selectionEndScreen;
    private float _commandPulseTimer;
    private float _hqHealth;
    private float _hqHealthP2;
    private float _navigationPulseTimer;
    private float _nextWaveTimer;
    private float _missionTime;
    private float _bannerTimer;
    private bool _selectionActive;
    private bool _leftMouseWasDown;
    private bool _middleMouseWasDown;
    private bool _minimapNavigationActive;
    private bool _rightMouseWasDown;
    private bool _helpVisible;
    private bool _victory;
    private bool _gameOver;
    private int _winnerTeam;
    private int _oreStockpile;
    private int _oreStockpileP2;
    private int _waveIndex;
    private string _bannerTitle = string.Empty;
    private string _bannerSubtitle = string.Empty;
    private readonly List<ProductionOrder> _productionQueueP2 = [];

    public int OreStockpile => LocalTeam == 0 ? _oreStockpile : _oreStockpileP2;

    public int OreGoal => IsMultiplayerMatch ? 0 : VictoryOreGoalValue;

    public int HeadquartersHealth => (int)MathF.Ceiling(LocalTeam == 0 ? _hqHealth : _hqHealthP2);

    public int SupplyTruckCount => _units.Count(unit => unit.IsAlive && unit.IsAllyOf(LocalTeam) && unit.Role == RtsUnitRole.Worker);

    public int RedGuardCount => _units.Count(unit => unit.IsAlive && unit.IsAllyOf(LocalTeam) && unit.Role == RtsUnitRole.Guard);

    public int TankHunterCount => _units.Count(unit => unit.IsAlive && unit.IsAllyOf(LocalTeam) && unit.Role == RtsUnitRole.TankHunter);

    public int BattlemasterCount => _units.Count(unit => unit.IsAlive && unit.IsAllyOf(LocalTeam) && unit.Role == RtsUnitRole.Battlemaster);

    public int RaiderCount => _units.Count(unit => unit.IsAlive && unit.IsEnemyOf(LocalTeam));

    public string WorkerBuildButtonText => GetProductionButtonText(RtsProductionType.Worker, "Q");

    public string GuardBuildButtonText => GetProductionButtonText(RtsProductionType.Guard, "E");

    public string TankHunterBuildButtonText => GetProductionButtonText(RtsProductionType.TankHunter, "W");

    public string BattlemasterBuildButtonText => GetProductionButtonText(RtsProductionType.Battlemaster, "D");

    public string BuildingBuildButtonText => GetProductionButtonText(RtsProductionType.Building, "R");

    public string DefenseTowerBuildButtonText => GetProductionButtonText(RtsProductionType.DefenseTower, "U");

    public string WarFactoryBuildButtonText => GetProductionButtonText(RtsProductionType.WarFactory, "T");

    public string ReactorBuildButtonText => GetProductionButtonText(RtsProductionType.Reactor, "Y");

    public string Slot1NameText => GetProductionLabel(RtsProductionType.Worker);
    public string Slot1CostText => GetSlotCostText(RtsProductionType.Worker, "Q");
    public string Slot2NameText => GetProductionLabel(RtsProductionType.Guard);
    public string Slot2CostText => GetSlotCostText(RtsProductionType.Guard, "E");
    public string Slot3NameText => GetProductionLabel(RtsProductionType.TankHunter);
    public string Slot3CostText => GetSlotCostText(RtsProductionType.TankHunter, "W");
    public string Slot4NameText => GetProductionLabel(RtsProductionType.Battlemaster);
    public string Slot4CostText => GetSlotCostText(RtsProductionType.Battlemaster, "D");
    public string Slot5NameText => GetProductionLabel(RtsProductionType.Building);
    public string Slot5CostText => GetSlotCostText(RtsProductionType.Building, "R");
    public string Slot6NameText => GetProductionLabel(RtsProductionType.WarFactory);
    public string Slot6CostText => GetSlotCostText(RtsProductionType.WarFactory, "T");
    public string Slot7NameText => GetProductionLabel(RtsProductionType.Reactor);
    public string Slot7CostText => GetSlotCostText(RtsProductionType.Reactor, "Y");
    public string Slot8NameText => GetProductionLabel(RtsProductionType.DefenseTower);
    public string Slot8CostText => GetSlotCostText(RtsProductionType.DefenseTower, "U");

    public string BackendLabel => Graphics.Backend == GraphicsBackend.Vulkan ? "Vulkan" : "Software";

    public bool HelpVisible => _helpVisible;

    public bool ShowCenterMessage => _victory || _gameOver || _bannerTimer > 0f;

    public string MessageTitle => ShowCenterMessage ? _bannerTitle : string.Empty;

    public string MessageSubtitle => ShowCenterMessage ? _bannerSubtitle : string.Empty;

    public string WaveText
    {
        get
        {
            if (_victory)
                return "MISSION COMPLETE";

            if (_gameOver)
            {
                if (IsMultiplayerMatch)
                    return _winnerTeam == LocalTeam ? "VICTORY" : "DEFEAT";
                return "BASE DESTROYED";
            }

            if (IsMultiplayerMatch)
                return $"PVP | YOUR UNITS {_units.Count(u => u.IsAlive && u.IsAllyOf(LocalTeam))} | ENEMY {_units.Count(u => u.IsAlive && u.IsEnemyOf(LocalTeam))}";

            if (RaiderCount > 0)
                return $"GLA ATTACK | {RaiderCount} HOSTILES";

            return $"WAVE {_waveIndex + 1} IN {_nextWaveTimer:0.0}s";
        }
    }

    public string ObjectiveText
    {
        get
        {
            if (_victory)
                return "VICTORY | PRESS R";

            if (_gameOver)
            {
                if (IsMultiplayerMatch)
                    return _winnerTeam == LocalTeam ? "VICTORY | PRESS R" : "DEFEAT | PRESS R";
                return "DEFEAT | PRESS R";
            }

            if (IsMultiplayerMatch)
                return "DESTROY ENEMY HQ";

            var remainingFunds = Math.Max(0, VictoryOreGoalValue - _oreStockpile);
            if (RaiderCount > 0)
                return $"GLA ATTACK | BEACON {(int)MathF.Ceiling(_hqHealthP2)} | ORE {remainingFunds}";

            return $"DESTROY BEACON | {(int)MathF.Ceiling(_hqHealthP2)} HP | ORE {remainingFunds}";
        }
    }

    public string UnitPanelName
    {
        get
        {
            var hoveredUnit = GetHoveredUnit();
            return hoveredUnit is null
                ? ObjectiveText
                : $"{hoveredUnit.Label.ToUpperInvariant()} | {hoveredUnit.Callsign}";
        }
    }

    public string UnitPanelHealth
    {
        get
        {
            var hoveredUnit = GetHoveredUnit();
            if (hoveredUnit is null)
                return SelectedSummary;

            var teamState = hoveredUnit.IsEnemyOf(LocalTeam) ? "HOSTILE" : "ALLY";
            return $"HP {(int)MathF.Ceiling(hoveredUnit.Health)}/{(int)MathF.Ceiling(hoveredUnit.MaxHealth)} | {teamState}";
        }
    }

    public string UnitPanelStats
    {
        get
        {
            var hoveredUnit = GetHoveredUnit();
            if (hoveredUnit is null)
                return ProductionModeText;

            if (hoveredUnit.Role == RtsUnitRole.Worker)
                return $"ORE {hoveredUnit.CarryOre}/{hoveredUnit.CarryCapacity} | {DescribeWorkerDirective(hoveredUnit).ToUpperInvariant()}";

            return $"ATK {(int)MathF.Ceiling(hoveredUnit.AttackDamage)} | RNG {(int)MathF.Ceiling(hoveredUnit.AttackRange)} | SPD {(int)MathF.Ceiling(hoveredUnit.Speed)}";
        }
    }

    public string UnitPanelOrders
    {
        get
        {
            var hoveredUnit = GetHoveredUnit();
            if (hoveredUnit is null)
                return ProductionSitesSummary;

            var directive = hoveredUnit.Role == RtsUnitRole.Worker
                ? DescribeWorkerDirective(hoveredUnit)
                : DescribeCombatDirective(hoveredUnit);
            return $"{directive.ToUpperInvariant()} | {DescribeSector(hoveredUnit.Position).ToUpperInvariant()}";
        }
    }

    public string SelectedSummary
    {
        get
        {
            var selectedUnits = GetSelectedUnits();
            if (selectedUnits.Count == 0)
                return "NO SELECTION";

            if (selectedUnits.Count == 1)
            {
                var unit = selectedUnits[0];
                if (unit.Role == RtsUnitRole.Worker)
                {
                    if (unit.OrderType == RtsUnitOrderType.Harvest && TryGetResourceNode(unit.AssignedNodeIndex, out var node))
                    {
                        var state = unit.ReturningToBase ? "Returning" : $"Harvesting {node.Name}";
                        return $"SUPPLY TRUCK | {unit.CarryOre} | {state.ToUpperInvariant()}";
                    }

                    return $"SUPPLY TRUCK | {unit.CarryOre} | IDLE";
                }

                return $"{unit.Label.ToUpperInvariant()} | HP {(int)MathF.Ceiling(unit.Health)}/{(int)MathF.Ceiling(unit.MaxHealth)}";
            }

            var trucks = selectedUnits.Count(unit => unit.Role == RtsUnitRole.Worker);
            var infantry = selectedUnits.Count(unit => unit.Role == RtsUnitRole.Guard || unit.Role == RtsUnitRole.TankHunter);
            var armor = selectedUnits.Count(unit => unit.Role == RtsUnitRole.Battlemaster);
            return $"SELECTED {selectedUnits.Count} | INF {infantry} | ARM {armor} | TRK {trucks}";
        }
    }

    public string SelectionDetail
    {
        get
        {
            var selectedUnits = GetSelectedUnits();
            if (selectedUnits.Count == 0)
                return "SHIFT ADD | CTRL CUT | SPACE HQ";

            float totalHealth = 0f;
            float maxHealth = 0f;
            var moving = 0;
            var harvesting = 0;
            var returning = 0;
            var carriedOre = 0;

            foreach (var unit in selectedUnits)
            {
                totalHealth += unit.Health;
                maxHealth += unit.MaxHealth;
                carriedOre += unit.CarryOre;

                if (unit.Role == RtsUnitRole.Worker && unit.OrderType == RtsUnitOrderType.Harvest)
                {
                    if (unit.ReturningToBase)
                        returning++;
                    else
                        harvesting++;
                }

                if (unit.HasMoveTarget && unit.OrderType == RtsUnitOrderType.Move)
                    moving++;
            }

            return $"HP {(int)MathF.Ceiling(totalHealth)}/{(int)MathF.Ceiling(maxHealth)} | MOVE {moving} | MINE {harvesting} | BACK {returning} | ORE {carriedOre}";
        }
    }

    public string QueueSummary
    {
        get
        {
            var queue = LocalTeam == 0 ? _productionQueue : _productionQueueP2;
            if (queue.Count == 0)
                return $"QUEUE 0/{QueueLimit}";

            var order = queue[0];
            return $"BUILDING: {order.Label.ToUpperInvariant()} {order.RemainingTime:0.0}s | {queue.Count}/{QueueLimit}";
        }
    }

    public string ProductionModeText => _activePlacementType is { } productionType
        ? $"PLACE {GetPlacementLabel(productionType).ToUpperInvariant()} | CLICK MAP | RMB CANCEL"
        : "SELECT BUILD OPTION";

    public string ProductionSitesSummary => $"STRUCTURES | Send a Supply Truck to build blueprints";

    public string RallySummary => $"RALLY {DescribeSector(_rallyPoint).ToUpperInvariant()}";

    public string CameraSummary
    {
        get
        {
            var cameraCenter = GetCameraCenterWorld();
            var cursor = GetCursorWorldPosition();
            return $"CAM {DescribeSector(cameraCenter).ToUpperInvariant()} | {cursor.X:0}/{cursor.Y:0}";
        }
    }

    public string ForceSummary
    {
        get
        {
            var totalFriendly = _units.Count(unit => unit.IsAlive && unit.IsAllyOf(LocalTeam));
            var queue = LocalTeam == 0 ? _productionQueue : _productionQueueP2;
            return $"UNITS {totalFriendly} | HOSTILES {RaiderCount} | QUEUE {queue.Count}/{QueueLimit}";
        }
    }

    public string EconomySummary
    {
        get
        {
            var ore = LocalTeam == 0 ? _oreStockpile : _oreStockpileP2;
            var activeSupply = _resourceNodes.Count(node => !node.IsDepleted);
            var fieldSupply = _resourceNodes.Sum(node => Math.Max(0, node.RemainingOre));
            var carriedSupply = _units.Where(unit => unit.IsAlive && unit.IsAllyOf(LocalTeam) && unit.Role == RtsUnitRole.Worker).Sum(unit => unit.CarryOre);
            return $"{ore} | SUPPLY {activeSupply}/{_resourceNodes.Count} | CARRY {carriedSupply}";
        }
    }

    public string RosterLine1 => GetSelectionRosterLine(0);

    public string RosterLine2 => GetSelectionRosterLine(1);

    public string RosterLine3 => GetSelectionRosterLine(2);

    public string MapHintText => string.Empty;

    public string HintText =>
        "DRAG | SHIFT | CTRL | RMB | Q/E/R/T | F1";

    public override void OnLoad()
    {
        _audio = Engine.Scripts.GetScript<RtsAudioScript>()
            ?? throw new InvalidOperationException("RtsAudioScript must be registered before RtsGameScript loads.");

        OnMultiplayerLoaded();
        ResetScenario();
    }

    public override void OnUpdate(float deltaTime)
    {
        var leftMouseDown = IsMouseDown(MouseButton.Left);
        var middleMouseDown = IsMouseDown(MouseButton.Middle);
        var rightMouseDown = IsMouseDown(MouseButton.Right);

        if (!_matchRunning)
        {
            _leftMouseWasDown = leftMouseDown;
            _middleMouseWasDown = middleMouseDown;
            _rightMouseWasDown = rightMouseDown;
            return;
        }

        if (_sessionMode == RtsSessionMode.Client)
        {
            UpdateClientReplica(deltaTime, leftMouseDown, middleMouseDown, rightMouseDown);
            _leftMouseWasDown = leftMouseDown;
            _middleMouseWasDown = middleMouseDown;
            _rightMouseWasDown = rightMouseDown;
            return;
        }

        if (IsKeyPressed(KeyCode.F1))
            _helpVisible = !_helpVisible;

        UpdateBanner(deltaTime);
        UpdateShotEffects(deltaTime);
        UpdateTacticalSignals(deltaTime);

        if (_victory || _gameOver)
        {
            if (IsKeyPressed(KeyCode.R) || IsKeyPressed(KeyCode.Enter))
                ResetScenario();

            _leftMouseWasDown = leftMouseDown;
            _middleMouseWasDown = middleMouseDown;
            _rightMouseWasDown = rightMouseDown;
            return;
        }

        _missionTime += deltaTime;
        HandleHotkeys();
        HandleHudButtons(leftMouseDown);
        var placementInputConsumed = HandleStructurePlacement(leftMouseDown, rightMouseDown);
        HandleNavigation(leftMouseDown, middleMouseDown);
        UpdateCamera(deltaTime);
        HandleSelection(leftMouseDown, placementInputConsumed);
        HandleCommands(rightMouseDown, placementInputConsumed);
        UpdateProduction(deltaTime);
        if (!IsMultiplayerMatch)
            UpdateEnemyWaves(deltaTime);
        UpdateUnits(deltaTime);
        UpdateStructures(deltaTime);
        ResolveUnitSeparation();
        CleanupDestroyedUnits();
        CleanupDestroyedStructures();
        CheckScenarioState();
        UpdateHostedSnapshot(deltaTime);

        _leftMouseWasDown = leftMouseDown;
        _middleMouseWasDown = middleMouseDown;
        _rightMouseWasDown = rightMouseDown;
    }

    public override void OnDraw()
    {
        DrawBattlefield();
        DrawStructures();
        DrawResourceNodes();
        DrawShotEffects();
        DrawSelectionOverlays();
        DrawUnits();
        DrawTacticalSignals();
        DrawSelectionRectangle();
        DrawMinimap();
    }

    private void ResetScenario()
    {
        _structures.Clear();
        _units.Clear();
        _resourceNodes.Clear();
        _shotEffects.Clear();
        _productionQueue.Clear();
        _productionQueueP2.Clear();
        _activePlacementType = null;
        _oreStockpile = 2500;
        _oreStockpileP2 = 2500;
        _hqHealth = HeadquartersMaxHealth;
        _hqHealthP2 = HeadquartersMaxHealth;
        _waveIndex = 0;
        _missionTime = 0f;
        _nextWaveTimer = 20f;
        _bannerTimer = 4.5f;
        _bannerTitle = IsMultiplayerMatch ? "1v1 Battle Engaged" : "Command Center Online";
        _bannerSubtitle = IsMultiplayerMatch
            ? "Destroy the enemy headquarters to win. Build units and structures to overwhelm your opponent."
            : "Deploy supply trucks to harvest resources. Build barracks and war factory to train combat units. Stockpile 10000 to win.";
        _commandPulsePosition = Vector2.Zero;
        _navigationPulsePosition = Vector2.Zero;
        _commandPulseTimer = 0f;
        _navigationPulseTimer = 0f;
        _helpVisible = false;
        _victory = false;
        _gameOver = false;
        _winnerTeam = -1;
        _selectionActive = false;
        _leftMouseWasDown = false;
        _middleMouseWasDown = false;
        _minimapNavigationActive = false;
        _rightMouseWasDown = false;
        _rallyPoint = HeadquartersPosition + new Vector2(220f, -40f);
        _rallyPointP2 = HeadquartersPositionP2 + new Vector2(-220f, 40f);
        _cameraPosition = ClampCamera(HeadquartersPosition - new Vector2(240f, 280f));

        RtsStructure.ResetIds();
        RtsUnit.ResetIds();
        _resourceNodes.Add(new RtsResourceNode("Supply Depot Alpha", new Vector2(670f, 930f), 4000));
        _resourceNodes.Add(new RtsResourceNode("Supply Depot Bravo", new Vector2(1020f, 760f), 5000));
        _resourceNodes.Add(new RtsResourceNode("Supply Depot Charlie", new Vector2(1360f, 520f), 5000));
        _resourceNodes.Add(new RtsResourceNode("Supply Depot Delta", new Vector2(1640f, 860f), 3500));

        // Team 0 (Player 1) starting units - bottom left
        SpawnPlayerUnit(0, RtsUnitRole.Worker, HeadquartersPosition + new Vector2(-44f, 24f), sendToRally: false);
        SpawnPlayerUnit(0, RtsUnitRole.Worker, HeadquartersPosition + new Vector2(-12f, 54f), sendToRally: false);
        SpawnPlayerUnit(0, RtsUnitRole.Guard, HeadquartersPosition + new Vector2(76f, -18f), sendToRally: true);
        SpawnPlayerUnit(0, RtsUnitRole.Guard, HeadquartersPosition + new Vector2(100f, 10f), sendToRally: true);

        if (IsMultiplayerMatch)
        {
            // Team 1 (Player 2) starting units - top right
            SpawnPlayerUnit(1, RtsUnitRole.Worker, HeadquartersPositionP2 + new Vector2(44f, -24f), sendToRally: false);
            SpawnPlayerUnit(1, RtsUnitRole.Worker, HeadquartersPositionP2 + new Vector2(12f, -54f), sendToRally: false);
            SpawnPlayerUnit(1, RtsUnitRole.Guard, HeadquartersPositionP2 + new Vector2(-76f, 18f), sendToRally: true);
            SpawnPlayerUnit(1, RtsUnitRole.Guard, HeadquartersPositionP2 + new Vector2(-100f, -10f), sendToRally: true);
        }

        SelectPlayerUnits(unit => unit.Role == RtsUnitRole.Worker);
        _audio.PlayMissionStart();
    }

    private void UpdateBanner(float deltaTime)
    {
        if (!_victory && !_gameOver)
            _bannerTimer = Math.Max(0f, _bannerTimer - deltaTime);
    }

    private void UpdateShotEffects(float deltaTime)
    {
        for (var index = _shotEffects.Count - 1; index >= 0; index--)
        {
            _shotEffects[index].RemainingTime -= deltaTime;
            if (_shotEffects[index].RemainingTime <= 0f)
                _shotEffects.RemoveAt(index);
        }
    }

    private void UpdateTacticalSignals(float deltaTime)
    {
        _commandPulseTimer = Math.Max(0f, _commandPulseTimer - deltaTime);
        _navigationPulseTimer = Math.Max(0f, _navigationPulseTimer - deltaTime);
    }

    private void UpdateCamera(float deltaTime)
    {
        if (_minimapNavigationActive)
            return;

        var pan = Vector2.Zero;
        if (IsKeyDown(KeyCode.Left))
            pan += Vector2.Left;
        if (IsKeyDown(KeyCode.Right))
            pan += Vector2.Right;
        if (IsKeyDown(KeyCode.Up))
            pan += Vector2.Up;
        if (IsKeyDown(KeyCode.Down))
            pan += Vector2.Down;

        if (!_selectionActive && !IsPointInsideMinimap(MousePosition))
        {
            if (MousePosition.X <= EdgeScrollMargin)
                pan += Vector2.Left;
            if (MousePosition.X >= Engine.Width - EdgeScrollMargin)
                pan += Vector2.Right;
            if (MousePosition.Y <= EdgeScrollMargin)
                pan += Vector2.Up;
            if (MousePosition.Y >= Engine.Height - EdgeScrollMargin)
                pan += Vector2.Down;
        }

        if (pan.LengthSquared <= 0f)
            return;

        _cameraPosition = ClampCamera(_cameraPosition + (pan.Normalized * CameraPanSpeed * deltaTime));
    }

    private void HandleHotkeys()
    {
        if (IsKeyPressed(KeyCode.Escape))
            CancelStructurePlacement();

        if (IsKeyPressed(KeyCode.Q))
            QueueProduction(RtsProductionType.Worker);

        if (IsKeyPressed(KeyCode.E))
            QueueProduction(RtsProductionType.Guard);

        if (IsKeyPressed(KeyCode.W))
            QueueProduction(RtsProductionType.TankHunter);

        if (IsKeyPressed(KeyCode.D))
            QueueProduction(RtsProductionType.Battlemaster);

        if (IsKeyPressed(KeyCode.R))
            QueueProduction(RtsProductionType.Building);

        if (IsKeyPressed(KeyCode.T))
            QueueProduction(RtsProductionType.WarFactory);

        if (IsKeyPressed(KeyCode.Y))
            QueueProduction(RtsProductionType.Reactor);

        if (IsKeyPressed(KeyCode.U))
            QueueProduction(RtsProductionType.DefenseTower);

        if (IsKeyPressed(KeyCode.D1))
            SelectPlayerUnits(unit => unit.Role == RtsUnitRole.Worker);

        if (IsKeyPressed(KeyCode.D2))
            SelectPlayerUnits(unit => unit.Role == RtsUnitRole.Guard);

        if (IsKeyPressed(KeyCode.D3))
            SelectPlayerUnits(unit => unit.IsAllyOf(LocalTeam));

        if (IsKeyPressed(KeyCode.Space))
            FocusCameraOnSelection();
    }

    private void HandleHudButtons(bool leftMouseDown)
    {
        if (!leftMouseDown || _leftMouseWasDown)
            return;

        if (IsPointInsideUiElement(MousePosition, "build-slot-1"))
        {
            QueueProduction(RtsProductionType.Worker);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "build-slot-2"))
        {
            QueueProduction(RtsProductionType.Guard);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "build-slot-3"))
        {
            QueueProduction(RtsProductionType.TankHunter);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "build-slot-4"))
        {
            QueueProduction(RtsProductionType.Battlemaster);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "build-slot-5"))
        {
            QueueProduction(RtsProductionType.Building);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "build-slot-6"))
        {
            QueueProduction(RtsProductionType.WarFactory);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "build-slot-7"))
        {
            QueueProduction(RtsProductionType.Reactor);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "build-slot-8"))
            QueueProduction(RtsProductionType.DefenseTower);
    }

    private bool HandleStructurePlacement(bool leftMouseDown, bool rightMouseDown)
    {
        if (_activePlacementType is not { } productionType)
            return false;

        if (rightMouseDown && !_rightMouseWasDown)
        {
            CancelStructurePlacement();
            return true;
        }

        if (!leftMouseDown || _leftMouseWasDown || IsPointInsideMinimap(MousePosition) || IsPointerInsideBlockingHud(MousePosition))
            return false;

        var worldPosition = ScreenToWorld(MousePosition);
        worldPosition = ClampPointToWorld(worldPosition);

        if (QueueProduction(productionType, worldPosition))
            _activePlacementType = null;

        return true;
    }

    private void HandleNavigation(bool leftMouseDown, bool middleMouseDown)
    {
        var mouseOnMinimap = IsPointInsideMinimap(MousePosition);
        if (!mouseOnMinimap && IsPointerInsideBlockingHud(MousePosition))
        {
            _minimapNavigationActive = false;
            return;
        }

        if (leftMouseDown && !_leftMouseWasDown && mouseOnMinimap)
        {
            _selectionActive = false;
            _minimapNavigationActive = true;
            var jumpTarget = MinimapToWorld(MousePosition);
            CenterCameraOnWorld(
                jumpTarget,
                announce: true,
                markPulse: true,
                title: "Minimap Jump",
                subtitle: $"Camera recentered on the {DescribeSector(jumpTarget)}.");
        }
        else if (!leftMouseDown)
        {
            _minimapNavigationActive = false;
        }

        if (_minimapNavigationActive && leftMouseDown)
            CenterCameraOnWorld(MinimapToWorld(MousePosition), announce: false, markPulse: false);

        if (middleMouseDown && !_middleMouseWasDown)
        {
            var snapTarget = mouseOnMinimap ? MinimapToWorld(MousePosition) : ScreenToWorld(MousePosition);
            var title = mouseOnMinimap ? "Minimap Snap" : "Tactical Snap";
            CenterCameraOnWorld(
                snapTarget,
                announce: true,
                markPulse: true,
                title: title,
                subtitle: $"Camera snapped to the {DescribeSector(snapTarget)}.");
        }
    }

    private void HandleSelection(bool leftMouseDown, bool placementInputConsumed)
    {
        if (_minimapNavigationActive || placementInputConsumed || _activePlacementType is not null)
            return;

        if (leftMouseDown && !_leftMouseWasDown)
        {
            if (IsPointerInsideBlockingHud(MousePosition))
                return;

            _selectionActive = true;
            _selectionStartScreen = MousePosition;
            _selectionEndScreen = MousePosition;
        }

        if (_selectionActive && leftMouseDown)
            _selectionEndScreen = MousePosition;

        if (_selectionActive && !leftMouseDown && _leftMouseWasDown)
        {
            FinalizeSelection();
            _selectionActive = false;
        }
    }

    private void HandleCommands(bool rightMouseDown, bool placementInputConsumed)
    {
        if (placementInputConsumed || _activePlacementType is not null)
            return;

        if (!rightMouseDown || _rightMouseWasDown || IsPointInsideMinimap(MousePosition) || IsPointerInsideBlockingHud(MousePosition))
            return;

        var selectedUnits = GetSelectedUnits();
        var worldPosition = ScreenToWorld(MousePosition);
        if (selectedUnits.Count == 0)
        {
            _rallyPoint = worldPosition;
            SetCommandPulse(_rallyPoint);
            _audio.PlayRally();
            ShowTransientMessage("Rally Updated", $"Fresh units will stage in the {DescribeSector(_rallyPoint)}.", 1.1f);
            return;
        }

        IssueOrders(selectedUnits, worldPosition, LocalTeam);
    }

    private void FinalizeSelection()
    {
        var additive = IsKeyDown(KeyCode.Shift);
        var subtractive = IsKeyDown(KeyCode.Control);
        if (!additive && !subtractive)
            ClearSelection();

        var drag = _selectionEndScreen - _selectionStartScreen;
        if (drag.LengthSquared >= SelectionThreshold * SelectionThreshold)
        {
            var selectionRect = CreateRectangle(ScreenToWorld(_selectionStartScreen), ScreenToWorld(_selectionEndScreen));
            foreach (var unit in _units)
            {
                if (unit.IsAllyOf(LocalTeam) && unit.IsAlive && selectionRect.Contains(unit.Position))
                    unit.Selected = !subtractive;
            }

            return;
        }

        var clickedUnit = FindPlayerUnit(ScreenToWorld(_selectionEndScreen), 22f);
        if (clickedUnit is not null)
            clickedUnit.Selected = !subtractive;
    }

    private bool QueueProduction(RtsProductionType productionType)
    {
        if (IsStructureProduction(productionType))
        {
            BeginStructurePlacement(productionType);
            return false;
        }

        return QueueProduction(productionType, null);
    }

    private void QueueProductionForTeam(int team, RtsProductionType productionType)
    {
        if (IsStructureProduction(productionType))
            return;

        QueueProductionForTeam(team, productionType, null);
    }

    private void QueueProductionForTeam(int team, RtsProductionType productionType, Vector2? reservedSite)
    {
        var queue = team == 0 ? _productionQueue : _productionQueueP2;
        ref var stockpile = ref (team == 0 ? ref _oreStockpile : ref _oreStockpileP2);

        if (!IsStructureProduction(productionType) && queue.Count >= QueueLimit)
            return;

        var cost = GetProductionCost(productionType);
        if (stockpile < cost)
            return;

        stockpile -= cost;

        if (reservedSite is { } site && TryMapToStructureType(productionType, out _))
        {
            var structure = new RtsStructure(RtsStructureType.Building, site) { Team = team };
            if (TryMapToStructureType(productionType, out var actualType))
                structure = new RtsStructure(actualType, site) { Team = team };
            structure.UnderConstruction = true;
            structure.ConstructionProgress = 0f;
            structure.ConstructionTime = GetProductionBuildTime(productionType);
            structure.Health = 1f;
            _structures.Add(structure);
            _audio.PlayQueue(productionType);
            return;
        }

        var buildTime = GetProductionBuildTime(productionType);
        queue.Add(new ProductionOrder(productionType, buildTime, null));
        _audio.PlayQueue(productionType);
    }

    private bool QueueProduction(RtsProductionType productionType, Vector2? reservedSite)
    {
        var availability = GetProductionAvailability(productionType);
        if (availability != ProductionAvailability.Ready)
        {
            ShowProductionUnavailable(productionType, availability);
            return false;
        }

        var cost = GetProductionCost(productionType);
        _oreStockpile -= cost;

        if (reservedSite is { } site && TryMapToStructureType(productionType, out var structureType))
        {
            var structure = new RtsStructure(structureType, site);
            structure.UnderConstruction = true;
            structure.ConstructionProgress = 0f;
            structure.ConstructionTime = GetProductionBuildTime(productionType);
            structure.Health = 1f;
            _structures.Add(structure);
            _audio.PlayQueue(productionType);
            ShowTransientMessage(
                $"{GetProductionLabel(productionType)} Blueprint",
                $"{GetProductionLabel(productionType)} placed in {DescribeSector(site)}. Send a supply truck to build it.",
                1f);
            return true;
        }

        var buildTime = GetProductionBuildTime(productionType);
        _productionQueue.Add(new ProductionOrder(productionType, buildTime, null));
        _audio.PlayQueue(productionType);
        ShowTransientMessage(
            $"{GetProductionLabel(productionType)} Queued",
            $"{GetProductionLabel(productionType)} fabrication started. Cost {cost}.",
            0.9f);
        return true;
    }

    private ProductionAvailability GetProductionAvailability(RtsProductionType productionType)
    {
        if (!IsStructureProduction(productionType) && _productionQueue.Count >= QueueLimit)
            return ProductionAvailability.QueueFull;

        return _oreStockpile >= GetProductionCost(productionType)
            ? ProductionAvailability.Ready
            : ProductionAvailability.InsufficientOre;
    }

    private void BeginStructurePlacement(RtsProductionType productionType)
    {
        var availability = GetProductionAvailability(productionType);
        if (availability != ProductionAvailability.Ready)
        {
            ShowProductionUnavailable(productionType, availability);
            return;
        }

        _activePlacementType = productionType;
        _selectionActive = false;
        _minimapNavigationActive = false;
        ShowTransientMessage(
            $"Place {GetProductionLabel(productionType)}",
            $"Click anywhere on the map to place the blueprint. Right click or Esc cancels.",
            1.15f);
    }

    private bool CancelStructurePlacement()
    {
        if (_activePlacementType is not { } productionType)
            return false;

        _activePlacementType = null;
        _selectionActive = false;
        ShowTransientMessage(
            "Placement Cancelled",
            $"{GetProductionLabel(productionType)} placement was cancelled.",
            0.8f);
        return true;
    }

    private void ShowProductionUnavailable(RtsProductionType productionType, ProductionAvailability availability)
    {
        _audio.PlayDenied();
        var cost = GetProductionCost(productionType);

        switch (availability)
        {
            case ProductionAvailability.QueueFull:
                ShowTransientMessage("Queue Full", "The foundry can hold four production orders at a time.", 1f);
                break;

            case ProductionAvailability.SiteFull:
                ShowTransientMessage("Pads Full", $"All {GetPlacementLabel(productionType).ToLowerInvariant()} pads are already reserved.", 1f);
                break;

            default:
                ShowTransientMessage("Ore Low", $"Need {cost} ore to queue a {GetProductionLabel(productionType).ToLowerInvariant()}.", 1f);
                break;
        }
    }

    private void UpdateProduction(float deltaTime)
    {
        UpdateProductionQueue(_productionQueue, 0, deltaTime);
        if (IsMultiplayerMatch)
            UpdateProductionQueue(_productionQueueP2, 1, deltaTime);
    }

    private void UpdateProductionQueue(List<ProductionOrder> queue, int team, float deltaTime)
    {
        if (queue.Count == 0)
            return;

        var order = queue[0];
        order.RemainingTime -= deltaTime;
        if (order.RemainingTime > 0f)
            return;

        queue.RemoveAt(0);
        if (TryMapToUnitRole(order.Type, out var role))
        {
            var hqPosition = team == 0 ? HeadquartersPosition : HeadquartersPositionP2;
            var rallyPoint = team == 0 ? _rallyPoint : _rallyPointP2;
            var spawnOffset = new Vector2(84f + (_random.NextSingle() * 26f), -34f + (_random.NextSingle() * 68f));
            if (team == 1)
                spawnOffset = new Vector2(-84f - (_random.NextSingle() * 26f), 34f - (_random.NextSingle() * 68f));
            SpawnPlayerUnit(team, role, hqPosition + spawnOffset, sendToRally: true);
            _audio.PlayUnitReady();
            var sectorName = DescribeSector(rallyPoint);
            ShowTransientMessage(
                $"{GetProductionLabel(order.Type)} Ready",
                $"A new {GetProductionLabel(order.Type).ToLowerInvariant()} is moving toward the {sectorName}.",
                1f);
            return;
        }
    }

    private void UpdateEnemyWaves(float deltaTime)
    {
        _nextWaveTimer -= deltaTime;
        if (_nextWaveTimer > 0f)
            return;

        _waveIndex++;
        var raiderCount = Math.Min(12, 2 + _waveIndex);
        for (var index = 0; index < raiderCount; index++)
        {
            var spawn = EnemySpawnPoints[index % EnemySpawnPoints.Length]
                + new Vector2((_random.NextSingle() * 70f) - 35f, (_random.NextSingle() * 70f) - 35f);

            var raider = new RtsUnit(RtsUnitRole.Raider, spawn)
            {
                Team = 1,
                MaxHealth = 76f + ((_waveIndex - 1) * 8f),
                AttackDamage = 9f + ((_waveIndex - 1) * 1.1f),
                Speed = 92f + Math.Min(18f, _waveIndex * 2f),
                DetectionRange = 250f + (_waveIndex * 6f)
            };
            raider.Health = raider.MaxHealth;
            _units.Add(raider);
        }

        _nextWaveTimer = Math.Max(8f, WaveInterval - Math.Min(8f, _waveIndex * 0.85f));
        _audio.PlayRaidAlert();
        ShowTransientMessage(
            $"Raid {_waveIndex} Inbound",
            $"{raiderCount} raiders crossed the ridge. Queue guards with E and keep the ore moving.",
            2f);
    }

    private void UpdateUnits(float deltaTime)
    {
        for (var index = 0; index < _units.Count; index++)
        {
            var unit = _units[index];
            if (!unit.IsAlive)
                continue;

            unit.AttackCooldown = Math.Max(0f, unit.AttackCooldown - deltaTime);

            switch (unit.Role)
            {
                case RtsUnitRole.Worker:
                    UpdateWorker(unit, deltaTime);
                    break;

                case RtsUnitRole.Guard:
                case RtsUnitRole.TankHunter:
                case RtsUnitRole.Battlemaster:
                    UpdateGuard(unit, deltaTime);
                    break;

                default:
                    UpdateRaider(unit, deltaTime);
                    break;
            }
        }
    }

    private void UpdateStructures(float deltaTime)
    {
        for (var index = 0; index < _structures.Count; index++)
        {
            var structure = _structures[index];
            if (!structure.IsAlive)
                continue;

            structure.AttackCooldown = Math.Max(0f, structure.AttackCooldown - deltaTime);
            if (structure.Type == RtsStructureType.DefenseTower)
                UpdateDefenseTower(structure);
        }
    }

    private void UpdateWorker(RtsUnit worker, float deltaTime)
    {
        var workerHq = worker.Team == 0 ? HeadquartersPosition : HeadquartersPositionP2;

        if (worker.OrderType == RtsUnitOrderType.Harvest && TryGetResourceNode(worker.AssignedNodeIndex, out var node))
        {
            if ((worker.CarryOre >= worker.CarryCapacity || worker.ReturningToBase || node.IsDepleted) && worker.CarryOre > 0)
            {
                worker.ReturningToBase = true;
                if (MoveUnit(worker, workerHq, deltaTime, HeadquartersHalfWidth + 12f))
                {
                    if (worker.Team == 0)
                        _oreStockpile += worker.CarryOre;
                    else
                        _oreStockpileP2 += worker.CarryOre;
                    _audio.PlayDeposit();
                    worker.CarryOre = 0;
                    worker.HarvestProgress = 0f;
                    worker.ReturningToBase = false;
                    if (node.IsDepleted)
                    {
                        worker.OrderType = RtsUnitOrderType.Idle;
                        worker.AssignedNodeIndex = -1;
                        worker.HasMoveTarget = false;
                    }
                }

                return;
            }

            if (node.IsDepleted)
            {
                worker.OrderType = RtsUnitOrderType.Idle;
                worker.AssignedNodeIndex = -1;
                worker.HasMoveTarget = false;
                return;
            }

            if (MoveUnit(worker, node.Position, deltaTime, node.Radius + worker.Radius + 10f))
            {
                worker.HarvestProgress += deltaTime;
                if (worker.HarvestProgress >= HarvestInterval)
                {
                    worker.HarvestProgress -= HarvestInterval;
                    var minedOre = Math.Min(HarvestAmount, Math.Min(node.RemainingOre, worker.CarryCapacity - worker.CarryOre));
                    if (minedOre > 0)
                    {
                        node.RemainingOre -= minedOre;
                        worker.CarryOre += minedOre;
                        _audio.PlayMine();
                    }

                    if (worker.CarryOre >= worker.CarryCapacity || node.IsDepleted)
                        worker.ReturningToBase = true;
                }
            }

            return;
        }

        if (worker.OrderType == RtsUnitOrderType.Build)
        {
            var buildTarget = FindStructureById(worker.AssignedStructureId);
            if (buildTarget is null || !buildTarget.IsAlive || !buildTarget.UnderConstruction)
            {
                worker.OrderType = RtsUnitOrderType.Idle;
                worker.AssignedStructureId = -1;
                worker.HasMoveTarget = false;
                UpdateMoveOrder(worker, deltaTime);
                return;
            }

            if (MoveUnit(worker, buildTarget.Position, deltaTime, buildTarget.Radius + worker.Radius + 8f))
            {
                buildTarget.ConstructionProgress += deltaTime;
                if (buildTarget.ConstructionProgress >= buildTarget.ConstructionTime)
                {
                    buildTarget.UnderConstruction = false;
                    buildTarget.ConstructionProgress = buildTarget.ConstructionTime;
                    buildTarget.Health = buildTarget.MaxHealth;
                    worker.OrderType = RtsUnitOrderType.Idle;
                    worker.AssignedStructureId = -1;
                    worker.HasMoveTarget = false;
                    _audio.PlayUnitReady();
                    ShowTransientMessage(
                        $"{buildTarget.Label} Online",
                        $"{buildTarget.Label} construction complete in the {DescribeSector(buildTarget.Position)}.",
                        1f);
                }
                else
                {
                    var progress = buildTarget.ConstructionProgress / buildTarget.ConstructionTime;
                    buildTarget.Health = Math.Max(1f, progress * buildTarget.MaxHealth);
                }
            }

            return;
        }

        worker.AssignedNodeIndex = -1;
        worker.AssignedStructureId = -1;
        worker.ReturningToBase = false;
        worker.HarvestProgress = 0f;
        UpdateMoveOrder(worker, deltaTime);
    }

    private void UpdateGuard(RtsUnit guard, float deltaTime)
    {
        var target = FindNearestUnit(guard.Position, unit => unit.IsEnemyOf(guard.Team) && unit.IsAlive, guard.DetectionRange);
        if (target is not null)
        {
            var distance = Vector2.Distance(guard.Position, target.Position);
            if (distance <= guard.AttackRange)
            {
                if (guard.AttackCooldown <= 0f)
                    FireAtUnit(guard, target, new Color(124, 243, 255));
            }
            else
            {
                MoveUnit(guard, target.Position, deltaTime, guard.AttackRange * 0.82f);
            }

            return;
        }

        var structureTarget = FindNearestStructure(guard.Position, structure => structure.IsAlive && structure.Team != guard.Team, guard.DetectionRange);
        if (structureTarget is not null)
        {
            var distance = Vector2.Distance(guard.Position, structureTarget.Position);
            if (distance <= guard.AttackRange + structureTarget.Radius)
            {
                if (guard.AttackCooldown <= 0f)
                {
                    guard.AttackCooldown = guard.AttackInterval;
                    structureTarget.Health = Math.Max(0f, structureTarget.Health - guard.AttackDamage);
                    _shotEffects.Add(new ShotEffect(guard.Position, structureTarget.Position, new Color(255, 232, 132), 0.12f));
                    if (guard.Role == RtsUnitRole.Guard) _audio.PlayGuardFire();
                }
            }
            else
            {
                MoveUnit(guard, structureTarget.Position, deltaTime, guard.AttackRange + structureTarget.Radius - 2f);
            }

            return;
        }

        if (!IsMultiplayerMatch && guard.Team == 0)
        {
            var distanceToBeacon = Vector2.Distance(guard.Position, EnemyBeaconPosition);
            if (distanceToBeacon <= EnemyBeaconHalfWidth + guard.AttackRange)
            {
                if (guard.AttackCooldown <= 0f)
                {
                    guard.AttackCooldown = guard.AttackInterval;
                    _hqHealthP2 = Math.Max(0f, _hqHealthP2 - guard.AttackDamage);
                    _shotEffects.Add(new ShotEffect(guard.Position, EnemyBeaconPosition, new Color(255, 232, 132), 0.12f));
                    if (guard.Role == RtsUnitRole.Guard) _audio.PlayGuardFire();
                }

                return;
            }

            if (distanceToBeacon <= guard.DetectionRange)
            {
                MoveUnit(guard, EnemyBeaconPosition, deltaTime, EnemyBeaconHalfWidth + guard.AttackRange - 4f);
                return;
            }

            UpdateMoveOrder(guard, deltaTime);
            return;
        }

        var enemyHqPosition = guard.Team == 0 ? HeadquartersPositionP2 : HeadquartersPosition;
        var distanceToHq = Vector2.Distance(guard.Position, enemyHqPosition);
        if (distanceToHq <= HeadquartersHalfWidth + guard.AttackRange)
        {
            if (guard.AttackCooldown <= 0f)
            {
                guard.AttackCooldown = guard.AttackInterval;
                if (guard.Team == 0)
                    _hqHealthP2 = Math.Max(0f, _hqHealthP2 - guard.AttackDamage);
                else
                    _hqHealth = Math.Max(0f, _hqHealth - guard.AttackDamage);

                _shotEffects.Add(new ShotEffect(guard.Position, enemyHqPosition, new Color(255, 232, 132), 0.12f));
                if (guard.Role == RtsUnitRole.Guard) _audio.PlayGuardFire();
            }
            return;
        }
        else if (guard.OrderType != RtsUnitOrderType.Move && distanceToHq <= guard.DetectionRange)
        {
            MoveUnit(guard, enemyHqPosition, deltaTime, HeadquartersHalfWidth + guard.AttackRange - 4f);
            return;
        }

        UpdateMoveOrder(guard, deltaTime);
    }

    private void UpdateDefenseTower(RtsStructure tower)
    {
        if (tower.UnderConstruction)
            return;

        var target = FindNearestUnit(tower.Position, unit => unit.IsEnemyOf(tower.Team) && unit.IsAlive, tower.DetectionRange);
        if (target is null)
            return;

        var distance = Vector2.Distance(tower.Position, target.Position);
        if (distance > tower.AttackRange + target.Radius || tower.AttackCooldown > 0f)
            return;

        tower.AttackCooldown = tower.AttackInterval;
        target.Health = Math.Max(0f, target.Health - tower.AttackDamage);
        _shotEffects.Add(new ShotEffect(tower.Position, target.Position, new Color(255, 232, 132), 0.12f));
        _audio.PlayGuardFire();
    }

    private void UpdateRaider(RtsUnit raider, float deltaTime)
    {
        var target = FindNearestUnit(raider.Position, unit => unit.IsEnemyOf(raider.Team) && unit.IsAlive, raider.DetectionRange);
        if (target is not null)
        {
            var distance = Vector2.Distance(raider.Position, target.Position);
            if (distance <= raider.AttackRange + target.Radius)
            {
                if (raider.AttackCooldown <= 0f)
                {
                    raider.AttackCooldown = raider.AttackInterval;
                    target.Health = Math.Max(0f, target.Health - raider.AttackDamage);
                    _shotEffects.Add(new ShotEffect(raider.Position, target.Position, new Color(255, 122, 92), 0.12f));
                    _audio.PlayUnderAttack();
                }
            }
            else
            {
                MoveUnit(raider, target.Position, deltaTime, raider.AttackRange + target.Radius - 2f);
            }

            return;
        }

        var structureTarget = FindNearestStructure(raider.Position, structure => structure.IsAlive && structure.Team != raider.Team, raider.DetectionRange);
        if (structureTarget is not null)
        {
            var distance = Vector2.Distance(raider.Position, structureTarget.Position);
            if (distance <= raider.AttackRange + structureTarget.Radius)
            {
                if (raider.AttackCooldown <= 0f)
                {
                    raider.AttackCooldown = raider.AttackInterval;
                    structureTarget.Health = Math.Max(0f, structureTarget.Health - raider.AttackDamage);
                    _shotEffects.Add(new ShotEffect(raider.Position, structureTarget.Position, new Color(255, 122, 92), 0.12f));
                    _audio.PlayUnderAttack();
                }
            }
            else
            {
                MoveUnit(raider, structureTarget.Position, deltaTime, raider.AttackRange + structureTarget.Radius - 2f);
            }

            return;
        }

        var enemyHqPosition = raider.Team == 0 ? HeadquartersPositionP2 : HeadquartersPosition;
        var distanceToHq = Vector2.Distance(raider.Position, enemyHqPosition);
        if (distanceToHq <= HeadquartersHalfWidth + raider.AttackRange)
        {
            if (raider.AttackCooldown <= 0f)
            {
                raider.AttackCooldown = raider.AttackInterval;
                if (raider.Team == 0)
                    _hqHealthP2 = Math.Max(0f, _hqHealthP2 - raider.AttackDamage);
                else
                    _hqHealth = Math.Max(0f, _hqHealth - raider.AttackDamage);
                _shotEffects.Add(new ShotEffect(raider.Position, enemyHqPosition, new Color(255, 122, 92), 0.12f));
                _audio.PlayUnderAttack();
            }
        }
        else
        {
            MoveUnit(raider, enemyHqPosition, deltaTime, HeadquartersHalfWidth + raider.AttackRange - 4f);
        }
    }

    private void UpdateMoveOrder(RtsUnit unit, float deltaTime)
    {
        if (!unit.HasMoveTarget)
            return;

        if (!MoveUnit(unit, unit.MoveTarget, deltaTime, 6f))
            return;

        unit.HasMoveTarget = false;
        if (unit.OrderType == RtsUnitOrderType.Move)
            unit.OrderType = RtsUnitOrderType.Idle;
    }

    private void IssueOrders(IReadOnlyList<RtsUnit> selectedUnits, Vector2 worldPosition, int team)
    {
        var resourceNodeIndex = FindResourceNodeIndex(worldPosition);
        var buildTarget = FindBlueprintNear(worldPosition);
        var columns = (int)MathF.Ceiling(MathF.Sqrt(selectedUnits.Count));
        var issuedHarvestOrder = false;
        for (var index = 0; index < selectedUnits.Count; index++)
        {
            var unit = selectedUnits[index];
            if (!unit.IsAlive)
                continue;

            if (buildTarget is not null && unit.Role == RtsUnitRole.Worker)
            {
                unit.OrderType = RtsUnitOrderType.Build;
                unit.AssignedStructureId = buildTarget.Id;
                unit.AssignedNodeIndex = -1;
                unit.ReturningToBase = false;
                unit.HasMoveTarget = true;
                unit.MoveTarget = buildTarget.Position;
                continue;
            }

            if (resourceNodeIndex >= 0 && unit.Role == RtsUnitRole.Worker)
            {
                issuedHarvestOrder = true;
                unit.OrderType = RtsUnitOrderType.Harvest;
                unit.AssignedNodeIndex = resourceNodeIndex;
                unit.ReturningToBase = false;
                unit.HasMoveTarget = true;
                unit.MoveTarget = _resourceNodes[resourceNodeIndex].Position + ComputeFormationOffset(index, columns, 22f);
                continue;
            }

            unit.OrderType = RtsUnitOrderType.Move;
            unit.AssignedNodeIndex = -1;
            unit.AssignedStructureId = -1;
            unit.ReturningToBase = false;
            unit.HasMoveTarget = true;
            unit.MoveTarget = ClampPointToWorld(worldPosition + ComputeFormationOffset(index, columns, 24f));
        }

        SetCommandPulse(buildTarget?.Position ?? (resourceNodeIndex >= 0 ? _resourceNodes[resourceNodeIndex].Position : worldPosition));
        _audio.PlayOrder(issuedHarvestOrder || buildTarget is not null);
    }

    private bool MoveUnit(RtsUnit unit, Vector2 target, float deltaTime, float stopDistance)
    {
        var toTarget = target - unit.Position;
        var distance = toTarget.Length;
        if (distance <= stopDistance)
            return true;

        var direction = distance > 0.001f ? toTarget * (1f / distance) : unit.AimDirection;
        var step = Math.Min(unit.Speed * deltaTime, Math.Max(0f, distance - stopDistance));
        if (step > 0f)
            unit.Position = ClampUnitPosition(unit, unit.Position + (direction * step));

        unit.AimDirection = direction;
        return distance - stopDistance <= unit.Speed * deltaTime;
    }

    private void FireAtUnit(RtsUnit attacker, RtsUnit target, Color color)
    {
        attacker.AttackCooldown = attacker.AttackInterval;
        target.Health = Math.Max(0f, target.Health - attacker.AttackDamage);
        attacker.AimDirection = SafeNormalize(target.Position - attacker.Position, attacker.AimDirection);
        _shotEffects.Add(new ShotEffect(attacker.Position, target.Position, color, 0.12f));
        if (attacker.Role == RtsUnitRole.Guard)
            _audio.PlayGuardFire();
    }

    private void ResolveUnitSeparation()
    {
        for (var i = 0; i < _units.Count; i++)
        {
            var a = _units[i];
            if (!a.IsAlive)
                continue;

            for (var j = i + 1; j < _units.Count; j++)
            {
                var b = _units[j];
                if (!b.IsAlive)
                    continue;

                var delta = b.Position - a.Position;
                var distance = delta.Length;
                var minimumDistance = a.Radius + b.Radius + 2f;
                if (distance >= minimumDistance)
                    continue;

                var direction = distance > 0.001f
                    ? delta * (1f / distance)
                    : new Vector2(((a.Id + b.Id) & 1) == 0 ? 1f : -1f, 0f);
                var push = (minimumDistance - distance) * 0.5f;
                a.Position = ClampUnitPosition(a, a.Position - (direction * push));
                b.Position = ClampUnitPosition(b, b.Position + (direction * push));
            }
        }
    }

    private void CleanupDestroyedUnits()
    {
        var salvageP1 = 0;
        var salvageP2 = 0;
        for (var index = _units.Count - 1; index >= 0; index--)
        {
            var unit = _units[index];
            if (unit.IsAlive)
                continue;

            if (unit.Team == 1)
                salvageP1 += SalvagePerRaider;
            else
                salvageP2 += SalvagePerRaider;

            _units.RemoveAt(index);
        }

        if (salvageP1 > 0)
            _oreStockpile += salvageP1;
        if (salvageP2 > 0)
            _oreStockpileP2 += salvageP2;
    }

    private void CleanupDestroyedStructures()
    {
        for (var index = _structures.Count - 1; index >= 0; index--)
        {
            if (!_structures[index].IsAlive)
                _structures.RemoveAt(index);
        }
    }

    private void CheckScenarioState()
    {
        if (IsMultiplayerMatch)
        {
            if (!_gameOver && _hqHealth <= 0f)
            {
                _gameOver = true;
                _winnerTeam = 1;
                _bannerTitle = "Base Destroyed";
                _bannerSubtitle = "Player 2 wins. Press R or Enter to restart.";
                _audio.PlayDefeat();
                return;
            }

            if (!_gameOver && _hqHealthP2 <= 0f)
            {
                _gameOver = true;
                _winnerTeam = 0;
                _bannerTitle = "Enemy Base Destroyed";
                _bannerSubtitle = "Player 1 wins. Press R or Enter to restart.";
                _audio.PlayVictory();
                return;
            }

            return;
        }

        if (!_gameOver && _hqHealth <= 0f)
        {
            _gameOver = true;
            _bannerTitle = "Command Center Destroyed";
            _bannerSubtitle = "The GLA has overrun your base. Press R or Enter to restart the mission.";
            _audio.PlayDefeat();
            return;
        }

        if (!_victory && _hqHealthP2 <= 0f)
        {
            _victory = true;
            _bannerTitle = "Enemy Beacon Destroyed";
            _bannerSubtitle = "The GLA rally beacon is down. Press R or Enter to restart the mission.";
            _audio.PlayVictory();
            return;
        }

        if (!_victory && _oreStockpile >= VictoryOreGoalValue)
        {
            _victory = true;
            _bannerTitle = "Mission Accomplished";
            _bannerSubtitle = $"{_oreStockpile} secured in {_missionTime:0.0}s. The General will be pleased. Press R or Enter to play again.";
            _audio.PlayVictory();
        }
    }

    private void DrawBattlefield()
    {
        Graphics.DrawFilledRect(0, 0, Engine.Width, Engine.Height, new Color(24, 20, 14));
        FillWorldRect(new Rectangle(0f, 0f, WorldWidth, 340f), new Color(60, 45, 30));
        FillWorldRect(new Rectangle(0f, 340f, WorldWidth, 440f), new Color(50, 42, 28));
        FillWorldRect(new Rectangle(0f, 780f, WorldWidth, WorldHeight - 780f), new Color(42, 36, 24));

        var visibleLeft = (int)(_cameraPosition.X / 80f) * 80;
        var visibleTop = (int)(_cameraPosition.Y / 80f) * 80;
        var visibleRight = (int)(_cameraPosition.X + Engine.Width) + 80;
        var visibleBottom = (int)(_cameraPosition.Y + Engine.Height) + 80;

        for (var x = visibleLeft; x <= visibleRight; x += 80)
        {
            var screenX = (int)(x - _cameraPosition.X);
            Graphics.DrawLine(screenX, 0, screenX, Engine.Height, new Color(50, 42, 30, 100));
        }

        for (var y = visibleTop; y <= visibleBottom; y += 80)
        {
            var screenY = (int)(y - _cameraPosition.Y);
            Graphics.DrawLine(0, screenY, Engine.Width, screenY, new Color(50, 42, 30, 100));
        }

        if (!IsMultiplayerMatch)
            DrawBeaconMarker(EnemyBeaconPosition, 42, new Color(196, 160, 96), new Color(255, 220, 170));
        DrawBeaconMarker(_rallyPoint, 18, new Color(204, 51, 51), new Color(255, 120, 120));
        if (IsMultiplayerMatch)
            DrawBeaconMarker(_rallyPointP2, 18, new Color(80, 80, 204), new Color(120, 120, 255));
    }

    private void DrawStructures()
    {
        // Player 1 HQ
        var hqTopLeft = WorldToScreen(HeadquartersPosition - new Vector2(HeadquartersHalfWidth, HeadquartersHalfHeight));
        Graphics.DrawFilledRect((int)hqTopLeft.X, (int)hqTopLeft.Y, (int)(HeadquartersHalfWidth * 2f), (int)(HeadquartersHalfHeight * 2f), new Color(80, 60, 40));
        Graphics.DrawRect((int)hqTopLeft.X, (int)hqTopLeft.Y, (int)(HeadquartersHalfWidth * 2f), (int)(HeadquartersHalfHeight * 2f), new Color(200, 160, 100));
        Graphics.DrawFilledRect((int)(hqTopLeft.X + 16f), (int)(hqTopLeft.Y + 16f), 44, 24, new Color(139, 26, 26));
        Graphics.DrawFilledRect((int)(hqTopLeft.X + 70f), (int)(hqTopLeft.Y + 16f), 26, 50, new Color(120, 100, 70));
        DrawBar(HeadquartersPosition + new Vector2(0f, -66f), 112, 8, _hqHealth / HeadquartersMaxHealth, new Color(51, 204, 51));

        if (IsMultiplayerMatch)
        {
            // Player 2 HQ
            var hq2TopLeft = WorldToScreen(HeadquartersPositionP2 - new Vector2(HeadquartersHalfWidth, HeadquartersHalfHeight));
            Graphics.DrawFilledRect((int)hq2TopLeft.X, (int)hq2TopLeft.Y, (int)(HeadquartersHalfWidth * 2f), (int)(HeadquartersHalfHeight * 2f), new Color(60, 40, 40));
            Graphics.DrawRect((int)hq2TopLeft.X, (int)hq2TopLeft.Y, (int)(HeadquartersHalfWidth * 2f), (int)(HeadquartersHalfHeight * 2f), new Color(200, 100, 100));
            Graphics.DrawFilledRect((int)(hq2TopLeft.X + 16f), (int)(hq2TopLeft.Y + 16f), 44, 24, new Color(139, 26, 26));
            Graphics.DrawFilledRect((int)(hq2TopLeft.X + 70f), (int)(hq2TopLeft.Y + 16f), 26, 50, new Color(100, 70, 70));
            DrawBar(HeadquartersPositionP2 + new Vector2(0f, -66f), 112, 8, _hqHealthP2 / HeadquartersMaxHealth, new Color(255, 80, 80));
        }
        else
        {
            // Enemy beacon (single player only)
            var beaconTopLeft = WorldToScreen(EnemyBeaconPosition - new Vector2(EnemyBeaconHalfWidth, EnemyBeaconHalfHeight));
            var beaconWidth = (int)(EnemyBeaconHalfWidth * 2f);
            var beaconHeight = (int)(EnemyBeaconHalfHeight * 2f);
            Graphics.DrawFilledRect((int)beaconTopLeft.X, (int)beaconTopLeft.Y, beaconWidth, beaconHeight, new Color(80, 65, 40));
            Graphics.DrawRect((int)beaconTopLeft.X, (int)beaconTopLeft.Y, beaconWidth, beaconHeight, new Color(196, 160, 96));
            Graphics.DrawLine((int)(beaconTopLeft.X + 8f), (int)(beaconTopLeft.Y + 10f), (int)(beaconTopLeft.X + beaconWidth - 8f), (int)(beaconTopLeft.Y + beaconHeight - 10f), new Color(196, 160, 96));
            Graphics.DrawLine((int)(beaconTopLeft.X + beaconWidth - 8f), (int)(beaconTopLeft.Y + 10f), (int)(beaconTopLeft.X + 8f), (int)(beaconTopLeft.Y + beaconHeight - 10f), new Color(196, 160, 96));
            DrawBar(EnemyBeaconPosition + new Vector2(0f, -(EnemyBeaconHalfHeight + 20f)), beaconWidth, 8, _hqHealthP2 / HeadquartersMaxHealth, new Color(255, 80, 80));
        }

        DrawPlacementGhost();
        DrawPlayerStructures();
    }

    private void DrawResourceNodes()
    {
        foreach (var node in _resourceNodes)
        {
            var screen = WorldToScreen(node.Position);
            if (!IsOnScreen(screen, 48f))
                continue;

            var baseColor = node.IsDepleted ? new Color(70, 60, 45) : new Color(180, 140, 60);
            var glowColor = node.IsDepleted ? new Color(100, 85, 65) : new Color(255, 220, 120);
            DrawDiamond(screen, 26, baseColor, glowColor);
            DrawBar(node.Position + new Vector2(0f, 40f), 48, 5, node.RemainingOre / 5000f, new Color(51, 204, 51));
        }
    }

    private void DrawShotEffects()
    {
        foreach (var effect in _shotEffects)
        {
            var from = WorldToScreen(effect.From);
            var to = WorldToScreen(effect.To);
            Graphics.DrawLine((int)from.X, (int)from.Y, (int)to.X, (int)to.Y, effect.Color);
        }
    }

    private void DrawSelectionOverlays()
    {
        foreach (var unit in _units)
        {
            if (!unit.IsAlive || !unit.Selected)
                continue;

            if (!TryGetUnitOrderTarget(unit, out var target, out var color))
                continue;

            var from = WorldToScreen(unit.Position);
            var to = WorldToScreen(target);
            var lineColor = new Color(color.R, color.G, color.B, 180);
            Graphics.DrawLine((int)from.X, (int)from.Y, (int)to.X, (int)to.Y, lineColor);
            DrawTargetMarker(target, color);
        }
    }

    private void DrawUnits()
    {
        foreach (var unit in _units)
        {
            if (!unit.IsAlive)
                continue;

            var screen = WorldToScreen(unit.Position);
            if (!IsOnScreen(screen, 36f))
                continue;

            var size = (int)(unit.Radius * 2f);
            var x = (int)(screen.X - unit.Radius);
            var y = (int)(screen.Y - unit.Radius);
            Graphics.DrawFilledRect(x, y, size, size, unit.FillColor);
            Graphics.DrawRect(x, y, size, size, unit.AccentColor);

            var aimEnd = screen + (unit.AimDirection * (unit.Radius + 8f));
            Graphics.DrawLine((int)screen.X, (int)screen.Y, (int)aimEnd.X, (int)aimEnd.Y, unit.AccentColor);

            if (unit.Selected)
                Graphics.DrawCircle((int)screen.X, (int)screen.Y, (int)(unit.Radius + 7f), new Color(51, 204, 51));

            if (unit.Role == RtsUnitRole.Worker && unit.CarryOre > 0)
                Graphics.DrawFilledRect(x + size - 4, y - 4, 6, 6, new Color(51, 204, 51));

            if (unit.Health < unit.MaxHealth || unit.IsEnemyOf(LocalTeam))
                DrawBar(unit.Position + new Vector2(0f, -18f), 30, 4, unit.Health / unit.MaxHealth, unit.IsAllyOf(LocalTeam) ? new Color(51, 204, 51) : new Color(255, 80, 80));
        }
    }

    private void DrawSelectionRectangle()
    {
        if (!_selectionActive)
            return;

        var left = (int)Math.Min(_selectionStartScreen.X, _selectionEndScreen.X);
        var top = (int)Math.Min(_selectionStartScreen.Y, _selectionEndScreen.Y);
        var width = Math.Max(1, (int)Math.Abs(_selectionStartScreen.X - _selectionEndScreen.X));
        var height = Math.Max(1, (int)Math.Abs(_selectionStartScreen.Y - _selectionEndScreen.Y));
        Graphics.DrawFilledRect(left, top, width, height, new Color(113, 193, 255, 26));
        Graphics.DrawRect(left, top, width, height, new Color(51, 204, 51));
    }

    private void DrawTacticalSignals()
    {
        if (_commandPulseTimer > 0f)
            DrawPulse(_commandPulsePosition, _commandPulseTimer / CommandPulseDuration, new Color(51, 204, 51));

        if (_navigationPulseTimer > 0f)
            DrawPulse(_navigationPulsePosition, _navigationPulseTimer / NavigationPulseDuration, new Color(204, 51, 51));
    }

    private void DrawMinimap()
    {
        var bounds = GetMinimapBounds();
        var left = (int)bounds.X;
        var top = (int)bounds.Y;
        var width = (int)bounds.Width;
        var height = (int)bounds.Height;
        var horizontalThird = width / 3;
        var verticalThird = height / 3;

        Graphics.DrawFilledRect(left, top, width, height, new Color(40, 32, 20, 210));
        Graphics.DrawFilledRect(left, top, width, verticalThird, new Color(50, 38, 22, 220));
        Graphics.DrawFilledRect(left, top + verticalThird, width, verticalThird, new Color(42, 34, 20, 220));
        Graphics.DrawFilledRect(left, top + (verticalThird * 2), width, height - (verticalThird * 2), new Color(36, 28, 18, 220));
        Graphics.DrawRect(left, top, width, height, new Color(139, 115, 69));
        Graphics.DrawLine(left + horizontalThird, top, left + horizontalThird, top + height, new Color(100, 80, 50, 120));
        Graphics.DrawLine(left + (horizontalThird * 2), top, left + (horizontalThird * 2), top + height, new Color(100, 80, 50, 120));
        Graphics.DrawLine(left, top + verticalThird, left + width, top + verticalThird, new Color(100, 80, 50, 120));
        Graphics.DrawLine(left, top + (verticalThird * 2), left + width, top + (verticalThird * 2), new Color(100, 80, 50, 120));

        foreach (var node in _resourceNodes)
        {
            var position = WorldToMinimap(node.Position, bounds);
            var color = node.IsDepleted ? new Color(82, 70, 50) : new Color(204, 170, 50);
            Graphics.DrawFilledRect((int)position.X - 2, (int)position.Y - 2, 4, 4, color);
        }

        var hq = WorldToMinimap(HeadquartersPosition, bounds);
        Graphics.DrawFilledRect((int)hq.X - 3, (int)hq.Y - 3, 6, 6, new Color(204, 51, 51));
        if (IsMultiplayerMatch)
        {
            var hq2 = WorldToMinimap(HeadquartersPositionP2, bounds);
            Graphics.DrawFilledRect((int)hq2.X - 3, (int)hq2.Y - 3, 6, 6, new Color(255, 80, 80));
        }
        else
        {
            var beacon = WorldToMinimap(EnemyBeaconPosition, bounds);
            Graphics.DrawFilledRect((int)beacon.X - 3, (int)beacon.Y - 3, 6, 6, new Color(196, 160, 96));
        }
        var rally = WorldToMinimap(_rallyPoint, bounds);
        DrawMinimapMarker(rally, new Color(51, 204, 51), 4);

        foreach (var structure in _structures)
        {
            if (!structure.IsAlive)
                continue;

            var point = WorldToMinimap(structure.Position, bounds);
            var color = structure.Type switch
            {
                RtsStructureType.Building => new Color(139, 26, 26),
                RtsStructureType.WarFactory => new Color(90, 90, 105),
                RtsStructureType.Reactor => new Color(180, 160, 50),
                _ => new Color(100, 60, 60)
            };
            var size = structure.Type == RtsStructureType.WarFactory ? 5 : 4;
            Graphics.DrawFilledRect((int)point.X - (size / 2), (int)point.Y - (size / 2), size, size, color);
        }

        foreach (var unit in _units)
        {
            if (!unit.IsAlive)
                continue;

            var point = WorldToMinimap(unit.Position, bounds);
            var color = unit.IsEnemyOf(LocalTeam) ? new Color(255, 80, 80) : new Color(51, 204, 51);
            Graphics.DrawFilledRect((int)point.X - 1, (int)point.Y - 1, 3, 3, color);
            if (unit.Selected)
                Graphics.DrawRect((int)point.X - 3, (int)point.Y - 3, 6, 6, new Color(51, 204, 51));
        }

        if (_commandPulseTimer > 0f)
            DrawMinimapPulse(_commandPulsePosition, _commandPulseTimer / CommandPulseDuration, bounds, new Color(51, 204, 51));

        if (_navigationPulseTimer > 0f)
            DrawMinimapPulse(_navigationPulsePosition, _navigationPulseTimer / NavigationPulseDuration, bounds, new Color(204, 51, 51));

        if (IsPointInsideMinimap(MousePosition))
        {
            var cursorPoint = WorldToMinimap(MinimapToWorld(MousePosition), bounds);
            Graphics.DrawRect((int)cursorPoint.X - 3, (int)cursorPoint.Y - 3, 6, 6, new Color(51, 204, 51));
        }

        var cameraLeft = left + (int)((_cameraPosition.X / WorldWidth) * width);
        var cameraTop = top + (int)((_cameraPosition.Y / WorldHeight) * height);
        var cameraWidth = Math.Max(12, (int)((Engine.Width / WorldWidth) * width));
        var cameraHeight = Math.Max(12, (int)((Engine.Height / WorldHeight) * height));
        Graphics.DrawRect(cameraLeft, cameraTop, cameraWidth, cameraHeight, new Color(51, 204, 51));
    }

    private void FillWorldRect(Rectangle worldRect, Color color)
    {
        var visibleWorld = new Rectangle(_cameraPosition.X, _cameraPosition.Y, Engine.Width, Engine.Height);
        if (!worldRect.Intersects(visibleWorld))
            return;

        var left = Math.Max(worldRect.X, visibleWorld.X);
        var top = Math.Max(worldRect.Y, visibleWorld.Y);
        var right = Math.Min(worldRect.Right, visibleWorld.Right);
        var bottom = Math.Min(worldRect.Bottom, visibleWorld.Bottom);
        Graphics.DrawFilledRect(
            (int)(left - _cameraPosition.X),
            (int)(top - _cameraPosition.Y),
            Math.Max(1, (int)(right - left)),
            Math.Max(1, (int)(bottom - top)),
            color);
    }

    private void DrawDiamond(Vector2 center, int radius, Color fillColor, Color outlineColor)
    {
        Graphics.DrawFilledRect((int)center.X - (radius / 2), (int)center.Y - (radius / 2), radius, radius, fillColor);
        Graphics.DrawLine((int)center.X, (int)(center.Y - radius), (int)(center.X + radius), (int)center.Y, outlineColor);
        Graphics.DrawLine((int)(center.X + radius), (int)center.Y, (int)center.X, (int)(center.Y + radius), outlineColor);
        Graphics.DrawLine((int)center.X, (int)(center.Y + radius), (int)(center.X - radius), (int)center.Y, outlineColor);
        Graphics.DrawLine((int)(center.X - radius), (int)center.Y, (int)center.X, (int)(center.Y - radius), outlineColor);
    }

    private void DrawBeaconMarker(Vector2 worldPosition, int radius, Color fillColor, Color outlineColor)
    {
        var screen = WorldToScreen(worldPosition);
        if (!IsOnScreen(screen, radius + 12f))
            return;

        Graphics.DrawCircle((int)screen.X, (int)screen.Y, radius, outlineColor);
        Graphics.DrawFilledRect((int)screen.X - 5, (int)screen.Y - 5, 10, 10, fillColor);
    }

    private void DrawTargetMarker(Vector2 worldPosition, Color color)
    {
        var screen = WorldToScreen(worldPosition);
        if (!IsOnScreen(screen, 16f))
            return;

        var markerColor = new Color(color.R, color.G, color.B, 220);
        Graphics.DrawRect((int)screen.X - 7, (int)screen.Y - 7, 14, 14, markerColor);
        Graphics.DrawLine((int)screen.X - 10, (int)screen.Y, (int)screen.X + 10, (int)screen.Y, markerColor);
        Graphics.DrawLine((int)screen.X, (int)screen.Y - 10, (int)screen.X, (int)screen.Y + 10, markerColor);
    }

    private void DrawPulse(Vector2 worldPosition, float normalizedTime, Color color)
    {
        var screen = WorldToScreen(worldPosition);
        if (!IsOnScreen(screen, 42f))
            return;

        var clamped = Math.Clamp(normalizedTime, 0f, 1f);
        var radius = 12 + (int)((1f - clamped) * 24f);
        var pulseColor = new Color(color.R, color.G, color.B, (byte)Math.Clamp((int)(72 + (clamped * 168f)), 0, 255));
        Graphics.DrawCircle((int)screen.X, (int)screen.Y, radius, pulseColor);
        Graphics.DrawLine((int)screen.X - 8, (int)screen.Y, (int)screen.X + 8, (int)screen.Y, pulseColor);
        Graphics.DrawLine((int)screen.X, (int)screen.Y - 8, (int)screen.X, (int)screen.Y + 8, pulseColor);
    }

    private void DrawMinimapMarker(Vector2 position, Color color, int radius)
    {
        Graphics.DrawRect((int)position.X - radius, (int)position.Y - radius, radius * 2, radius * 2, color);
        Graphics.DrawLine((int)position.X - radius - 2, (int)position.Y, (int)position.X + radius + 2, (int)position.Y, color);
        Graphics.DrawLine((int)position.X, (int)position.Y - radius - 2, (int)position.X, (int)position.Y + radius + 2, color);
    }

    private void DrawMinimapPulse(Vector2 worldPosition, float normalizedTime, Rectangle bounds, Color color)
    {
        var point = WorldToMinimap(worldPosition, bounds);
        var clamped = Math.Clamp(normalizedTime, 0f, 1f);
        var radius = 4 + (int)((1f - clamped) * 6f);
        var pulseColor = new Color(color.R, color.G, color.B, (byte)Math.Clamp((int)(80 + (clamped * 160f)), 0, 255));
        Graphics.DrawRect((int)point.X - radius, (int)point.Y - radius, radius * 2, radius * 2, pulseColor);
    }

    private void DrawBar(Vector2 worldPosition, int width, int height, float normalizedValue, Color fillColor)
    {
        var clampedValue = Math.Clamp(normalizedValue, 0f, 1f);
        var screen = WorldToScreen(worldPosition);
        var left = (int)(screen.X - (width / 2f));
        var top = (int)screen.Y;
        Graphics.DrawFilledRect(left, top, width, height, new Color(8, 12, 16, 220));
        Graphics.DrawRect(left, top, width, height, new Color(201, 222, 231));
        Graphics.DrawFilledRect(left + 1, top + 1, Math.Max(0, (int)((width - 2) * clampedValue)), Math.Max(1, height - 2), fillColor);
    }

    private RtsUnit SpawnPlayerUnit(int team, RtsUnitRole role, Vector2 position, bool sendToRally)
    {
        var unit = new RtsUnit(role, position) { Team = team };
        if (sendToRally)
        {
            unit.OrderType = RtsUnitOrderType.Move;
            unit.HasMoveTarget = true;
            unit.MoveTarget = team == 0 ? _rallyPoint : _rallyPointP2;
        }

        _units.Add(unit);
        return unit;
    }

    private void SelectPlayerUnits(Func<RtsUnit, bool> predicate)
    {
        ClearSelection();
        foreach (var unit in _units)
        {
            if (unit.IsAlive && unit.IsAllyOf(LocalTeam) && predicate(unit))
                unit.Selected = true;
        }
    }

    private void ClearSelection()
    {
        foreach (var unit in _units)
            unit.Selected = false;
    }

    private List<RtsUnit> GetSelectedUnits()
    {
        return _units.Where(unit => unit.IsAlive && unit.IsAllyOf(LocalTeam) && unit.Selected).ToList();
    }

    private RtsUnit? FindPlayerUnit(Vector2 worldPosition, float maxDistance)
    {
        var bestDistance = maxDistance;
        RtsUnit? result = null;

        foreach (var unit in _units)
        {
            if (!unit.IsAlive || !unit.IsAllyOf(LocalTeam))
                continue;

            var distance = Vector2.Distance(unit.Position, worldPosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                result = unit;
            }
        }

        return result;
    }

    private RtsUnit? FindHoveredUnit(Vector2 worldPosition, float maxDistance)
    {
        var bestDistance = maxDistance;
        RtsUnit? result = null;

        foreach (var unit in _units)
        {
            if (!unit.IsAlive)
                continue;

            var hoverDistance = Math.Max(maxDistance, unit.Radius + 6f);
            var distance = Vector2.Distance(unit.Position, worldPosition);
            if (distance > hoverDistance || distance > bestDistance)
                continue;

            bestDistance = distance;
            result = unit;
        }

        return result;
    }

    private RtsUnit? FindNearestUnit(Vector2 origin, Func<RtsUnit, bool> predicate, float range)
    {
        var bestDistanceSquared = range * range;
        RtsUnit? result = null;

        foreach (var unit in _units)
        {
            if (!predicate(unit))
                continue;

            var delta = unit.Position - origin;
            var distanceSquared = delta.LengthSquared;
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                result = unit;
            }
        }

        return result;
    }

    private RtsStructure? FindNearestStructure(Vector2 origin, Func<RtsStructure, bool> predicate, float range)
    {
        var bestDistanceSquared = range * range;
        RtsStructure? result = null;

        foreach (var structure in _structures)
        {
            if (!predicate(structure))
                continue;

            var delta = structure.Position - origin;
            var distanceSquared = delta.LengthSquared;
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                result = structure;
            }
        }

        return result;
    }

    private int FindResourceNodeIndex(Vector2 worldPosition)
    {
        var bestDistance = 52f;
        var result = -1;
        for (var index = 0; index < _resourceNodes.Count; index++)
        {
            var node = _resourceNodes[index];
            if (node.IsDepleted)
                continue;

            var distance = Vector2.Distance(worldPosition, node.Position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                result = index;
            }
        }

        return result;
    }

    private bool TryGetResourceNode(int index, out RtsResourceNode node)
    {
        if (index >= 0 && index < _resourceNodes.Count)
        {
            node = _resourceNodes[index];
            return true;
        }

        node = null!;
        return false;
    }

    private bool TryGetUnitOrderTarget(RtsUnit unit, out Vector2 target, out Color color)
    {
        if (unit.Role == RtsUnitRole.Worker && unit.OrderType == RtsUnitOrderType.Harvest && TryGetResourceNode(unit.AssignedNodeIndex, out var node))
        {
            var unitHq = unit.Team == 0 ? HeadquartersPosition : HeadquartersPositionP2;
            target = unit.ReturningToBase ? unitHq : node.Position;
            color = unit.ReturningToBase ? new Color(255, 214, 96) : unit.AccentColor;
            return true;
        }

        if (unit.HasMoveTarget)
        {
            target = unit.MoveTarget;
            color = unit.AccentColor;
            return true;
        }

        target = Vector2.Zero;
        color = Color.Transparent;
        return false;
    }

    private void SetCommandPulse(Vector2 worldPosition)
    {
        _commandPulsePosition = ClampPointToWorld(worldPosition);
        _commandPulseTimer = CommandPulseDuration;
    }

    private void FocusCameraOnSelection()
    {
        var selectedUnits = GetSelectedUnits();
        var localHq = LocalTeam == 0 ? HeadquartersPosition : HeadquartersPositionP2;
        var focusPoint = selectedUnits.Count == 0 ? localHq : GetSelectionCenter(selectedUnits);
        var title = selectedUnits.Count == 0 ? "HQ Focus" : "Squad Focus";
        var subtitle = selectedUnits.Count == 0
            ? $"Camera centered on the foundry in the {DescribeSector(focusPoint)}."
            : $"Camera centered on {selectedUnits.Count} selected units in the {DescribeSector(focusPoint)}.";

        CenterCameraOnWorld(focusPoint, announce: true, markPulse: true, title: title, subtitle: subtitle);
    }

    private void CenterCameraOnWorld(Vector2 worldPosition, bool announce, bool markPulse, string? title = null, string? subtitle = null)
    {
        var clamped = ClampPointToWorld(worldPosition);
        _cameraPosition = ClampCamera(clamped - new Vector2(Engine.Width * 0.5f, Engine.Height * 0.5f));

        if (markPulse)
        {
            _navigationPulsePosition = clamped;
            _navigationPulseTimer = NavigationPulseDuration;
        }

        if (announce)
            ShowTransientMessage(title ?? "Camera Jump", subtitle ?? $"Camera centered on the {DescribeSector(clamped)}.", 0.95f);
    }

    private Vector2 ClampCamera(Vector2 position)
    {
        var maxX = Math.Max(0f, WorldWidth - Engine.Width);
        var maxY = Math.Max(0f, WorldHeight - Engine.Height);
        return new Vector2(Math.Clamp(position.X, 0f, maxX), Math.Clamp(position.Y, 0f, maxY));
    }

    private Vector2 ClampPointToWorld(Vector2 point)
    {
        return new Vector2(Math.Clamp(point.X, 0f, WorldWidth), Math.Clamp(point.Y, 0f, WorldHeight));
    }

    private Vector2 ClampUnitPosition(RtsUnit unit, Vector2 position)
    {
        return new Vector2(
            Math.Clamp(position.X, unit.Radius, WorldWidth - unit.Radius),
            Math.Clamp(position.Y, unit.Radius, WorldHeight - unit.Radius));
    }

    private Rectangle GetMinimapBounds()
    {
        return new Rectangle(
            MinimapMargin,
            Engine.Height - MinimapHeight - MinimapMargin,
            MinimapWidth,
            MinimapHeight);
    }

    private bool IsPointerInsideBlockingHud(Vector2 screenPosition)
    {
        for (var index = 0; index < BlockingHudElementIds.Length; index++)
        {
            if (IsPointInsideUiElement(screenPosition, BlockingHudElementIds[index]))
                return true;
        }

        return false;
    }

    private bool IsPointInsideUiElement(Vector2 screenPosition, string elementId)
    {
        return TryGetUiElementBounds(elementId, out var bounds) && bounds.Contains(screenPosition);
    }

    private bool TryGetUiElementBounds(string elementId, out Rectangle bounds)
    {
        if (Engine.UI is not null && Engine.UI.TryGetBounds(elementId, Engine.Width, Engine.Height, out bounds))
            return true;

        bounds = default;
        return false;
    }

    private bool IsPointInsideMinimap(Vector2 screenPosition) => GetMinimapBounds().Contains(screenPosition);

    private Vector2 WorldToScreen(Vector2 worldPosition) => worldPosition - _cameraPosition;

    private Vector2 ScreenToWorld(Vector2 screenPosition) => ClampPointToWorld(screenPosition + _cameraPosition);

    private Vector2 WorldToMinimap(Vector2 worldPosition, Rectangle bounds)
    {
        return new Vector2(
            bounds.X + ((worldPosition.X / WorldWidth) * bounds.Width),
            bounds.Y + ((worldPosition.Y / WorldHeight) * bounds.Height));
    }

    private Vector2 MinimapToWorld(Vector2 minimapPosition)
    {
        var bounds = GetMinimapBounds();
        var normalizedX = Math.Clamp((minimapPosition.X - bounds.X) / bounds.Width, 0f, 1f);
        var normalizedY = Math.Clamp((minimapPosition.Y - bounds.Y) / bounds.Height, 0f, 1f);
        return new Vector2(normalizedX * WorldWidth, normalizedY * WorldHeight);
    }

    private Vector2 GetCameraCenterWorld()
    {
        return ClampPointToWorld(_cameraPosition + new Vector2(Engine.Width * 0.5f, Engine.Height * 0.5f));
    }

    private Vector2 GetCursorWorldPosition()
    {
        return IsPointInsideMinimap(MousePosition) ? MinimapToWorld(MousePosition) : ScreenToWorld(MousePosition);
    }

    private RtsUnit? GetHoveredUnit()
    {
        if (!_matchRunning
            || _selectionActive
            || _activePlacementType is not null
            || IsPointInsideMinimap(MousePosition)
            || IsPointerInsideBlockingHud(MousePosition))
        {
            return null;
        }

        return FindHoveredUnit(ScreenToWorld(MousePosition), 20f);
    }

    private Vector2 GetSelectionCenter(IReadOnlyList<RtsUnit> units)
    {
        if (units.Count == 0)
            return HeadquartersPosition;

        var sum = Vector2.Zero;
        for (var index = 0; index < units.Count; index++)
            sum += units[index].Position;

        return sum * (1f / units.Count);
    }

    private string GetProductionButtonText(RtsProductionType productionType, string hotkey)
    {
        if (_activePlacementType == productionType)
            return $"{hotkey} | {GetProductionCost(productionType)} ORE | PICK PAD";

        var status = GetProductionAvailability(productionType) switch
        {
            ProductionAvailability.Ready => "READY",
            ProductionAvailability.InsufficientOre => "LOW ORE",
            ProductionAvailability.SiteFull => "PADS FULL",
            _ => "QUEUE FULL"
        };

        return $"{hotkey} | {GetProductionCost(productionType)} ORE | {status}";
    }

    private string GetSlotCostText(RtsProductionType productionType, string hotkey)
    {
        if (_activePlacementType == productionType)
            return $"{hotkey} - PLACE";

        var status = GetProductionAvailability(productionType) switch
        {
            ProductionAvailability.Ready => $"{GetProductionCost(productionType)}",
            ProductionAvailability.InsufficientOre => "LOW",
            ProductionAvailability.SiteFull => "FULL",
            _ => "BUSY"
        };

        return $"{hotkey} - {status}";
    }

    private bool TryDeployStructure(RtsProductionType productionType, Vector2? reservedSite, out RtsStructure structure)
    {
        if (!TryMapToStructureType(productionType, out var structureType)
            || reservedSite is not { } position
            || !IsKnownStructurePad(structureType, position)
            || IsStructurePadOccupied(structureType, position)
            || IsStructurePadQueued(structureType, position))
        {
            structure = null!;
            return false;
        }

        structure = new RtsStructure(structureType, position);
        _structures.Add(structure);
        return true;
    }

    private void DrawPlacementGhost()
    {
        if (_activePlacementType is not { } productionType)
            return;

        if (!TryMapToStructureType(productionType, out var structureType))
            return;

        if (IsPointInsideMinimap(MousePosition) || IsPointerInsideBlockingHud(MousePosition))
            return;

        var worldPosition = ScreenToWorld(MousePosition);
        worldPosition = ClampPointToWorld(worldPosition);
        var screen = WorldToScreen(worldPosition);

        var (halfW, halfH) = structureType switch
        {
            RtsStructureType.Building => (32, 24),
            RtsStructureType.WarFactory => (40, 30),
            RtsStructureType.Reactor => (24, 24),
            _ => (18, 18)
        };

        Graphics.DrawFilledRect((int)screen.X - halfW, (int)screen.Y - halfH, halfW * 2, halfH * 2, new Color(51, 204, 51, 40));
        Graphics.DrawRect((int)screen.X - halfW, (int)screen.Y - halfH, halfW * 2, halfH * 2, new Color(51, 204, 51, 180));
    }

    private void DrawPlayerStructures()
    {
        for (var index = 0; index < _structures.Count; index++)
        {
            var structure = _structures[index];
            if (!structure.IsAlive)
                continue;

            var screen = WorldToScreen(structure.Position);
            var margin = Math.Max(structure.HalfSize.X, structure.HalfSize.Y) + 18f;
            if (!IsOnScreen(screen, margin))
                continue;

            var topLeft = WorldToScreen(structure.Position - structure.HalfSize);
            var width = (int)(structure.HalfSize.X * 2f);
            var height = (int)(structure.HalfSize.Y * 2f);
            var left = (int)topLeft.X;
            var top = (int)topLeft.Y;

            if (structure.UnderConstruction)
            {
                // Blueprint ghost: dashed outline and translucent fill
                Graphics.DrawFilledRect(left, top, width, height, new Color(51, 204, 51, 30));
                Graphics.DrawRect(left, top, width, height, new Color(51, 204, 51, 140));
                // Dashed cross pattern to indicate blueprint
                Graphics.DrawLine(left, top, left + width, top + height, new Color(51, 204, 51, 60));
                Graphics.DrawLine(left + width, top, left, top + height, new Color(51, 204, 51, 60));
                // Construction progress bar
                var progress = structure.ConstructionTime > 0f ? structure.ConstructionProgress / structure.ConstructionTime : 0f;
                DrawBar(structure.Position + new Vector2(0f, -structure.HalfSize.Y - 16f), width + 12, 5, progress, new Color(255, 200, 50));
                continue;
            }

            Graphics.DrawFilledRect(left, top, width, height, structure.FillColor);
            Graphics.DrawRect(left, top, width, height, structure.AccentColor);

            if (structure.Type == RtsStructureType.Building)
            {
                Graphics.DrawFilledRect(left + 8, top + 10, width - 16, 12, new Color(100, 40, 40));
                Graphics.DrawFilledRect(left + 10, top + 26, 12, 10, new Color(204, 51, 51));
                Graphics.DrawFilledRect(left + width - 22, top + 26, 12, 10, new Color(204, 51, 51));
            }
            else if (structure.Type == RtsStructureType.WarFactory)
            {
                Graphics.DrawFilledRect(left + 6, top + 6, width - 12, height - 12, new Color(70, 70, 85));
                Graphics.DrawFilledRect(left + 10, top + height - 14, width - 20, 8, new Color(120, 120, 140));
                Graphics.DrawLine(left + width / 2, top + 4, left + width / 2, top + height - 4, structure.AccentColor);
            }
            else if (structure.Type == RtsStructureType.Reactor)
            {
                Graphics.DrawFilledRect(left + 6, top + 6, width - 12, height - 12, new Color(160, 140, 40));
                Graphics.DrawCircle((int)screen.X, (int)screen.Y, 10, new Color(204, 180, 50));
                Graphics.DrawRect(left + 4, top + 4, width - 8, height - 8, new Color(204, 180, 50));
            }
            else
            {
                Graphics.DrawFilledRect(left + 4, top + 4, width - 8, height - 8, new Color(80, 50, 50));
                Graphics.DrawLine((int)screen.X, top - 8, (int)screen.X, top + 14, structure.AccentColor);
                Graphics.DrawLine((int)screen.X, top + 6, left + width + 10, top - 2, structure.AccentColor);
            }

            if (structure.Health < structure.MaxHealth)
                DrawBar(structure.Position + new Vector2(0f, -structure.HalfSize.Y - 16f), width + 12, 5, structure.Health / structure.MaxHealth, new Color(51, 204, 51));
        }
    }

    private bool IsStructurePadOccupied(RtsStructureType structureType, Vector2 pad)
    {
        for (var index = 0; index < _structures.Count; index++)
        {
            var structure = _structures[index];
            if (structure.IsAlive
                && structure.Type == structureType
                && Vector2.Distance(structure.Position, pad) <= 1f)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsStructurePadQueued(RtsStructureType structureType, Vector2 pad)
    {
        for (var index = 0; index < _productionQueue.Count; index++)
        {
            var order = _productionQueue[index];
            if (order.ReservedSite is not { } reservedSite || !TryMapToStructureType(order.Type, out var reservedType))
                continue;

            if (reservedType == structureType && Vector2.Distance(reservedSite, pad) <= 1f)
                return true;
        }

        return false;
    }

    private bool IsStructurePadAvailable(RtsProductionType productionType, Vector2 pad)
    {
        return TryMapToStructureType(productionType, out var structureType)
            && IsKnownStructurePad(structureType, pad)
            && !IsStructurePadOccupied(structureType, pad)
            && !IsStructurePadQueued(structureType, pad);
    }

    private bool TryGetSelectableStructurePad(RtsProductionType productionType, Vector2 screenPosition, out Vector2 pad)
    {
        if (!TryMapToStructureType(productionType, out var structureType))
        {
            pad = Vector2.Zero;
            return false;
        }

        var worldPosition = ScreenToWorld(screenPosition);
        var pads = GetStructurePadPositions(structureType);
        var bestDistance = float.MaxValue;
        pad = Vector2.Zero;
        for (var index = 0; index < pads.Length; index++)
        {
            var candidate = pads[index];
            if (!IsStructurePadAvailable(productionType, candidate) || !IsPointInsideStructurePad(structureType, worldPosition, candidate))
                continue;

            var distance = Vector2.Distance(worldPosition, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                pad = candidate;
            }
        }

        return bestDistance < float.MaxValue;
    }

    private static bool IsPointInsideStructurePad(RtsStructureType structureType, Vector2 worldPosition, Vector2 pad)
    {
        var (halfW, halfH) = structureType switch
        {
            RtsStructureType.Building => (34f, 26f),
            RtsStructureType.WarFactory => (42f, 32f),
            RtsStructureType.Reactor => (26f, 26f),
            _ => (20f, 20f)
        };

        var bounds = new Rectangle(pad.X - halfW, pad.Y - halfH, halfW * 2f, halfH * 2f);
        return bounds.Contains(worldPosition);
    }

    private static bool IsKnownStructurePad(RtsStructureType structureType, Vector2 pad)
    {
        var pads = GetStructurePadPositions(structureType);
        for (var index = 0; index < pads.Length; index++)
        {
            if (Vector2.Distance(pads[index], pad) <= 1f)
                return true;
        }

        return false;
    }

    private string GetSelectionRosterLine(int index)
    {
        var selectedUnits = GetSelectedUnits();
        if (selectedUnits.Count == 0)
            return index == 0 ? "Roster empty | Use LMB minimap, MMB snap, or Space HQ focus." : string.Empty;

        if (selectedUnits.Count > 3 && index == 2)
            return $"+ {selectedUnits.Count - 2} more units | Formation {DescribeSector(GetSelectionCenter(selectedUnits))}";

        if (index >= Math.Min(3, selectedUnits.Count))
            return string.Empty;

        return DescribeRosterLine(selectedUnits[index]);
    }

    private string DescribeRosterLine(RtsUnit unit)
    {
        var health = $"{(int)MathF.Ceiling(unit.Health)}/{(int)MathF.Ceiling(unit.MaxHealth)}";
        if (unit.Role == RtsUnitRole.Worker)
            return $"{unit.Callsign} {health} | Ore {unit.CarryOre}/{unit.CarryCapacity} | {DescribeWorkerDirective(unit)}";

        return $"{unit.Callsign} {health} | {DescribeCombatDirective(unit)}";
    }

    private string DescribeWorkerDirective(RtsUnit unit)
    {
        if (unit.OrderType == RtsUnitOrderType.Build)
            return "Constructing";

        if (unit.OrderType == RtsUnitOrderType.Harvest && TryGetResourceNode(unit.AssignedNodeIndex, out var node))
            return unit.ReturningToBase ? "Returning HQ" : $"Mining {node.Name}";

        if (unit.HasMoveTarget)
            return $"Moving {DescribeSector(unit.MoveTarget)}";

        return "Standing by";
    }

    private string DescribeCombatDirective(RtsUnit unit)
    {
        if (unit.HasMoveTarget)
            return $"Moving {DescribeSector(unit.MoveTarget)}";

        return unit.Role == RtsUnitRole.Guard ? "Auto-covering sector" : "Advancing";
    }

    private bool IsOnScreen(Vector2 screenPosition, float margin)
    {
        return screenPosition.X >= -margin
            && screenPosition.Y >= -margin
            && screenPosition.X <= Engine.Width + margin
            && screenPosition.Y <= Engine.Height + margin;
    }

    private static Rectangle CreateRectangle(Vector2 a, Vector2 b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new Rectangle(left, top, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private static Vector2 ComputeFormationOffset(int index, int columns, float spacing)
    {
        var row = index / columns;
        var column = index % columns;
        var width = (columns - 1) * spacing;
        return new Vector2((column * spacing) - (width * 0.5f), row * spacing);
    }

    private static Vector2 SafeNormalize(Vector2 vector, Vector2 fallback)
    {
        var length = vector.Length;
        return length > 0.001f ? vector * (1f / length) : fallback;
    }

    private static string GetRoleLabel(RtsUnitRole role)
    {
        return role switch
        {
            RtsUnitRole.Worker => "Supply Truck",
            RtsUnitRole.Guard => "Red Guard",
            RtsUnitRole.TankHunter => "Tank Hunter",
            RtsUnitRole.Battlemaster => "Battlemaster",
            _ => "GLA Fighter"
        };
    }

    private static string DescribeSector(Vector2 position)
    {
        string horizontal = position.X < WorldWidth * 0.33f ? "west lane" : position.X < WorldWidth * 0.66f ? "central lane" : "east lane";
        string vertical = position.Y < WorldHeight * 0.33f ? "upper" : position.Y < WorldHeight * 0.66f ? "midfield" : "lower";
        return vertical == "midfield" ? $"{vertical} {horizontal}" : $"{vertical} {horizontal}";
    }

    private int GetReservedSiteCount(RtsProductionType productionType)
    {
        if (!TryMapToStructureType(productionType, out var structureType))
            return 0;

        var liveStructures = _structures.Count(structure => structure.IsAlive && structure.Type == structureType);
        var queuedStructures = _productionQueue.Count(order => order.Type == productionType);
        return liveStructures + queuedStructures;
    }

    private int GetOpenSiteCount(RtsProductionType productionType)
    {
        if (!TryMapToStructureType(productionType, out var structureType))
            return 0;

        var openSites = 0;
        var pads = GetStructurePadPositions(structureType);
        for (var index = 0; index < pads.Length; index++)
        {
            if (!IsStructurePadOccupied(structureType, pads[index]) && !IsStructurePadQueued(structureType, pads[index]))
                openSites++;
        }

        return openSites;
    }

    private static bool UsesStructurePad(RtsProductionType productionType)
    {
        return productionType is RtsProductionType.Building
            or RtsProductionType.DefenseTower
            or RtsProductionType.WarFactory
            or RtsProductionType.Reactor;
    }

    private static bool IsStructureProduction(RtsProductionType productionType)
    {
        return productionType is RtsProductionType.Building
            or RtsProductionType.DefenseTower
            or RtsProductionType.WarFactory
            or RtsProductionType.Reactor;
    }

    private RtsStructure? FindStructureById(int id)
    {
        for (var index = 0; index < _structures.Count; index++)
        {
            if (_structures[index].Id == id && _structures[index].IsAlive)
                return _structures[index];
        }

        return null;
    }

    private RtsStructure? FindBlueprintNear(Vector2 worldPosition)
    {
        RtsStructure? best = null;
        var bestDistance = 60f;
        for (var index = 0; index < _structures.Count; index++)
        {
            var structure = _structures[index];
            if (!structure.IsAlive || !structure.UnderConstruction)
                continue;

            var distance = Vector2.Distance(structure.Position, worldPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = structure;
            }
        }

        return best;
    }

    private static int GetProductionCost(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => WorkerCost,
            RtsProductionType.Guard => GuardCost,
            RtsProductionType.TankHunter => TankHunterCost,
            RtsProductionType.Battlemaster => BattlemasterCost,
            RtsProductionType.Building => BuildingCost,
            RtsProductionType.DefenseTower => DefenseTowerCost,
            RtsProductionType.WarFactory => WarFactoryCost,
            _ => ReactorCost
        };
    }

    private static int GetProductionSiteCapacity(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Building => BuildingSitePositions.Length,
            RtsProductionType.DefenseTower => DefenseTowerSitePositions.Length,
            RtsProductionType.WarFactory => WarFactorySitePositions.Length,
            RtsProductionType.Reactor => ReactorSitePositions.Length,
            _ => 0
        };
    }

    private static float GetProductionBuildTime(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => 3.5f,
            RtsProductionType.Guard => 2.0f,
            RtsProductionType.TankHunter => 2.5f,
            RtsProductionType.Battlemaster => 5.0f,
            RtsProductionType.Building => 6.0f,
            RtsProductionType.DefenseTower => 5.0f,
            RtsProductionType.WarFactory => 8.0f,
            _ => 5.0f
        };
    }

    private static string GetProductionLabel(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => "Supply Truck",
            RtsProductionType.Guard => "Red Guard",
            RtsProductionType.TankHunter => "Tank Hunter",
            RtsProductionType.Battlemaster => "Battlemaster",
            RtsProductionType.Building => "Barracks",
            RtsProductionType.DefenseTower => "Gattling Cannon",
            RtsProductionType.WarFactory => "War Factory",
            _ => "Nuclear Reactor"
        };
    }

    private static string GetPlacementLabel(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Building => "Barracks",
            RtsProductionType.DefenseTower => "Gattling Cannon",
            RtsProductionType.WarFactory => "War Factory",
            RtsProductionType.Reactor => "Reactor",
            _ => GetProductionLabel(productionType)
        };
    }

    private static string GetProductionQueueLabel(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => "Supply Truck",
            RtsProductionType.Guard => "Red Guard",
            RtsProductionType.TankHunter => "Tank Hunter",
            RtsProductionType.Battlemaster => "Battlemaster",
            RtsProductionType.Building => "Barracks",
            RtsProductionType.DefenseTower => "Gattling Cannon",
            RtsProductionType.WarFactory => "War Factory",
            _ => "Nuclear Reactor"
        };
    }

    private static bool TryMapToUnitRole(RtsProductionType productionType, out RtsUnitRole role)
    {
        switch (productionType)
        {
            case RtsProductionType.Worker:
                role = RtsUnitRole.Worker;
                return true;

            case RtsProductionType.Guard:
                role = RtsUnitRole.Guard;
                return true;

            case RtsProductionType.TankHunter:
                role = RtsUnitRole.TankHunter;
                return true;

            case RtsProductionType.Battlemaster:
                role = RtsUnitRole.Battlemaster;
                return true;

            default:
                role = default;
                return false;
        }
    }

    private static bool TryMapToStructureType(RtsProductionType productionType, out RtsStructureType structureType)
    {
        switch (productionType)
        {
            case RtsProductionType.Building:
                structureType = RtsStructureType.Building;
                return true;

            case RtsProductionType.DefenseTower:
                structureType = RtsStructureType.DefenseTower;
                return true;

            case RtsProductionType.WarFactory:
                structureType = RtsStructureType.WarFactory;
                return true;

            case RtsProductionType.Reactor:
                structureType = RtsStructureType.Reactor;
                return true;

            default:
                structureType = default;
                return false;
        }
    }

    private static Vector2[] GetStructurePadPositions(RtsStructureType structureType)
    {
        return structureType switch
        {
            RtsStructureType.Building => BuildingSitePositions,
            RtsStructureType.DefenseTower => DefenseTowerSitePositions,
            RtsStructureType.WarFactory => WarFactorySitePositions,
            _ => ReactorSitePositions
        };
    }

    private void ShowTransientMessage(string title, string subtitle, float duration)
    {
        if (_victory || _gameOver)
            return;

        _bannerTitle = title;
        _bannerSubtitle = subtitle;
        _bannerTimer = duration;
    }

    private enum ProductionAvailability
    {
        Ready,
        InsufficientOre,
        SiteFull,
        QueueFull
    }

    private sealed class ProductionOrder
    {
        public RtsProductionType Type { get; }

        public string Label { get; }

        public Vector2? ReservedSite { get; }

        public float RemainingTime { get; set; }

        public ProductionOrder(RtsProductionType productionType, float buildTime, Vector2? reservedSite)
        {
            Type = productionType;
            Label = GetProductionQueueLabel(productionType);
            ReservedSite = reservedSite;
            RemainingTime = buildTime;
        }
    }

    private sealed class ShotEffect
    {
        public Vector2 From { get; }

        public Vector2 To { get; }

        public Color Color { get; }

        public float RemainingTime { get; set; }

        public ShotEffect(Vector2 from, Vector2 to, Color color, float remainingTime)
        {
            From = from;
            To = to;
            Color = color;
            RemainingTime = remainingTime;
        }
    }
}