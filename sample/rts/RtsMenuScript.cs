using AssemblyEngine.Core;
using AssemblyEngine.Networking;
using AssemblyEngine.Scripting;
using System.Runtime.InteropServices;

namespace RtsSample;

internal sealed class RtsMenuScript : GameScript
{
    private static readonly (KeyCode Key, char Value)[] LetterKeys =
    [
        (KeyCode.A, 'A'), (KeyCode.B, 'B'), (KeyCode.C, 'C'), (KeyCode.D, 'D'), (KeyCode.E, 'E'),
        (KeyCode.F, 'F'), (KeyCode.G, 'G'), (KeyCode.H, 'H'), (KeyCode.I, 'I'), (KeyCode.J, 'J'),
        (KeyCode.K, 'K'), (KeyCode.L, 'L'), (KeyCode.M, 'M'), (KeyCode.N, 'N'), (KeyCode.O, 'O'),
        (KeyCode.P, 'P'), (KeyCode.Q, 'Q'), (KeyCode.R, 'R'), (KeyCode.S, 'S'), (KeyCode.T, 'T'),
        (KeyCode.U, 'U'), (KeyCode.V, 'V'), (KeyCode.W, 'W'), (KeyCode.X, 'X'), (KeyCode.Y, 'Y'),
        (KeyCode.Z, 'Z')
    ];

    private static readonly (KeyCode Key, char Value)[] DigitKeys =
    [
        (KeyCode.D0, '0'), (KeyCode.D1, '1'), (KeyCode.D2, '2'), (KeyCode.D3, '3'), (KeyCode.D4, '4'),
        (KeyCode.D5, '5'), (KeyCode.D6, '6'), (KeyCode.D7, '7'), (KeyCode.D8, '8'), (KeyCode.D9, '9')
    ];

    private readonly RtsSampleSettings _settings;
    private readonly string _settingsPath;
    private readonly RtsMenuInputField _playerNameField;
    private readonly RtsMenuInputField _portField;
    private readonly RtsMenuInputField _addressField;
    private RtsGameScript _game = null!;
    private RtsMenuInputField? _focusedField;
    private Task? _pendingOperation;
    private bool _leftMouseWasDown;
    private LobbyMode _lobbyMode;
    private string _statusText = "Choose a deployment mode.";

