using AssemblyEngine.Core;
using AssemblyEngine.Networking;

namespace RtsSample;

public sealed partial class RtsGameScript
{
    private const float SnapshotBroadcastInterval = 0.1f;
    private readonly HashSet<int> _localSelectedUnitIds = [];
    private bool _multiplayerHooksLoaded;
    private float _snapshotBroadcastTimer;
    private RtsSessionMode _sessionMode = RtsSessionMode.SinglePlayer;
    private bool _matchRunning;

    public bool MatchRunning => _matchRunning;

    public bool IsMultiplayerMatch => _sessionMode is RtsSessionMode.Host or RtsSessionMode.Client;

    public void StartSinglePlayerMatch()
    {
        _sessionMode = RtsSessionMode.SinglePlayer;
        _matchRunning = true;
        _snapshotBroadcastTimer = 0f;
        _localSelectedUnitIds.Clear();
        ResetScenario();
    }

    internal RtsSessionStartPayload CreateHostedSessionStartPayload()
    {
        _sessionMode = RtsSessionMode.Host;
        _matchRunning = true;
        _snapshotBroadcastTimer = 0f;
        _localSelectedUnitIds.Clear();
        ResetScenario();
        return new RtsSessionStartPayload(CreateSnapshot());
    }

    internal void StartClientSession(RtsSessionStartPayload payload)
    {
        _sessionMode = RtsSessionMode.Client;
        _matchRunning = true;
        _snapshotBroadcastTimer = 0f;
        _localSelectedUnitIds.Clear();
        _activePlacementType = null;
        _selectionActive = false;
        ApplySnapshot(payload.Snapshot);
    }

    public void ReturnToFrontEnd()
    {
        _sessionMode = RtsSessionMode.SinglePlayer;
        _matchRunning = false;
        _snapshotBroadcastTimer = 0f;
        _localSelectedUnitIds.Clear();
        _activePlacementType = null;
        _selectionActive = false;
        ResetScenario();
    }

    public override void OnUnload()
    {
        if (!_multiplayerHooksLoaded)
            return;

        Engine.Multiplayer.MessageReceived -= HandleMultiplayerMessageReceived;
        Engine.Multiplayer.GameStarted -= HandleMultiplayerGameStarted;
        _multiplayerHooksLoaded = false;
    }

    private void OnMultiplayerLoaded()
    {
        if (_multiplayerHooksLoaded)
            return;

        Engine.Multiplayer.MessageReceived += HandleMultiplayerMessageReceived;
        Engine.Multiplayer.GameStarted += HandleMultiplayerGameStarted;
        _multiplayerHooksLoaded = true;
        _matchRunning = false;
        _sessionMode = RtsSessionMode.SinglePlayer;
    }

    private void UpdateClientReplica(float deltaTime, bool leftMouseDown, bool middleMouseDown, bool rightMouseDown)
    {
        if (IsKeyPressed(KeyCode.F1))
            _helpVisible = !_helpVisible;

        UpdateBanner(deltaTime);
        UpdateShotEffects(deltaTime);
        UpdateTacticalSignals(deltaTime);

        if (_victory || _gameOver)
        {
            if (IsKeyPressed(KeyCode.R) || IsKeyPressed(KeyCode.Enter))
                _ = Engine.Multiplayer.SendToHostAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.Restart, new RtsRestartCommand());

            return;
        }

