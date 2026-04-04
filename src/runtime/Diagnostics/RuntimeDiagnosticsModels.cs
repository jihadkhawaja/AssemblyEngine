using System.Text.Json;
using AssemblyEngine.Core;

namespace AssemblyEngine.Diagnostics;

internal enum RuntimeDiagnosticsLevel
{
    Trace,
    Info,
    Warning,
    Error,
    Critical,
}

internal enum RuntimeInputAction
{
    Down,
    Up,
    Tap,
}

internal sealed record RuntimeSessionInfo(
    DateTimeOffset TimestampUtc,
    int ProcessId,
    string ProcessName,
    string BaseDirectory,
    string AssemblyVersion,
    string Title,
    int Width,
    int Height);

internal sealed record RuntimeLogEntry(
    long Sequence,
    DateTimeOffset TimestampUtc,
    RuntimeDiagnosticsLevel Level,
    string Category,
    string Message,
    string? Detail);

internal sealed record RuntimeSceneChangeInfo(
    DateTimeOffset TimestampUtc,
    string? PreviousScene,
    string CurrentScene,
    int EntityCount);

internal sealed record RuntimeCrashInfo(
    DateTimeOffset TimestampUtc,
    string Category,
    string Message,
    string Detail);

internal sealed record RuntimeStateSnapshot(
    DateTimeOffset TimestampUtc,
    bool Initialized,
    string Title,
    int Width,
    int Height,
    WindowMode WindowMode,
    float UiScale,
    bool VSyncEnabled,
    string ClearColor,
    string? ActiveScene,
    int RegisteredSceneCount,
    int ActiveEntityCount,
    int ScriptCount,
    int MouseX,
    int MouseY,
    int Fps,
    float DeltaTime);

internal sealed record RuntimeShutdownInfo(
    DateTimeOffset TimestampUtc,
    bool CleanExit,
    string? Detail);

internal sealed record RuntimeScreenshot(
    byte[] ImageData,
    string MimeType,
    int Width,
    int Height);

internal sealed record RuntimeCommandAccepted(
    bool Accepted,
    string Message);

internal sealed record RuntimeKeyInputCommand(
    KeyCode Key,
    RuntimeInputAction Action,
    int HoldFrames = 1);

internal sealed record RuntimeMouseMoveCommand(
    int X,
    int Y);

internal sealed record RuntimeMouseButtonInputCommand(
    MouseButton Button,
    RuntimeInputAction Action,
    int HoldFrames = 1,
    int? X = null,
    int? Y = null);

internal sealed class RuntimeDiagnosticsMessage
{
    public required string Kind { get; init; }
    public required string Name { get; init; }
    public long? RequestId { get; init; }
    public string? Error { get; init; }
    public JsonElement Payload { get; init; }
}

internal static class RuntimeDiagnosticsProtocol
{
    public const string EventKind = "event";
    public const string CommandKind = "command";
    public const string ResponseKind = "response";

    public const string SessionHelloEvent = "session.hello";
    public const string LogEvent = "log.appended";
    public const string EngineStartedEvent = "engine.started";
    public const string EngineStoppedEvent = "engine.stopped";
    public const string SceneChangedEvent = "scene.changed";
    public const string EngineCrashedEvent = "engine.crashed";

    public const string GetStateCommand = "state.get";
    public const string CaptureScreenshotCommand = "screenshot.capture";
    public const string KeyInputCommand = "input.key";
    public const string MouseMoveCommand = "input.mouse.move";
    public const string MouseButtonCommand = "input.mouse.button";
}