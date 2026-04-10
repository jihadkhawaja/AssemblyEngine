using AssemblyEngine.Core;
using AssemblyEngine.Scripting;

namespace RtsSample;

public sealed partial class RtsGameScript : GameScript
{
    private const float WorldWidth = 2200f;
    private const float WorldHeight = 1400f;
    private const float HeadquartersHalfWidth = 58f;
    private const float HeadquartersHalfHeight = 46f;
    private const float HeadquartersMaxHealth = 420f;
    private const int VictoryOreGoalValue = 320;
    private const int WorkerCost = 45;
    private const int GuardCost = 70;
    private const int BuildingCost = 85;
    private const int DefenseTowerCost = 95;
    private const int QueueLimit = 4;
    private const int SalvagePerRaider = 8;
    private const int HarvestAmount = 10;
    private const float HarvestInterval = 0.78f;
    private const float CameraPanSpeed = 540f;
    private const float EdgeScrollMargin = 18f;
    private const float WaveInterval = 18f;
    private const float SelectionThreshold = 8f;
    private const float MinimapWidth = 198f;
    private const float MinimapHeight = 126f;
    private const float MinimapMargin = 18f;
    private const float MinimapBottomOffset = 200f;
    private const float CommandPulseDuration = 0.9f;
    private const float NavigationPulseDuration = 0.72f;
    private static readonly Vector2 HeadquartersPosition = new(320f, 1060f);
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
        new Vector2(1410f, 1150f)
    ];
    private static readonly Vector2[] DefenseTowerSitePositions =
    [
        new Vector2(760f, 1030f),
        new Vector2(1090f, 820f),
        new Vector2(1460f, 610f),
        new Vector2(1740f, 900f)
    ];
    private static readonly string[] BlockingHudElementIds =
    [
        "top-bar",
        "center-message",
        "intel-panel",
        "production-panel",
        "tactical-panel",
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
    private Vector2 _selectionStartScreen;
    private Vector2 _selectionEndScreen;
    private float _commandPulseTimer;
    private float _hqHealth;
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
    private int _oreStockpile;
    private int _waveIndex;
    private string _bannerTitle = string.Empty;
    private string _bannerSubtitle = string.Empty;

    public int OreStockpile => _oreStockpile;

    public int OreGoal => VictoryOreGoalValue;

    public int HeadquartersHealth => (int)MathF.Ceiling(_hqHealth);

    public int WorkerCount => _units.Count(unit => unit.IsAlive && unit.Role == RtsUnitRole.Worker);

    public int GuardCount => _units.Count(unit => unit.IsAlive && unit.Role == RtsUnitRole.Guard);

    public int BuildingCount => _structures.Count(structure => structure.IsAlive && structure.Type == RtsStructureType.Building);

    public int DefenseTowerCount => _structures.Count(structure => structure.IsAlive && structure.Type == RtsStructureType.DefenseTower);

    public int RaiderCount => _units.Count(unit => unit.IsAlive && unit.IsEnemy);

    public string WorkerBuildButtonText => GetProductionButtonText(RtsProductionType.Worker, "Q");

    public string GuardBuildButtonText => GetProductionButtonText(RtsProductionType.Guard, "E");

    public string BuildingBuildButtonText => GetProductionButtonText(RtsProductionType.Building, "R");

    public string DefenseTowerBuildButtonText => GetProductionButtonText(RtsProductionType.DefenseTower, "T");

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
                return "Stockpile secured";

            if (_gameOver)
                return "Outpost lost";

            if (RaiderCount > 0)
                return $"Raid {_waveIndex} | Hostiles {RaiderCount}";

            return $"Raid {_waveIndex + 1} in {_nextWaveTimer:0.0}s";
        }
    }

    public string ObjectiveText
    {
        get
        {
            if (_victory)
                return "VICTORY | PRESS R";

            if (_gameOver)
                return "HQ LOST | PRESS R";

            var remainingOre = Math.Max(0, VictoryOreGoalValue - _oreStockpile);
            if (RaiderCount > 0)
                return $"RAID {RaiderCount} | ORE LEFT {remainingOre}";

            return $"MINE | ORE LEFT {remainingOre}";
        }
    }

    public string SelectedSummary
    {
        get
        {
            var selectedUnits = GetSelectedUnits();
            if (selectedUnits.Count == 0)
                return "NO SELECTION | RMB SETS RALLY";

            if (selectedUnits.Count == 1)
            {
                var unit = selectedUnits[0];
                if (unit.Role == RtsUnitRole.Worker)
                {
                    if (unit.OrderType == RtsUnitOrderType.Harvest && TryGetResourceNode(unit.AssignedNodeIndex, out var node))
                    {
                        var state = unit.ReturningToBase ? "Returning to HQ" : $"Mining {node.Name}";
                        return $"WORKER | {unit.CarryOre}/{unit.CarryCapacity} | {state.ToUpperInvariant()}";
                    }

                    return $"WORKER | {unit.CarryOre}/{unit.CarryCapacity} | RMB VEIN";
                }

                return "GUARD | AUTO DEFEND | RMB HOLD";
            }

            var workers = selectedUnits.Count(unit => unit.Role == RtsUnitRole.Worker);
            var guards = selectedUnits.Count(unit => unit.Role == RtsUnitRole.Guard);
            return $"SEL {selectedUnits.Count} | WRK {workers} | GRD {guards}";
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
            if (_productionQueue.Count == 0)
                return $"QUEUE 0/{QueueLimit}";

            var order = _productionQueue[0];
            return $"QUEUE {_productionQueue.Count}/{QueueLimit} | {order.Label.ToUpperInvariant()} {order.RemainingTime:0.0}s";
        }
    }

    public string ProductionModeText => _activePlacementType is { } productionType
        ? $"PLACE {GetPlacementLabel(productionType).ToUpperInvariant()} | CLICK OPEN PAD | RMB/ESC CANCEL"
        : "Q/E RECRUIT NOW | R/T PICK A BUILD PAD";

    public string ProductionSitesSummary => $"OPEN PADS | STRUCTURE {GetOpenSiteCount(RtsProductionType.Building)}/{BuildingSitePositions.Length} | TOWER {GetOpenSiteCount(RtsProductionType.DefenseTower)}/{DefenseTowerSitePositions.Length}";

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
            var injured = _units.Count(unit => unit.IsAlive && unit.IsPlayerControlled && unit.Health < unit.MaxHealth);
            return $"UNITS {WorkerCount + GuardCount} | RAIDERS {RaiderCount} | DMG {injured} | Q {_productionQueue.Count}/{QueueLimit}";
        }
    }

    public string EconomySummary
    {
        get
        {
            var activeVeins = _resourceNodes.Count(node => !node.IsDepleted);
            var fieldOre = _resourceNodes.Sum(node => Math.Max(0, node.RemainingOre));
            var carriedOre = _units.Where(unit => unit.IsAlive && unit.Role == RtsUnitRole.Worker).Sum(unit => unit.CarryOre);
            return $"ORE {fieldOre} | VEINS {activeVeins}/{_resourceNodes.Count} | CARRY {carriedOre}";
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
        _activePlacementType = null;
        _oreStockpile = 90;
        _hqHealth = HeadquartersMaxHealth;
        _waveIndex = 0;
        _missionTime = 0f;
        _nextWaveTimer = 12f;
        _bannerTimer = 4.5f;
        _bannerTitle = "Frontier Foundry";
        _bannerSubtitle = "Select the starting workers, right click cobalt veins, and hold the HQ until the stockpile reaches 320 ore.";
        _commandPulsePosition = Vector2.Zero;
        _navigationPulsePosition = Vector2.Zero;
        _commandPulseTimer = 0f;
        _navigationPulseTimer = 0f;
        _helpVisible = false;
        _victory = false;
        _gameOver = false;
        _selectionActive = false;
        _leftMouseWasDown = false;
        _middleMouseWasDown = false;
        _minimapNavigationActive = false;
        _rightMouseWasDown = false;
        _rallyPoint = HeadquartersPosition + new Vector2(220f, -40f);
        _cameraPosition = ClampCamera(HeadquartersPosition - new Vector2(240f, 280f));

        RtsStructure.ResetIds();
        RtsUnit.ResetIds();
        _resourceNodes.Add(new RtsResourceNode("West Vein", new Vector2(670f, 930f), 180));
        _resourceNodes.Add(new RtsResourceNode("Central Vein", new Vector2(1020f, 760f), 210));
        _resourceNodes.Add(new RtsResourceNode("North Vein", new Vector2(1360f, 520f), 210));
        _resourceNodes.Add(new RtsResourceNode("East Vein", new Vector2(1640f, 860f), 170));

        SpawnPlayerUnit(RtsUnitRole.Worker, HeadquartersPosition + new Vector2(-44f, 24f), sendToRally: false);
        SpawnPlayerUnit(RtsUnitRole.Worker, HeadquartersPosition + new Vector2(-12f, 54f), sendToRally: false);
        SpawnPlayerUnit(RtsUnitRole.Worker, HeadquartersPosition + new Vector2(30f, 16f), sendToRally: false);
        SpawnPlayerUnit(RtsUnitRole.Guard, HeadquartersPosition + new Vector2(76f, -18f), sendToRally: true);
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

        if (IsKeyPressed(KeyCode.R))
            QueueProduction(RtsProductionType.Building);

        if (IsKeyPressed(KeyCode.T))
            QueueProduction(RtsProductionType.DefenseTower);

        if (IsKeyPressed(KeyCode.D1))
            SelectPlayerUnits(unit => unit.Role == RtsUnitRole.Worker);

        if (IsKeyPressed(KeyCode.D2))
            SelectPlayerUnits(unit => unit.Role == RtsUnitRole.Guard);

        if (IsKeyPressed(KeyCode.D3))
            SelectPlayerUnits(unit => unit.IsPlayerControlled);

        if (IsKeyPressed(KeyCode.Space))
            FocusCameraOnSelection();
    }

    private void HandleHudButtons(bool leftMouseDown)
    {
        if (!leftMouseDown || _leftMouseWasDown)
            return;

        if (IsPointInsideUiElement(MousePosition, "queue-worker-button"))
        {
            QueueProduction(RtsProductionType.Worker);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "queue-guard-button"))
        {
            QueueProduction(RtsProductionType.Guard);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "queue-building-button"))
        {
            QueueProduction(RtsProductionType.Building);
            return;
        }

        if (IsPointInsideUiElement(MousePosition, "queue-tower-button"))
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

        if (!TryGetSelectableStructurePad(productionType, MousePosition, out var reservedSite))
        {
            _audio.PlayDenied();
            ShowTransientMessage(
                "Select Pad",
                $"Click a highlighted {GetPlacementLabel(productionType).ToLowerInvariant()} pad to queue this build.",
                1f);
            return true;
        }

        if (QueueProduction(productionType, reservedSite))
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

        IssueOrders(selectedUnits, worldPosition);
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
                if (unit.IsPlayerControlled && unit.IsAlive && selectionRect.Contains(unit.Position))
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
        if (UsesStructurePad(productionType))
        {
            BeginStructurePlacement(productionType);
            return false;
        }

        return QueueProduction(productionType, null);
    }

    private bool QueueProduction(RtsProductionType productionType, Vector2? reservedSite)
    {
        var availability = GetProductionAvailability(productionType);
        if (availability != ProductionAvailability.Ready)
        {
            ShowProductionUnavailable(productionType, availability);
            return false;
        }

        if (reservedSite is { } site && !IsStructurePadAvailable(productionType, site))
        {
            _audio.PlayDenied();
            ShowTransientMessage(
                "Pad Taken",
                $"That {GetPlacementLabel(productionType).ToLowerInvariant()} pad is no longer open.",
                1f);
            return false;
        }

        var cost = GetProductionCost(productionType);
        _oreStockpile -= cost;
        var buildTime = GetProductionBuildTime(productionType);
        _productionQueue.Add(new ProductionOrder(productionType, buildTime, reservedSite));
        _audio.PlayQueue(productionType);

        var subtitle = reservedSite is { } placementSite
            ? $"{GetProductionLabel(productionType)} reserved for the {DescribeSector(placementSite)} pad. Cost {cost} ore."
            : $"{GetProductionLabel(productionType)} fabrication started. Cost {cost} ore.";

        ShowTransientMessage(
            $"{GetProductionLabel(productionType)} Queued",
            subtitle,
            0.9f);
        return true;
    }

    private ProductionAvailability GetProductionAvailability(RtsProductionType productionType)
    {
        if (_productionQueue.Count >= QueueLimit)
            return ProductionAvailability.QueueFull;

        if (UsesStructurePad(productionType) && GetReservedSiteCount(productionType) >= GetProductionSiteCapacity(productionType))
            return ProductionAvailability.SiteFull;

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
            $"Click an open {GetPlacementLabel(productionType).ToLowerInvariant()} pad to queue the build. Right click or Esc cancels.",
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
        if (_productionQueue.Count == 0)
            return;

        var order = _productionQueue[0];
        order.RemainingTime -= deltaTime;
        if (order.RemainingTime > 0f)
            return;

        _productionQueue.RemoveAt(0);
        if (TryMapToUnitRole(order.Type, out var role))
        {
            var spawnOffset = new Vector2(84f + (_random.NextSingle() * 26f), -34f + (_random.NextSingle() * 68f));
            SpawnPlayerUnit(role, HeadquartersPosition + spawnOffset, sendToRally: true);
            _audio.PlayUnitReady();
            ShowTransientMessage(
                $"{GetProductionLabel(order.Type)} Ready",
                $"A new {GetProductionLabel(order.Type).ToLowerInvariant()} is moving toward the {DescribeSector(_rallyPoint)}.",
                1f);
            return;
        }

        if (!TryDeployStructure(order.Type, order.ReservedSite, out var structure))
        {
            _oreStockpile += GetProductionCost(order.Type);
            _audio.PlayDenied();
            ShowTransientMessage(
                "Pad Lost",
                $"{GetProductionLabel(order.Type)} fabrication completed but its reserved pad was no longer available. Ore refunded.",
                1.2f);
            return;
        }

        _audio.PlayUnitReady();
        ShowTransientMessage(
            $"{GetProductionLabel(order.Type)} Online",
            $"{GetProductionLabel(order.Type)} deployed in the {DescribeSector(structure.Position)}.",
            1f);
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
        if (worker.OrderType == RtsUnitOrderType.Harvest && TryGetResourceNode(worker.AssignedNodeIndex, out var node))
        {
            if ((worker.CarryOre >= worker.CarryCapacity || worker.ReturningToBase || node.IsDepleted) && worker.CarryOre > 0)
            {
                worker.ReturningToBase = true;
                if (MoveUnit(worker, HeadquartersPosition, deltaTime, HeadquartersHalfWidth + 12f))
                {
                    _oreStockpile += worker.CarryOre;
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

        worker.AssignedNodeIndex = -1;
        worker.ReturningToBase = false;
        worker.HarvestProgress = 0f;
        UpdateMoveOrder(worker, deltaTime);
    }

    private void UpdateGuard(RtsUnit guard, float deltaTime)
    {
        var target = FindNearestUnit(guard.Position, unit => unit.IsEnemy && unit.IsAlive, guard.DetectionRange);
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

        UpdateMoveOrder(guard, deltaTime);
    }

    private void UpdateDefenseTower(RtsStructure tower)
    {
        var target = FindNearestUnit(tower.Position, unit => unit.IsEnemy && unit.IsAlive, tower.DetectionRange);
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
        var target = FindNearestUnit(raider.Position, unit => unit.IsPlayerControlled && unit.IsAlive, raider.DetectionRange);
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

        var structureTarget = FindNearestStructure(raider.Position, structure => structure.IsAlive, raider.DetectionRange);
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

        var distanceToHq = Vector2.Distance(raider.Position, HeadquartersPosition);
        if (distanceToHq <= HeadquartersHalfWidth + raider.AttackRange)
        {
            if (raider.AttackCooldown <= 0f)
            {
                raider.AttackCooldown = raider.AttackInterval;
                _hqHealth = Math.Max(0f, _hqHealth - raider.AttackDamage);
                _shotEffects.Add(new ShotEffect(raider.Position, HeadquartersPosition, new Color(255, 122, 92), 0.12f));
                _audio.PlayUnderAttack();
            }
        }
        else
        {
            MoveUnit(raider, HeadquartersPosition, deltaTime, HeadquartersHalfWidth + raider.AttackRange - 4f);
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

    private void IssueOrders(IReadOnlyList<RtsUnit> selectedUnits, Vector2 worldPosition)
    {
        var resourceNodeIndex = FindResourceNodeIndex(worldPosition);
        var columns = (int)MathF.Ceiling(MathF.Sqrt(selectedUnits.Count));
        var issuedHarvestOrder = false;
        for (var index = 0; index < selectedUnits.Count; index++)
        {
            var unit = selectedUnits[index];
            if (!unit.IsAlive)
                continue;

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
            unit.ReturningToBase = false;
            unit.HasMoveTarget = true;
            unit.MoveTarget = ClampPointToWorld(worldPosition + ComputeFormationOffset(index, columns, 24f));
        }

        SetCommandPulse(resourceNodeIndex >= 0 ? _resourceNodes[resourceNodeIndex].Position : worldPosition);
        _audio.PlayOrder(issuedHarvestOrder);
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
        var salvage = 0;
        for (var index = _units.Count - 1; index >= 0; index--)
        {
            var unit = _units[index];
            if (unit.IsAlive)
                continue;

            if (unit.IsEnemy)
                salvage += SalvagePerRaider;

            _units.RemoveAt(index);
        }

        if (salvage > 0)
            _oreStockpile += salvage;
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
        if (!_gameOver && _hqHealth <= 0f)
        {
            _gameOver = true;
            _bannerTitle = "Outpost Lost";
            _bannerSubtitle = "The refinery collapsed under the raid. Press R or Enter to restart the mission.";
            _audio.PlayDefeat();
            return;
        }

        if (!_victory && _oreStockpile >= VictoryOreGoalValue)
        {
            _victory = true;
            _bannerTitle = "Stockpile Secure";
            _bannerSubtitle = $"{_oreStockpile} ore banked in {_missionTime:0.0}s. Press R or Enter to run the scenario again.";
            _audio.PlayVictory();
        }
    }

    private void DrawBattlefield()
    {
        Graphics.DrawFilledRect(0, 0, Engine.Width, Engine.Height, new Color(9, 14, 20));
        FillWorldRect(new Rectangle(0f, 0f, WorldWidth, 340f), new Color(36, 18, 20));
        FillWorldRect(new Rectangle(0f, 340f, WorldWidth, 440f), new Color(18, 28, 24));
        FillWorldRect(new Rectangle(0f, 780f, WorldWidth, WorldHeight - 780f), new Color(12, 22, 28));

        var visibleLeft = (int)(_cameraPosition.X / 80f) * 80;
        var visibleTop = (int)(_cameraPosition.Y / 80f) * 80;
        var visibleRight = (int)(_cameraPosition.X + Engine.Width) + 80;
        var visibleBottom = (int)(_cameraPosition.Y + Engine.Height) + 80;

        for (var x = visibleLeft; x <= visibleRight; x += 80)
        {
            var screenX = (int)(x - _cameraPosition.X);
            Graphics.DrawLine(screenX, 0, screenX, Engine.Height, new Color(22, 34, 42, 160));
        }

        for (var y = visibleTop; y <= visibleBottom; y += 80)
        {
            var screenY = (int)(y - _cameraPosition.Y);
            Graphics.DrawLine(0, screenY, Engine.Width, screenY, new Color(22, 34, 42, 160));
        }

        DrawBeaconMarker(EnemyBeaconPosition, 42, new Color(255, 98, 90), new Color(255, 199, 165));
        DrawBeaconMarker(_rallyPoint, 18, new Color(111, 210, 255), new Color(221, 245, 255));
    }

    private void DrawStructures()
    {
        var hqTopLeft = WorldToScreen(HeadquartersPosition - new Vector2(HeadquartersHalfWidth, HeadquartersHalfHeight));
        Graphics.DrawFilledRect((int)hqTopLeft.X, (int)hqTopLeft.Y, (int)(HeadquartersHalfWidth * 2f), (int)(HeadquartersHalfHeight * 2f), new Color(42, 78, 96));
        Graphics.DrawRect((int)hqTopLeft.X, (int)hqTopLeft.Y, (int)(HeadquartersHalfWidth * 2f), (int)(HeadquartersHalfHeight * 2f), new Color(167, 235, 255));
        Graphics.DrawFilledRect((int)(hqTopLeft.X + 16f), (int)(hqTopLeft.Y + 16f), 44, 24, new Color(255, 210, 108));
        Graphics.DrawFilledRect((int)(hqTopLeft.X + 70f), (int)(hqTopLeft.Y + 16f), 26, 50, new Color(86, 168, 196));
        DrawBar(HeadquartersPosition + new Vector2(0f, -66f), 112, 8, _hqHealth / HeadquartersMaxHealth, new Color(120, 255, 172));

        var beaconTopLeft = WorldToScreen(EnemyBeaconPosition - new Vector2(52f, 40f));
        Graphics.DrawFilledRect((int)beaconTopLeft.X, (int)beaconTopLeft.Y, 104, 80, new Color(64, 22, 20));
        Graphics.DrawRect((int)beaconTopLeft.X, (int)beaconTopLeft.Y, 104, 80, new Color(255, 130, 112));
        Graphics.DrawLine((int)(beaconTopLeft.X + 8f), (int)(beaconTopLeft.Y + 10f), (int)(beaconTopLeft.X + 96f), (int)(beaconTopLeft.Y + 70f), new Color(255, 180, 142));
        Graphics.DrawLine((int)(beaconTopLeft.X + 96f), (int)(beaconTopLeft.Y + 10f), (int)(beaconTopLeft.X + 8f), (int)(beaconTopLeft.Y + 70f), new Color(255, 180, 142));

        DrawStructurePads();
        DrawPlayerStructures();
    }

    private void DrawResourceNodes()
    {
        foreach (var node in _resourceNodes)
        {
            var screen = WorldToScreen(node.Position);
            if (!IsOnScreen(screen, 48f))
                continue;

            var baseColor = node.IsDepleted ? new Color(70, 74, 78) : new Color(97, 203, 255);
            var glowColor = node.IsDepleted ? new Color(102, 106, 112) : new Color(225, 244, 255);
            DrawDiamond(screen, 26, baseColor, glowColor);
            DrawBar(node.Position + new Vector2(0f, 40f), 48, 5, node.RemainingOre / 210f, new Color(250, 215, 116));
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
                Graphics.DrawCircle((int)screen.X, (int)screen.Y, (int)(unit.Radius + 7f), new Color(248, 249, 153));

            if (unit.Role == RtsUnitRole.Worker && unit.CarryOre > 0)
                Graphics.DrawFilledRect(x + size - 4, y - 4, 6, 6, new Color(255, 214, 92));

            if (unit.Health < unit.MaxHealth || unit.IsEnemy)
                DrawBar(unit.Position + new Vector2(0f, -18f), 30, 4, unit.Health / unit.MaxHealth, new Color(124, 255, 170));
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
        Graphics.DrawRect(left, top, width, height, new Color(160, 223, 255));
    }

    private void DrawTacticalSignals()
    {
        if (_commandPulseTimer > 0f)
            DrawPulse(_commandPulsePosition, _commandPulseTimer / CommandPulseDuration, new Color(255, 214, 96));

        if (_navigationPulseTimer > 0f)
            DrawPulse(_navigationPulsePosition, _navigationPulseTimer / NavigationPulseDuration, new Color(111, 210, 255));
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

        Graphics.DrawFilledRect(left, top, width, height, new Color(5, 10, 15, 210));
        Graphics.DrawFilledRect(left, top, width, verticalThird, new Color(36, 18, 20, 220));
        Graphics.DrawFilledRect(left, top + verticalThird, width, verticalThird, new Color(18, 28, 24, 220));
        Graphics.DrawFilledRect(left, top + (verticalThird * 2), width, height - (verticalThird * 2), new Color(12, 22, 28, 220));
        Graphics.DrawRect(left, top, width, height, new Color(118, 203, 236));
        Graphics.DrawLine(left + horizontalThird, top, left + horizontalThird, top + height, new Color(67, 103, 119, 160));
        Graphics.DrawLine(left + (horizontalThird * 2), top, left + (horizontalThird * 2), top + height, new Color(67, 103, 119, 160));
        Graphics.DrawLine(left, top + verticalThird, left + width, top + verticalThird, new Color(67, 103, 119, 160));
        Graphics.DrawLine(left, top + (verticalThird * 2), left + width, top + (verticalThird * 2), new Color(67, 103, 119, 160));

        foreach (var node in _resourceNodes)
        {
            var position = WorldToMinimap(node.Position, bounds);
            var color = node.IsDepleted ? new Color(82, 88, 96) : new Color(106, 211, 255);
            Graphics.DrawFilledRect((int)position.X - 2, (int)position.Y - 2, 4, 4, color);
        }

        var hq = WorldToMinimap(HeadquartersPosition, bounds);
        Graphics.DrawFilledRect((int)hq.X - 3, (int)hq.Y - 3, 6, 6, new Color(255, 214, 96));
        var beacon = WorldToMinimap(EnemyBeaconPosition, bounds);
        Graphics.DrawFilledRect((int)beacon.X - 3, (int)beacon.Y - 3, 6, 6, new Color(255, 120, 105));
        var rally = WorldToMinimap(_rallyPoint, bounds);
        DrawMinimapMarker(rally, new Color(111, 210, 255), 4);

        foreach (var structure in _structures)
        {
            if (!structure.IsAlive)
                continue;

            var point = WorldToMinimap(structure.Position, bounds);
            var color = structure.Type == RtsStructureType.Building ? new Color(255, 210, 146) : new Color(246, 246, 177);
            var size = structure.Type == RtsStructureType.Building ? 4 : 5;
            Graphics.DrawFilledRect((int)point.X - (size / 2), (int)point.Y - (size / 2), size, size, color);
        }

        foreach (var unit in _units)
        {
            if (!unit.IsAlive)
                continue;

            var point = WorldToMinimap(unit.Position, bounds);
            var color = unit.IsEnemy ? new Color(255, 120, 105) : unit.Role == RtsUnitRole.Worker ? new Color(111, 210, 255) : new Color(140, 255, 179);
            Graphics.DrawFilledRect((int)point.X - 1, (int)point.Y - 1, 3, 3, color);
            if (unit.Selected)
                Graphics.DrawRect((int)point.X - 3, (int)point.Y - 3, 6, 6, new Color(248, 249, 153));
        }

        if (_commandPulseTimer > 0f)
            DrawMinimapPulse(_commandPulsePosition, _commandPulseTimer / CommandPulseDuration, bounds, new Color(255, 214, 96));

        if (_navigationPulseTimer > 0f)
            DrawMinimapPulse(_navigationPulsePosition, _navigationPulseTimer / NavigationPulseDuration, bounds, new Color(111, 210, 255));

        if (IsPointInsideMinimap(MousePosition))
        {
            var cursorPoint = WorldToMinimap(MinimapToWorld(MousePosition), bounds);
            Graphics.DrawRect((int)cursorPoint.X - 3, (int)cursorPoint.Y - 3, 6, 6, new Color(248, 249, 153));
        }

        var cameraLeft = left + (int)((_cameraPosition.X / WorldWidth) * width);
        var cameraTop = top + (int)((_cameraPosition.Y / WorldHeight) * height);
        var cameraWidth = Math.Max(12, (int)((Engine.Width / WorldWidth) * width));
        var cameraHeight = Math.Max(12, (int)((Engine.Height / WorldHeight) * height));
        Graphics.DrawRect(cameraLeft, cameraTop, cameraWidth, cameraHeight, new Color(248, 249, 153));
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

    private RtsUnit SpawnPlayerUnit(RtsUnitRole role, Vector2 position, bool sendToRally)
    {
        var unit = new RtsUnit(role, position);
        if (sendToRally)
        {
            unit.OrderType = RtsUnitOrderType.Move;
            unit.HasMoveTarget = true;
            unit.MoveTarget = _rallyPoint;
        }

        _units.Add(unit);
        return unit;
    }

    private void SelectPlayerUnits(Func<RtsUnit, bool> predicate)
    {
        ClearSelection();
        foreach (var unit in _units)
        {
            if (unit.IsAlive && unit.IsPlayerControlled && predicate(unit))
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
        return _units.Where(unit => unit.IsAlive && unit.IsPlayerControlled && unit.Selected).ToList();
    }

    private RtsUnit? FindPlayerUnit(Vector2 worldPosition, float maxDistance)
    {
        var bestDistance = maxDistance;
        RtsUnit? result = null;

        foreach (var unit in _units)
        {
            if (!unit.IsAlive || !unit.IsPlayerControlled)
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
            target = unit.ReturningToBase ? HeadquartersPosition : node.Position;
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
        var focusPoint = selectedUnits.Count == 0 ? HeadquartersPosition : GetSelectionCenter(selectedUnits);
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
            Engine.Width - MinimapWidth - MinimapMargin,
            Engine.Height - MinimapHeight - MinimapBottomOffset,
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

    private void DrawStructurePads()
    {
        DrawStructurePads(BuildingSitePositions, RtsStructureType.Building);
        DrawStructurePads(DefenseTowerSitePositions, RtsStructureType.DefenseTower);
    }

    private void DrawStructurePads(Vector2[] pads, RtsStructureType structureType)
    {
        var placementActiveForType = false;
        var hoveredPad = Vector2.Zero;
        if (_activePlacementType is { } activeProductionType
            && TryMapToStructureType(activeProductionType, out var activeStructureType)
            && activeStructureType == structureType)
        {
            placementActiveForType = true;
            if (TryGetSelectableStructurePad(activeProductionType, MousePosition, out var activePad))
                hoveredPad = activePad;
        }

        for (var index = 0; index < pads.Length; index++)
        {
            var pad = pads[index];
            if (IsStructurePadOccupied(structureType, pad))
                continue;

            var screen = WorldToScreen(pad);
            if (!IsOnScreen(screen, structureType == RtsStructureType.Building ? 42f : 32f))
                continue;

            var reserved = IsStructurePadQueued(structureType, pad);
            var hovered = placementActiveForType && Vector2.Distance(hoveredPad, pad) <= 1f;

            if (structureType == RtsStructureType.Building)
            {
                var outline = hovered
                    ? new Color(255, 232, 150)
                    : placementActiveForType
                        ? new Color(228, 192, 128, 170)
                        : reserved
                            ? new Color(138, 116, 92, 150)
                            : new Color(188, 155, 119, 110);
                var fill = hovered
                    ? new Color(255, 220, 140, 62)
                    : placementActiveForType
                        ? new Color(220, 188, 126, 34)
                        : reserved
                            ? new Color(120, 96, 70, 44)
                            : Color.Transparent;

                if (fill.A > 0)
                    Graphics.DrawFilledRect((int)screen.X - 32, (int)screen.Y - 24, 64, 48, fill);

                Graphics.DrawRect((int)screen.X - 32, (int)screen.Y - 24, 64, 48, outline);
                Graphics.DrawLine((int)screen.X - 26, (int)screen.Y + 16, (int)screen.X + 26, (int)screen.Y + 16, outline);
            }
            else
            {
                var outline = hovered
                    ? new Color(255, 244, 170)
                    : placementActiveForType
                        ? new Color(246, 246, 177, 180)
                        : reserved
                            ? new Color(134, 136, 116, 150)
                            : new Color(246, 246, 177, 120);
                var fill = hovered
                    ? new Color(246, 246, 177, 56)
                    : placementActiveForType
                        ? new Color(246, 246, 177, 30)
                        : reserved
                            ? new Color(118, 122, 142, 40)
                            : Color.Transparent;

                if (fill.A > 0)
                    Graphics.DrawFilledRect((int)screen.X - 11, (int)screen.Y - 11, 22, 22, fill);

                Graphics.DrawCircle((int)screen.X, (int)screen.Y, 20, outline);
                Graphics.DrawRect((int)screen.X - 11, (int)screen.Y - 11, 22, 22, outline);
            }
        }
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
            Graphics.DrawFilledRect(left, top, width, height, structure.FillColor);
            Graphics.DrawRect(left, top, width, height, structure.AccentColor);

            if (structure.Type == RtsStructureType.Building)
            {
                Graphics.DrawFilledRect(left + 8, top + 10, width - 16, 12, new Color(68, 44, 34));
                Graphics.DrawFilledRect(left + 10, top + 26, 12, 10, new Color(255, 210, 108));
                Graphics.DrawFilledRect(left + width - 22, top + 26, 12, 10, new Color(255, 210, 108));
            }
            else
            {
                Graphics.DrawFilledRect(left + 4, top + 4, width - 8, height - 8, new Color(70, 74, 92));
                Graphics.DrawLine((int)screen.X, top - 8, (int)screen.X, top + 14, structure.AccentColor);
                Graphics.DrawLine((int)screen.X, top + 6, left + width + 10, top - 2, structure.AccentColor);
            }

            if (structure.Health < structure.MaxHealth)
                DrawBar(structure.Position + new Vector2(0f, -structure.HalfSize.Y - 16f), width + 12, 5, structure.Health / structure.MaxHealth, new Color(124, 255, 170));
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
        if (structureType == RtsStructureType.Building)
        {
            var bounds = new Rectangle(pad.X - 34f, pad.Y - 26f, 68f, 52f);
            return bounds.Contains(worldPosition);
        }

        return Vector2.Distance(worldPosition, pad) <= 24f;
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
            RtsUnitRole.Worker => "Worker",
            RtsUnitRole.Guard => "Guard",
            _ => "Raider"
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
        return productionType == RtsProductionType.Building || productionType == RtsProductionType.DefenseTower;
    }

    private static int GetProductionCost(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => WorkerCost,
            RtsProductionType.Guard => GuardCost,
            RtsProductionType.Building => BuildingCost,
            _ => DefenseTowerCost
        };
    }

    private static int GetProductionSiteCapacity(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Building => BuildingSitePositions.Length,
            RtsProductionType.DefenseTower => DefenseTowerSitePositions.Length,
            _ => 0
        };
    }

    private static float GetProductionBuildTime(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => 2.5f,
            RtsProductionType.Guard => 4.1f,
            RtsProductionType.Building => 5.4f,
            _ => 4.8f
        };
    }

    private static string GetProductionLabel(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => "Worker",
            RtsProductionType.Guard => "Guard",
            RtsProductionType.Building => "Structure",
            _ => "Defense Tower"
        };
    }

    private static string GetPlacementLabel(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Building => "Structure",
            RtsProductionType.DefenseTower => "Tower",
            _ => GetProductionLabel(productionType)
        };
    }

    private static string GetProductionQueueLabel(RtsProductionType productionType)
    {
        return productionType switch
        {
            RtsProductionType.Worker => "Worker",
            RtsProductionType.Guard => "Guard",
            RtsProductionType.Building => "Structure",
            _ => "Defense Tower"
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

            default:
                structureType = default;
                return false;
        }
    }

    private static Vector2[] GetStructurePadPositions(RtsStructureType structureType)
    {
        return structureType == RtsStructureType.Building ? BuildingSitePositions : DefenseTowerSitePositions;
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