        HandleClientHotkeys();
        HandleClientHudButtons(leftMouseDown);
        var placementInputConsumed = HandleClientStructurePlacement(leftMouseDown, rightMouseDown);
        HandleNavigation(leftMouseDown, middleMouseDown);
        UpdateCamera(deltaTime);
        HandleSelection(leftMouseDown, placementInputConsumed);
        CaptureLocalSelection();
        HandleClientCommands(rightMouseDown, placementInputConsumed);
    }

    private void UpdateHostedSnapshot(float deltaTime)
    {
        if (_sessionMode != RtsSessionMode.Host || Engine.Multiplayer.Peers.Count <= 1)
            return;

        _snapshotBroadcastTimer -= deltaTime;
        if (_snapshotBroadcastTimer > 0f)
            return;

        _snapshotBroadcastTimer = SnapshotBroadcastInterval;
        _ = Engine.Multiplayer.BroadcastAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.Snapshot, CreateSnapshot());
    }

    private void HandleMultiplayerGameStarted(object? sender, MultiplayerGameStartedEventArgs args)
    {
        if (Engine.Multiplayer.Role != MultiplayerSessionRole.Client)
            return;

        StartClientSession(args.DeserializePayload<RtsSessionStartPayload>());
    }

    private void HandleMultiplayerMessageReceived(object? sender, MultiplayerMessageEventArgs args)
    {
        if (!string.Equals(args.Channel, RtsMultiplayerChannel.Name, StringComparison.Ordinal))
            return;

        if (_sessionMode == RtsSessionMode.Host)
        {
            switch (args.Type)
            {
                case RtsMultiplayerMessageTypes.QueueProduction:
                    var queueCommand = args.DeserializePayload<RtsQueueProductionCommand>();
                    if (queueCommand.ReservedSite is { } reservedSite)
                        QueueProduction(queueCommand.Type, reservedSite);
                    else
                        QueueProduction(queueCommand.Type);
                    break;

                case RtsMultiplayerMessageTypes.IssueOrder:
                    var order = args.DeserializePayload<RtsIssueOrderCommand>();
                    IssueOrdersByIds(order.UnitIds, order.Target);
                    break;

                case RtsMultiplayerMessageTypes.SetRallyPoint:
                    var rallyPoint = args.DeserializePayload<RtsSetRallyPointCommand>();
                    _rallyPoint = ClampPointToWorld(rallyPoint.RallyPoint);
                    SetCommandPulse(_rallyPoint);
                    _audio.PlayRally();
                    break;

                case RtsMultiplayerMessageTypes.Restart:
                    ResetScenario();
                    break;
            }

            return;
        }

        if (_sessionMode == RtsSessionMode.Client && string.Equals(args.Type, RtsMultiplayerMessageTypes.Snapshot, StringComparison.Ordinal))
            ApplySnapshot(args.DeserializePayload<RtsGameSnapshot>());
    }

    private void HandleClientHotkeys()
    {
        if (IsKeyPressed(KeyCode.Escape))
            CancelStructurePlacement();

        if (IsKeyPressed(KeyCode.Q))
            _ = Engine.Multiplayer.SendToHostAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.QueueProduction, new RtsQueueProductionCommand(RtsProductionType.Worker, null));

        if (IsKeyPressed(KeyCode.E))
            _ = Engine.Multiplayer.SendToHostAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.QueueProduction, new RtsQueueProductionCommand(RtsProductionType.Guard, null));

        if (IsKeyPressed(KeyCode.R))
            BeginClientStructurePlacement(RtsProductionType.Building);

        if (IsKeyPressed(KeyCode.T))
            BeginClientStructurePlacement(RtsProductionType.DefenseTower);

        if (IsKeyPressed(KeyCode.D1))
            SelectPlayerUnits(unit => unit.Role == RtsUnitRole.Worker);

        if (IsKeyPressed(KeyCode.D2))
            SelectPlayerUnits(unit => unit.Role == RtsUnitRole.Guard);

        if (IsKeyPressed(KeyCode.D3))
            SelectPlayerUnits(unit => unit.IsPlayerControlled);

        if (IsKeyPressed(KeyCode.D1) || IsKeyPressed(KeyCode.D2) || IsKeyPressed(KeyCode.D3))
            CaptureLocalSelection();

        if (IsKeyPressed(KeyCode.Space))
            FocusCameraOnSelection();
    }

    private void HandleClientHudButtons(bool leftMouseDown)
    {
        if (!leftMouseDown || _leftMouseWasDown)
            return;

        if (IsPointInsideUiElement(MousePosition, "queue-worker-button"))
            _ = Engine.Multiplayer.SendToHostAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.QueueProduction, new RtsQueueProductionCommand(RtsProductionType.Worker, null));
        else if (IsPointInsideUiElement(MousePosition, "queue-guard-button"))
            _ = Engine.Multiplayer.SendToHostAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.QueueProduction, new RtsQueueProductionCommand(RtsProductionType.Guard, null));
        else if (IsPointInsideUiElement(MousePosition, "queue-building-button"))
            BeginClientStructurePlacement(RtsProductionType.Building);
        else if (IsPointInsideUiElement(MousePosition, "queue-tower-button"))
            BeginClientStructurePlacement(RtsProductionType.DefenseTower);
    }

    private bool HandleClientStructurePlacement(bool leftMouseDown, bool rightMouseDown)
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
            return true;

        _ = Engine.Multiplayer.SendToHostAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.QueueProduction, new RtsQueueProductionCommand(productionType, reservedSite));
        _activePlacementType = null;
        return true;
    }

    private void HandleClientCommands(bool rightMouseDown, bool placementInputConsumed)
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
            _ = Engine.Multiplayer.SendToHostAsync(RtsMultiplayerChannel.Name, RtsMultiplayerMessageTypes.SetRallyPoint, new RtsSetRallyPointCommand(worldPosition));
            return;
        }

        var issuedHarvestOrder = FindResourceNodeIndex(worldPosition) >= 0 && selectedUnits.Any(unit => unit.Role == RtsUnitRole.Worker);
        SetCommandPulse(worldPosition);
        _audio.PlayOrder(issuedHarvestOrder);
        _ = Engine.Multiplayer.SendToHostAsync(
            RtsMultiplayerChannel.Name,
            RtsMultiplayerMessageTypes.IssueOrder,
            new RtsIssueOrderCommand(selectedUnits.Select(unit => unit.Id).ToArray(), worldPosition));
    }

    private void BeginClientStructurePlacement(RtsProductionType productionType)
    {
        _activePlacementType = productionType;
        _selectionActive = false;
        _minimapNavigationActive = false;
        ShowTransientMessage(
            $"Place {GetProductionLabel(productionType)}",
            $"Click an open {GetPlacementLabel(productionType).ToLowerInvariant()} pad to queue the build.",
            1f);
    }

    private void IssueOrdersByIds(IEnumerable<int> unitIds, Vector2 worldPosition)
    {
        var unitIdSet = unitIds.ToHashSet();
        var selectedUnits = _units
            .Where(unit => unit.IsAlive && unit.IsPlayerControlled && unitIdSet.Contains(unit.Id))
            .ToList();
        if (selectedUnits.Count == 0)
            return;

        IssueOrders(selectedUnits, worldPosition);
    }

    private void CaptureLocalSelection()
    {
        _localSelectedUnitIds.Clear();
        foreach (var unit in _units)
        {
            if (unit.IsAlive && unit.IsPlayerControlled && unit.Selected)
                _localSelectedUnitIds.Add(unit.Id);
        }
    }
}