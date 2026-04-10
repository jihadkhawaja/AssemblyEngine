using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssemblyEngine.Networking;

public enum MultiplayerSessionRole
{
    Offline,
    Host,
    Client
}

public enum MultiplayerTransportMode
{
    PeerToPeer,
    Localhost
}

public enum MultiplayerConnectionState
{
    Disconnected,
    Hosting,
    Connecting,
    Connected,
    Error
}

public sealed record MultiplayerHostOptions(
    int Port,
    string DisplayName,
    MultiplayerTransportMode TransportMode = MultiplayerTransportMode.PeerToPeer,
    int MaxConnections = 4);

public sealed record MultiplayerJoinOptions(
    string Address,
    int Port,
    string DisplayName,
    MultiplayerTransportMode TransportMode = MultiplayerTransportMode.PeerToPeer);

public sealed record MultiplayerPeerInfo(
    string Id,
    string DisplayName,
    bool IsLocal,
    bool IsReady,
    string EndPoint);

public sealed class MultiplayerMessageEventArgs : EventArgs
{
    public MultiplayerMessageEventArgs(MultiplayerPeerInfo peer, string channel, string type, JsonElement payload)
    {
        Peer = peer;
        Channel = channel;
        Type = type;
        Payload = payload;
    }

    public MultiplayerPeerInfo Peer { get; }

    public string Channel { get; }

    public string Type { get; }

    public JsonElement Payload { get; }

    public T DeserializePayload<T>()
    {
        return Payload.Deserialize<T>(MultiplayerSerialization.Options)
            ?? throw new InvalidOperationException($"Failed to deserialize multiplayer payload to '{typeof(T).Name}'.");
    }
}

public sealed class MultiplayerGameStartedEventArgs : EventArgs
{
    public MultiplayerGameStartedEventArgs(MultiplayerPeerInfo initiator, JsonElement payload)
    {
        Initiator = initiator;
        Payload = payload;
    }

    public MultiplayerPeerInfo Initiator { get; }

    public JsonElement Payload { get; }

    public T DeserializePayload<T>()
    {
        return Payload.Deserialize<T>(MultiplayerSerialization.Options)
            ?? throw new InvalidOperationException($"Failed to deserialize multiplayer start payload to '{typeof(T).Name}'.");
    }
}

internal static class MultiplayerSerialization
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public sealed class MultiplayerStatusChangedEventArgs : EventArgs
{
    public MultiplayerStatusChangedEventArgs(
        MultiplayerSessionRole role,
        MultiplayerConnectionState state,
        string? message,
        Exception? exception)
    {
        Role = role;
        State = state;
        Message = message;
        Exception = exception;
    }

    public MultiplayerSessionRole Role { get; }

    public MultiplayerConnectionState State { get; }

    public string? Message { get; }

    public Exception? Exception { get; }
}

internal static class MultiplayerEnvelopeKind
{
    public const string Hello = "hello";
    public const string Welcome = "welcome";
    public const string PeerUpdated = "peerUpdated";
    public const string PeerRemoved = "peerRemoved";
    public const string AppMessage = "app";
    public const string GameStart = "gameStart";
    public const string Error = "error";
}

internal sealed class MultiplayerEnvelope
{
    public string Kind { get; set; } = string.Empty;

    public string? SenderId { get; set; }

    public string? Channel { get; set; }

    public string? Type { get; set; }

    public JsonElement Payload { get; set; }
}

internal sealed record MultiplayerHelloPayload(string PeerId, string DisplayName, bool IsReady);

internal sealed record MultiplayerWelcomePayload(string SessionId, string HostPeerId, MultiplayerPeerInfo[] Peers);

internal sealed record MultiplayerPeerRemovedPayload(string PeerId);

internal sealed record MultiplayerErrorPayload(string Message);