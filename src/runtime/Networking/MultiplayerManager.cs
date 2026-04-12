using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AssemblyEngine.Networking;

public sealed class MultiplayerManager : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Queue<Action> _pendingDispatches = [];
    private readonly Dictionary<string, MultiplayerConnection> _connections = [];
    private readonly JsonSerializerOptions _serializerOptions = MultiplayerSerialization.Options;
    private CancellationTokenSource? _lifetime;
    private TcpListener? _listener;
    private Task? _acceptLoopTask;
    private MultiplayerConnection? _hostConnection;
    private Dictionary<string, MultiplayerPeerInfo> _peers = [];
    private MultiplayerSessionRole _role = MultiplayerSessionRole.Offline;
    private MultiplayerConnectionState _state = MultiplayerConnectionState.Disconnected;
    private string _localPeerId = Guid.NewGuid().ToString("N");
    private string _localDisplayName = "Commander";
    private string _sessionId = string.Empty;
    private string? _statusMessage;
    private Exception? _lastException;
    private string[] _localAddresses = [];
    private int _listeningPort;

    public event EventHandler? PeersChanged;
    public event EventHandler<MultiplayerMessageEventArgs>? MessageReceived;
    public event EventHandler<MultiplayerGameStartedEventArgs>? GameStarted;
    public event EventHandler<MultiplayerStatusChangedEventArgs>? StatusChanged;

    public MultiplayerSessionRole Role
    {
        get
        {
            lock (_gate)
                return _role;
        }
    }

    public MultiplayerConnectionState State
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    public string LocalPeerId
    {
        get
        {
            lock (_gate)
                return _localPeerId;
        }
    }

    public string LocalDisplayName
    {
        get
        {
            lock (_gate)
                return _localDisplayName;
        }
    }

    public string? StatusMessage
    {
        get
        {
            lock (_gate)
                return _statusMessage;
        }
    }

    public Exception? LastException
    {
        get
        {
            lock (_gate)
                return _lastException;
        }
    }

    public int ListeningPort
    {
        get
        {
            lock (_gate)
                return _listeningPort;
        }
    }

    public IReadOnlyList<string> LocalAddresses
    {
        get
        {
            lock (_gate)
                return _localAddresses.ToArray();
        }
    }

    public IReadOnlyList<MultiplayerPeerInfo> Peers
    {
        get
        {
            lock (_gate)
            {
                return _peers.Values
                    .OrderByDescending(peer => peer.IsLocal)
                    .ThenBy(peer => peer.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
    }

    public bool IsSessionActive => Role is MultiplayerSessionRole.Host or MultiplayerSessionRole.Client;

    public void Pump()
    {
        while (true)
        {
            Action? dispatch = null;
            lock (_gate)
            {
                if (_pendingDispatches.Count > 0)
                    dispatch = _pendingDispatches.Dequeue();
            }

            if (dispatch is null)
                break;

            dispatch();
        }
    }

    public async Task HostAsync(MultiplayerHostOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Port < 0 || options.Port > 65535)
            throw new ArgumentOutOfRangeException(nameof(options.Port));

        await StopAsync();

        var bindAddress = options.TransportMode == MultiplayerTransportMode.Localhost
            ? IPAddress.Loopback
            : IPAddress.Any;

        var listener = new TcpListener(bindAddress, options.Port);
        listener.Start(Math.Max(1, options.MaxConnections));

        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var listeningPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        var localPeerId = Guid.NewGuid().ToString("N");
        var localDisplayName = SanitizeDisplayName(options.DisplayName, fallback: "Host");
        var localEndPoint = options.TransportMode == MultiplayerTransportMode.Localhost
            ? $"127.0.0.1:{listeningPort}"
            : $"{string.Join(", ", EnumerateLocalIpv4Addresses())}:{listeningPort}";

        lock (_gate)
        {
            _listener = listener;
            _lifetime = lifetime;
            _acceptLoopTask = Task.Run(() => AcceptLoopAsync(lifetime.Token));
            _hostConnection = null;
            _connections.Clear();
            _peers = [];
            _role = MultiplayerSessionRole.Host;
            _state = MultiplayerConnectionState.Hosting;
            _localPeerId = localPeerId;
            _localDisplayName = localDisplayName;
            _sessionId = Guid.NewGuid().ToString("N");
            _localAddresses = options.TransportMode == MultiplayerTransportMode.Localhost
                ? ["127.0.0.1"]
                : EnumerateLocalIpv4Addresses();
            _listeningPort = listeningPort;
            _peers[localPeerId] = new MultiplayerPeerInfo(localPeerId, localDisplayName, true, false, localEndPoint);
        }

        QueuePeersChanged();
        QueueStatusChanged(MultiplayerSessionRole.Host, MultiplayerConnectionState.Hosting, $"Hosting on port {listeningPort}.", null);
    }

    public async Task JoinAsync(MultiplayerJoinOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Address))
            throw new ArgumentException("Join address is required.", nameof(options.Address));

        if (options.Port <= 0 || options.Port > 65535)
            throw new ArgumentOutOfRangeException(nameof(options.Port));

        await StopAsync();

        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var localPeerId = Guid.NewGuid().ToString("N");
        var localDisplayName = SanitizeDisplayName(options.DisplayName, fallback: "Client");

        lock (_gate)
        {
            _lifetime = lifetime;
            _listener = null;
            _acceptLoopTask = null;
            _connections.Clear();
            _hostConnection = null;
            _peers = [];
            _role = MultiplayerSessionRole.Client;
            _state = MultiplayerConnectionState.Connecting;
            _localPeerId = localPeerId;
            _localDisplayName = localDisplayName;
            _sessionId = string.Empty;
            _localAddresses = [];
            _listeningPort = 0;
            _peers[localPeerId] = new MultiplayerPeerInfo(localPeerId, localDisplayName, true, false, "local");
        }

        QueuePeersChanged();
        QueueStatusChanged(MultiplayerSessionRole.Client, MultiplayerConnectionState.Connecting, $"Connecting to {options.Address}:{options.Port}.", null);

        try
        {
            var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(options.Address, options.Port, cancellationToken);

            var connection = new MultiplayerConnection(client, _serializerOptions);
            lock (_gate)
            {
                _hostConnection = connection;
            }

            _ = Task.Run(() => RunConnectionAsync(connection, lifetime.Token));
            await connection.SendAsync(
                CreateEnvelope(
                    MultiplayerEnvelopeKind.Hello,
                    senderId: localPeerId,
                    payload: new MultiplayerHelloPayload(localPeerId, localDisplayName, false)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            UpdateState(MultiplayerSessionRole.Offline, MultiplayerConnectionState.Error, ex.Message, ex, clearPeers: true);
            throw;
        }
    }

    public async Task SetReadyAsync(bool ready, CancellationToken cancellationToken = default)
    {
        MultiplayerEnvelope? outboundEnvelope;
        lock (_gate)
        {
            if (!_peers.TryGetValue(_localPeerId, out var current))
                return;

            var updated = current with { IsReady = ready, IsLocal = true };
            _peers[_localPeerId] = updated;
            outboundEnvelope = CreateEnvelope(MultiplayerEnvelopeKind.PeerUpdated, _localPeerId, updated with { IsLocal = false });
        }

        QueuePeersChanged();
        await SendEnvelopeAccordingToRoleAsync(outboundEnvelope, cancellationToken);
    }

    public async Task BroadcastAsync<T>(string channel, string type, T payload, CancellationToken cancellationToken = default)
    {
        if (Role != MultiplayerSessionRole.Host)
            throw new InvalidOperationException("Only the host can broadcast multiplayer app messages.");

        var envelope = CreateEnvelope(MultiplayerEnvelopeKind.AppMessage, LocalPeerId, payload, channel, type);
        await BroadcastEnvelopeAsync(envelope, exceptPeerId: null, cancellationToken);
    }

    public async Task SendToHostAsync<T>(string channel, string type, T payload, CancellationToken cancellationToken = default)
    {
        if (Role != MultiplayerSessionRole.Client)
            throw new InvalidOperationException("Only a client can send directed messages to the host.");

        var envelope = CreateEnvelope(MultiplayerEnvelopeKind.AppMessage, LocalPeerId, payload, channel, type);
        MultiplayerConnection hostConnection = GetRequiredHostConnection();
        await hostConnection.SendAsync(envelope, cancellationToken);
    }

    public async Task StartGameAsync<T>(T payload, CancellationToken cancellationToken = default)
    {
        if (Role != MultiplayerSessionRole.Host)
            throw new InvalidOperationException("Only the host can start the multiplayer session.");

        var envelope = CreateEnvelope(MultiplayerEnvelopeKind.GameStart, LocalPeerId, payload);
        await BroadcastEnvelopeAsync(envelope, exceptPeerId: null, cancellationToken);

        var initiator = GetPeerSnapshot(LocalPeerId);
        EnqueueDispatch(() => GameStarted?.Invoke(this, new MultiplayerGameStartedEventArgs(initiator, envelope.Payload.Clone())));
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? lifetime;
        TcpListener? listener;
        Task? acceptLoopTask;
        MultiplayerConnection? hostConnection;
        MultiplayerConnection[] remoteConnections;

        lock (_gate)
        {
            lifetime = _lifetime;
            listener = _listener;
            acceptLoopTask = _acceptLoopTask;
            hostConnection = _hostConnection;
            remoteConnections = _connections.Values.ToArray();
            _lifetime = null;
            _listener = null;
            _acceptLoopTask = null;
            _hostConnection = null;
            _connections.Clear();
            _peers = [];
            _role = MultiplayerSessionRole.Offline;
            _state = MultiplayerConnectionState.Disconnected;
            _sessionId = string.Empty;
            _localAddresses = [];
            _listeningPort = 0;
            _statusMessage = "Disconnected.";
            _lastException = null;
        }

        lifetime?.Cancel();
        listener?.Stop();

        if (hostConnection is not null)
            await hostConnection.DisposeAsync();

        foreach (var connection in remoteConnections)
            await connection.DisposeAsync();

        if (acceptLoopTask is not null)
        {
            try
            {
                await acceptLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        lifetime?.Dispose();
        QueuePeersChanged();
        QueueStatusChanged(MultiplayerSessionRole.Offline, MultiplayerConnectionState.Disconnected, "Disconnected.", null);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        TcpListener listener;
        lock (_gate)
        {
            listener = _listener ?? throw new InvalidOperationException("Listener was not initialized.");
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
                var connection = new MultiplayerConnection(client, _serializerOptions);
                _ = Task.Run(() => RunConnectionAsync(connection, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunConnectionAsync(MultiplayerConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await connection.ReadLoopAsync(envelope => HandleIncomingAsync(connection, envelope, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _statusMessage = ex.Message;
                _lastException = ex;
            }
            QueueStatusChanged(Role, MultiplayerConnectionState.Error, ex.Message, ex);
        }
        finally
        {
            await HandleDisconnectAsync(connection, cancellationToken);
            await connection.DisposeAsync();
        }
    }

    private async Task HandleIncomingAsync(MultiplayerConnection connection, MultiplayerEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.Kind)
        {
            case MultiplayerEnvelopeKind.Hello:
                await HandleHelloAsync(connection, envelope, cancellationToken);
                break;

            case MultiplayerEnvelopeKind.Welcome:
                HandleWelcome(connection, envelope);
                break;

            case MultiplayerEnvelopeKind.PeerUpdated:
                await HandlePeerUpdatedAsync(connection, envelope, cancellationToken);
                break;

            case MultiplayerEnvelopeKind.PeerRemoved:
                HandlePeerRemoved(envelope);
                break;

            case MultiplayerEnvelopeKind.AppMessage:
                await HandleAppMessageAsync(connection, envelope, cancellationToken);
                break;

            case MultiplayerEnvelopeKind.GameStart:
                HandleGameStart(envelope);
                break;

            case MultiplayerEnvelopeKind.Error:
                HandleError(envelope);
                break;
        }
    }

    private async Task HandleHelloAsync(MultiplayerConnection connection, MultiplayerEnvelope envelope, CancellationToken cancellationToken)
    {
        if (Role != MultiplayerSessionRole.Host)
            return;

        var hello = envelope.Payload.Deserialize<MultiplayerHelloPayload>(MultiplayerSerialization.Options)
            ?? throw new InvalidDataException("Multiplayer hello payload was missing.");

        var peerId = string.IsNullOrWhiteSpace(hello.PeerId) ? Guid.NewGuid().ToString("N") : hello.PeerId;
        var peer = new MultiplayerPeerInfo(
            peerId,
            SanitizeDisplayName(hello.DisplayName, fallback: "Peer"),
            false,
            hello.IsReady,
            connection.EndPoint);

        lock (_gate)
        {
            connection.PeerId = peerId;
            _connections[peerId] = connection;
            _peers[peerId] = peer;
            _state = MultiplayerConnectionState.Connected;
            _statusMessage = $"Peer '{peer.DisplayName}' connected.";
            _lastException = null;
        }

        QueuePeersChanged();
        QueueStatusChanged(MultiplayerSessionRole.Host, MultiplayerConnectionState.Connected, $"Peer '{peer.DisplayName}' connected.", null);

        await connection.SendAsync(
            CreateEnvelope(
                MultiplayerEnvelopeKind.Welcome,
                senderId: LocalPeerId,
                payload: new MultiplayerWelcomePayload(_sessionId, LocalPeerId, SnapshotPeersForRemote())),
            cancellationToken);

        await BroadcastEnvelopeAsync(CreateEnvelope(MultiplayerEnvelopeKind.PeerUpdated, LocalPeerId, peer), exceptPeerId: peerId, cancellationToken);
    }

    private void HandleWelcome(MultiplayerConnection connection, MultiplayerEnvelope envelope)
    {
        if (Role != MultiplayerSessionRole.Client)
            return;

        var welcome = envelope.Payload.Deserialize<MultiplayerWelcomePayload>(MultiplayerSerialization.Options)
            ?? throw new InvalidDataException("Multiplayer welcome payload was missing.");

        lock (_gate)
        {
            _sessionId = welcome.SessionId;
            _state = MultiplayerConnectionState.Connected;
            _statusMessage = $"Connected to host '{welcome.HostPeerId}'.";
            _lastException = null;

            var peers = new Dictionary<string, MultiplayerPeerInfo>(StringComparer.Ordinal);
            foreach (var peer in welcome.Peers ?? [])
            {
                if (peer is null || string.IsNullOrWhiteSpace(peer.Id))
                    continue;

                peers[peer.Id] = peer.Id == _localPeerId
                    ? peer with { IsLocal = true }
                    : peer with { IsLocal = false };
            }

            if (!string.IsNullOrWhiteSpace(welcome.HostPeerId) && !peers.ContainsKey(welcome.HostPeerId))
            {
                peers[welcome.HostPeerId] = new MultiplayerPeerInfo(welcome.HostPeerId, "Host", false, false, connection.EndPoint);
            }

            if (!peers.ContainsKey(_localPeerId))
            {
                peers[_localPeerId] = new MultiplayerPeerInfo(_localPeerId, _localDisplayName, true, false, "local");
            }

            _peers = peers;
            _hostConnection = connection;
        }

        QueuePeersChanged();
        QueueStatusChanged(MultiplayerSessionRole.Client, MultiplayerConnectionState.Connected, "Connected to host.", null);
    }

    private async Task HandlePeerUpdatedAsync(MultiplayerConnection connection, MultiplayerEnvelope envelope, CancellationToken cancellationToken)
    {
        var peer = envelope.Payload.Deserialize<MultiplayerPeerInfo>(MultiplayerSerialization.Options)
            ?? throw new InvalidDataException("Peer update payload was missing.");

        if (Role == MultiplayerSessionRole.Host)
        {
            if (connection.PeerId is null)
                return;

            var updated = peer with { Id = connection.PeerId, IsLocal = false };
            lock (_gate)
            {
                _peers[connection.PeerId] = updated;
            }

            QueuePeersChanged();
            await BroadcastEnvelopeAsync(CreateEnvelope(MultiplayerEnvelopeKind.PeerUpdated, updated.Id, updated), exceptPeerId: updated.Id, cancellationToken);
            return;
        }

        lock (_gate)
        {
            _peers[peer.Id] = peer.Id == _localPeerId
                ? peer with { IsLocal = true }
                : peer with { IsLocal = false };
        }

        QueuePeersChanged();
    }

    private void HandlePeerRemoved(MultiplayerEnvelope envelope)
    {
        var removed = envelope.Payload.Deserialize<MultiplayerPeerRemovedPayload>(MultiplayerSerialization.Options)
            ?? throw new InvalidDataException("Peer removed payload was missing.");

        lock (_gate)
        {
            _peers.Remove(removed.PeerId);
        }

        QueuePeersChanged();
    }

    private async Task HandleAppMessageAsync(MultiplayerConnection connection, MultiplayerEnvelope envelope, CancellationToken cancellationToken)
    {
        var senderId = connection.PeerId ?? envelope.SenderId;
        if (string.IsNullOrWhiteSpace(senderId))
            return;

        var peer = GetPeerSnapshot(senderId);

        if (Role == MultiplayerSessionRole.Host)
        {
            EnqueueDispatch(() =>
                MessageReceived?.Invoke(
                    this,
                    new MultiplayerMessageEventArgs(peer, envelope.Channel ?? string.Empty, envelope.Type ?? string.Empty, envelope.Payload.Clone())));
            return;
        }

        EnqueueDispatch(() =>
            MessageReceived?.Invoke(
                this,
                new MultiplayerMessageEventArgs(peer, envelope.Channel ?? string.Empty, envelope.Type ?? string.Empty, envelope.Payload.Clone())));

        await Task.CompletedTask;
    }

    private void HandleGameStart(MultiplayerEnvelope envelope)
    {
        var initiator = GetPeerSnapshot(envelope.SenderId ?? LocalPeerId);
        EnqueueDispatch(() => GameStarted?.Invoke(this, new MultiplayerGameStartedEventArgs(initiator, envelope.Payload.Clone())));
    }

    private void HandleError(MultiplayerEnvelope envelope)
    {
        var error = envelope.Payload.Deserialize<MultiplayerErrorPayload>(MultiplayerSerialization.Options);
        QueueStatusChanged(Role, MultiplayerConnectionState.Error, error?.Message ?? "Unknown multiplayer error.", null);
    }

    private async Task HandleDisconnectAsync(MultiplayerConnection connection, CancellationToken cancellationToken)
    {
        string? removedPeerId;
        MultiplayerSessionRole currentRole;
        bool hostDisconnected;

        lock (_gate)
        {
            currentRole = _role;
            hostDisconnected = ReferenceEquals(connection, _hostConnection);
            removedPeerId = connection.PeerId;

            if (removedPeerId is not null)
                _connections.Remove(removedPeerId);
        }

        if (currentRole == MultiplayerSessionRole.Client && hostDisconnected)
        {
            var previousFailure = LastException;
            UpdateState(
                MultiplayerSessionRole.Offline,
                MultiplayerConnectionState.Disconnected,
                previousFailure?.Message ?? "Disconnected from host.",
                previousFailure,
                clearPeers: true);
            return;
        }

        if (currentRole != MultiplayerSessionRole.Host || string.IsNullOrWhiteSpace(removedPeerId))
            return;

        lock (_gate)
        {
            _peers.Remove(removedPeerId);
            _state = _connections.Count == 0 ? MultiplayerConnectionState.Hosting : MultiplayerConnectionState.Connected;
            _statusMessage = $"Peer '{removedPeerId}' disconnected.";
            _lastException = null;
        }

        QueuePeersChanged();
        QueueStatusChanged(MultiplayerSessionRole.Host, State, $"Peer '{removedPeerId}' disconnected.", null);
        await BroadcastEnvelopeAsync(
            CreateEnvelope(MultiplayerEnvelopeKind.PeerRemoved, LocalPeerId, new MultiplayerPeerRemovedPayload(removedPeerId)),
            exceptPeerId: removedPeerId,
            cancellationToken);
    }

    private async Task SendEnvelopeAccordingToRoleAsync(MultiplayerEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (Role)
        {
            case MultiplayerSessionRole.Host:
                await BroadcastEnvelopeAsync(envelope, exceptPeerId: null, cancellationToken);
                break;

            case MultiplayerSessionRole.Client:
                await GetRequiredHostConnection().SendAsync(envelope, cancellationToken);
                break;
        }
    }

    private async Task BroadcastEnvelopeAsync(MultiplayerEnvelope envelope, string? exceptPeerId, CancellationToken cancellationToken)
    {
        MultiplayerConnection[] recipients;
        lock (_gate)
        {
            recipients = _connections
                .Where(entry => !string.Equals(entry.Key, exceptPeerId, StringComparison.Ordinal))
                .Select(entry => entry.Value)
                .ToArray();
        }

        foreach (var connection in recipients)
            await connection.SendAsync(envelope, cancellationToken);
    }

    private MultiplayerConnection GetRequiredHostConnection()
    {
        lock (_gate)
        {
            return _hostConnection ?? throw new InvalidOperationException("No host connection is active.");
        }
    }

    private MultiplayerEnvelope CreateEnvelope<T>(string kind, string? senderId, T payload, string? channel = null, string? type = null)
    {
        return new MultiplayerEnvelope
        {
            Kind = kind,
            SenderId = senderId,
            Channel = channel,
            Type = type,
            Payload = JsonSerializer.SerializeToElement(payload, _serializerOptions)
        };
    }

    private MultiplayerPeerInfo[] SnapshotPeersForRemote()
    {
        lock (_gate)
        {
            return _peers.Values
                .Select(peer => peer with { IsLocal = peer.Id == _localPeerId })
                .ToArray();
        }
    }

    private MultiplayerPeerInfo GetPeerSnapshot(string peerId)
    {
        lock (_gate)
        {
            if (_peers.TryGetValue(peerId, out var peer))
                return peer;

            return new MultiplayerPeerInfo(peerId, peerId, false, false, "unknown");
        }
    }

    private void UpdateState(
        MultiplayerSessionRole role,
        MultiplayerConnectionState state,
        string? message,
        Exception? exception,
        bool clearPeers = false)
    {
        lock (_gate)
        {
            _role = role;
            _state = state;
            _statusMessage = message;
            _lastException = exception;
            if (clearPeers)
            {
                _peers = [];
                _hostConnection = null;
                _connections.Clear();
            }
        }

        QueuePeersChanged();
        QueueStatusChanged(role, state, message, exception);
    }

    private void QueuePeersChanged()
    {
        EnqueueDispatch(() => PeersChanged?.Invoke(this, EventArgs.Empty));
    }

    private void QueueStatusChanged(MultiplayerSessionRole role, MultiplayerConnectionState state, string? message, Exception? exception)
    {
        EnqueueDispatch(() => StatusChanged?.Invoke(this, new MultiplayerStatusChangedEventArgs(role, state, message, exception)));
    }

    private void EnqueueDispatch(Action dispatch)
    {
        lock (_gate)
            _pendingDispatches.Enqueue(dispatch);
    }

    private static string SanitizeDisplayName(string? displayName, string fallback)
    {
        var trimmed = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();
        return trimmed.Length <= 24 ? trimmed : trimmed[..24];
    }

    private static string[] EnumerateLocalIpv4Addresses()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(address => address, StringComparer.Ordinal)
                .ToArray();
        }
        catch (SocketException)
        {
            return ["127.0.0.1"];
        }
    }
}