    public RtsMenuScript(RtsSampleSettings settings, string settingsPath)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _playerNameField = new RtsMenuInputField("menu-player-value", 24, value => char.IsLetterOrDigit(value) || value == ' ', settings.PlayerName);
        _portField = new RtsMenuInputField("menu-port-value", 5, char.IsDigit, settings.MultiplayerPort.ToString());
        _addressField = new RtsMenuInputField("menu-address-value", 15, c => char.IsDigit(c) || c == '.', settings.PeerAddress);
    }

    public override void OnLoad()
    {
        _game = Engine.Scripts.GetScript<RtsGameScript>()
            ?? throw new InvalidOperationException("RtsGameScript must be registered before RtsMenuScript loads.");

        Engine.Multiplayer.StatusChanged += HandleStatusChanged;
        Engine.Multiplayer.GameStarted += HandleGameStarted;
        PersistSettings();
    }

    public override void OnUnload()
    {
        Engine.Multiplayer.StatusChanged -= HandleStatusChanged;
        Engine.Multiplayer.GameStarted -= HandleGameStarted;
    }

    public override void OnUpdate(float deltaTime)
    {
        var leftMouseDown = IsMouseDown(MouseButton.Left);
        UpdatePendingOperation();

        if (_game.MatchRunning)
        {
            _leftMouseWasDown = leftMouseDown;
            return;
        }

        HandleFieldFocus(leftMouseDown);
        HandleFieldInput();
        HandleButtons(leftMouseDown);
        _leftMouseWasDown = leftMouseDown;
    }

    public override void OnDraw()
    {
        if (Engine.UI is null)
            return;

        if (_game.MatchRunning && _lobbyMode != LobbyMode.None)
        {
            _lobbyMode = LobbyMode.None;
            _pendingOperation = null;
            _statusText = "Mission started.";
        }

        var inLobby = _lobbyMode != LobbyMode.None;
        Engine.UI.SetVisible("front-end-root", !_game.MatchRunning);
        Engine.UI.SetVisible("menu-shell", !_game.MatchRunning && !inLobby);
        Engine.UI.SetVisible("lobby-shell", !_game.MatchRunning && inLobby);

        Engine.UI.UpdateText("menu-player-value", FormatField(_playerNameField));
        Engine.UI.UpdateText("menu-port-value", FormatField(_portField));
        Engine.UI.UpdateText("menu-address-value", FormatField(_addressField));

        Engine.UI.UpdateText("menu-status", _statusText);
        Engine.UI.UpdateText("lobby-status", _statusText);
        Engine.UI.UpdateText("lobby-mode", GetLobbyModeLabel());
        Engine.UI.UpdateText("lobby-endpoint", GetLobbyEndpointText());
        Engine.UI.UpdateText("lobby-peer-1", GetPeerLine(0));
        Engine.UI.UpdateText("lobby-peer-2", GetPeerLine(1));
        Engine.UI.UpdateText("lobby-ready-button", GetReadyButtonText());

        var isHost = Engine.Multiplayer.Role == MultiplayerSessionRole.Host;
        Engine.UI.SetVisible("lobby-start-button", isHost);
        if (isHost)
            Engine.UI.UpdateText("lobby-start-button", _pendingOperation is null ? "START MISSION" : "WORKING...");
    }

    private void HandleStatusChanged(object? sender, MultiplayerStatusChangedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.Message))
            _statusText = args.Message;

        if (_game.IsMultiplayerMatch && args.Role == MultiplayerSessionRole.Offline && args.State == MultiplayerConnectionState.Disconnected)
        {
            _game.ReturnToFrontEnd();
            _lobbyMode = LobbyMode.None;
        }
    }

    private void HandleGameStarted(object? sender, MultiplayerGameStartedEventArgs args)
    {
        if (Engine.Multiplayer.Role == MultiplayerSessionRole.Client)
        {
            _lobbyMode = LobbyMode.None;
            _pendingOperation = null;
            _statusText = $"Mission started by {args.Initiator.DisplayName}.";
        }
    }

    private void HandleFieldFocus(bool leftMouseDown)
    {
        if (!leftMouseDown || _leftMouseWasDown)
            return;

        _focusedField = null;

        if (TryHit("menu-player-value"))
            _focusedField = _playerNameField;
        else if (TryHit("menu-port-value"))
            _focusedField = _portField;
        else if (TryHit("menu-address-value"))
            _focusedField = _addressField;
    }

    private void HandleFieldInput()
    {
        if (_focusedField is null || _pendingOperation is not null)
            return;

        var changed = false;
        if (IsKeyPressed(KeyCode.BackSpace))
            changed = _focusedField.Backspace();

        foreach (var (key, value) in LetterKeys)
        {
            if (_focusedField == _playerNameField && IsKeyPressed(key))
                changed |= _focusedField.Append(value);
        }

        foreach (var (key, value) in DigitKeys)
        {
            if (IsKeyPressed(key))
                changed |= _focusedField.Append(value);
        }

        if (ReferenceEquals(_focusedField, _addressField) && IsKeyPressed(KeyCode.OemPeriod))
            changed |= _focusedField.Append('.');

        if (_focusedField == _playerNameField && IsKeyPressed(KeyCode.Space))
            changed |= _focusedField.Append(' ');

        if (IsKeyDown(KeyCode.Control) && IsKeyPressed(KeyCode.V))
        {
            var clipboardText = ReadClipboardText();
            if (!string.IsNullOrEmpty(clipboardText))
            {
                _focusedField.SetValue(clipboardText);
                changed = true;
            }
        }

        if (changed)
            PersistSettings();
    }

    private void HandleButtons(bool leftMouseDown)
    {
        if (!leftMouseDown || _leftMouseWasDown || _pendingOperation is not null)
            return;

        if (_lobbyMode == LobbyMode.None)
        {
            if (TryHit("menu-solo-button"))
                StartSolo();
            else if (TryHit("menu-host-peer-button"))
                BeginLobby(LobbyMode.HostPeer);
            else if (TryHit("menu-join-peer-button"))
                BeginLobby(LobbyMode.JoinPeer);
            else if (TryHit("menu-host-local-button"))
                BeginLobby(LobbyMode.HostLocal);
            else if (TryHit("menu-join-local-button"))
                BeginLobby(LobbyMode.JoinLocal);

            return;
        }

        if (TryHit("lobby-ready-button"))
        {
            var localPeer = Engine.Multiplayer.Peers.FirstOrDefault(peer => peer.IsLocal);
            var nextReadyState = localPeer is null || !localPeer.IsReady;
            BeginOperation(() => Engine.Multiplayer.SetReadyAsync(nextReadyState), "Updating readiness...");
        }
        else if (TryHit("lobby-start-button") && Engine.Multiplayer.Role == MultiplayerSessionRole.Host)
            StartHostedMatch();
        else if (TryHit("lobby-back-button"))
            LeaveLobby();
    }

    private void StartSolo()
    {
        PersistSettings();
        _game.StartSinglePlayerMatch();
        _statusText = "Single-player mission started.";
        _lobbyMode = LobbyMode.None;
    }

    private void BeginLobby(LobbyMode mode)
    {
        PersistSettings();
        _lobbyMode = mode;

        if (mode is LobbyMode.HostPeer or LobbyMode.HostLocal)
        {
            var transportMode = mode == LobbyMode.HostLocal ? MultiplayerTransportMode.Localhost : MultiplayerTransportMode.PeerToPeer;
            BeginOperation(
                () => Engine.Multiplayer.HostAsync(new MultiplayerHostOptions(ParsePort(), _playerNameField.Value, transportMode)),
                "Opening lobby...");
            return;
        }

        var address = mode == LobbyMode.JoinLocal ? "127.0.0.1" : _addressField.Value;
        var joinTransport = mode == LobbyMode.JoinLocal ? MultiplayerTransportMode.Localhost : MultiplayerTransportMode.PeerToPeer;
        BeginOperation(
            () => Engine.Multiplayer.JoinAsync(new MultiplayerJoinOptions(address, ParsePort(), _playerNameField.Value, joinTransport)),
            $"Connecting to {address}:{ParsePort()}...");
    }

    private void StartHostedMatch()
    {
        BeginOperation(async () =>
        {
            var payload = _game.CreateHostedSessionStartPayload();
            await Engine.Multiplayer.StartGameAsync(payload);
            _statusText = "Mission started.";
        }, "Starting mission...");
    }

    private void LeaveLobby()
    {
        BeginOperation(async () =>
        {
            await Engine.Multiplayer.StopAsync();
            _lobbyMode = LobbyMode.None;
            _statusText = "Returned to the main menu.";
        }, "Closing lobby...");
    }

    private void BeginOperation(Func<Task> operation, string statusText)
    {
        _statusText = statusText;
        _pendingOperation = operation();
    }

    private void UpdatePendingOperation()
    {
        if (_pendingOperation is null || !_pendingOperation.IsCompleted)
            return;

        if (_pendingOperation.IsFaulted)
        {
            _statusText = _pendingOperation.Exception?.GetBaseException().Message ?? "Multiplayer operation failed.";
            _lobbyMode = _game.MatchRunning ? _lobbyMode : LobbyMode.None;
        }
        else if (_lobbyMode == LobbyMode.None && !_game.MatchRunning)
        {
            _statusText = "Choose a deployment mode.";
        }

        _pendingOperation = null;
    }

    private string GetPeerLine(int index)
    {
        var peers = Engine.Multiplayer.Peers;
        if (index >= peers.Count)
            return "OPEN SLOT";

        var peer = peers[index];
        var location = peer.IsLocal ? "LOCAL" : "REMOTE";
        var ready = peer.IsReady ? "READY" : "WAITING";
        return $"{peer.DisplayName.ToUpperInvariant()} | {location} | {ready}";
    }

    private string GetLobbyModeLabel() => _lobbyMode switch
    {
        LobbyMode.HostPeer => "DIRECT HOST | SHARE YOUR IPv4 ADDRESS",
        LobbyMode.JoinPeer => "DIRECT JOIN | ENTER THE HOST IPv4 ADDRESS",
        LobbyMode.HostLocal => "LOCALHOST HOST | USE A SECOND CLIENT ON THIS MACHINE",
        LobbyMode.JoinLocal => "LOCALHOST JOIN | CONNECTING TO 127.0.0.1",
        _ => ""
    };

    private string GetLobbyEndpointText()
    {
        if (_lobbyMode is LobbyMode.HostPeer or LobbyMode.HostLocal)
        {
            var addresses = Engine.Multiplayer.LocalAddresses;
            var addressText = addresses.Count > 0 ? string.Join(" | ", addresses) : "waiting for bind";
            return $"JOIN TARGET | {addressText} | PORT {ParsePort()}";
        }

        return $"JOIN TARGET | {_addressField.Value} | PORT {ParsePort()}";
    }

    private string GetReadyButtonText()
    {
        var localPeer = Engine.Multiplayer.Peers.FirstOrDefault(peer => peer.IsLocal);
        return localPeer is not null && localPeer.IsReady ? "SET NOT READY" : "SET READY";
    }

    private string FormatField(RtsMenuInputField field)
    {
        var prefix = ReferenceEquals(_focusedField, field) ? "| " : "  ";
        var value = string.IsNullOrWhiteSpace(field.Value) ? "-" : field.Value;
        return prefix + value;
    }

    private static string? ReadClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero))
            return null;

        try
        {
            var handle = GetClipboardData(13); // CF_UNICODETEXT
            if (handle == IntPtr.Zero)
                return null;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private bool TryHit(string elementId)
    {
        return Engine.UI is not null
            && Engine.UI.TryGetBounds(elementId, Engine.Width, Engine.Height, out var bounds)
            && bounds.Contains(MousePosition);
    }

    private int ParsePort()
    {
        return int.TryParse(_portField.Value, out var port)
            ? Math.Clamp(port, 1024, 65535)
            : 40444;
    }

    private void PersistSettings()
    {
        _settings.PlayerName = string.IsNullOrWhiteSpace(_playerNameField.Value) ? "Commander" : _playerNameField.Value;
        _settings.MultiplayerPort = ParsePort();
        _settings.PeerAddress = _addressField.Value;
        RtsSampleSettingsStore.Save(_settingsPath, _settings);
    }

    private enum LobbyMode
    {
        None,
        HostPeer,
        JoinPeer,
        HostLocal,
        JoinLocal
    }
}