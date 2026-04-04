using AssemblyEngine.Diagnostics;
using System.Text.Json;

namespace AssemblyEngine.RuntimeMcpServer;

internal sealed record RuntimeLaunchOptions(
    string ExecutablePath,
    string? Arguments,
    string WorkingDirectory);

internal sealed record SessionLogRecord(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Source,
    string Level,
    string Category,
    string Message,
    string? Detail);

internal sealed record SessionLogBatch(
    long NextSequence,
    IReadOnlyList<SessionLogRecord> Entries);

internal sealed record RuntimeSessionStatus(
    string State,
    bool BridgeConnected,
    int? ProcessId,
    int? ExitCode,
    string? ExecutablePath,
    RuntimeSessionInfo? Runtime,
    RuntimeStateSnapshot? LastState,
    RuntimeCrashInfo? LastCrash,
    RuntimeShutdownInfo? ShutdownInfo,
    int BufferedLogCount)
{
    public static RuntimeSessionStatus Idle { get; } = new(
        "idle",
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        0);
}

internal static class JsonText
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string Serialize<TValue>(TValue value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }
}