namespace AssemblyEngine.Diagnostics;

internal static class RuntimeDiagnosticsEnvironment
{
    public const string PipeNameVariable = "ASSEMBLYENGINE_RUNTIME_PIPE";
    public const int ConnectTimeoutMilliseconds = 2000;

    public static string? GetPipeName()
    {
        var pipeName = Environment.GetEnvironmentVariable(PipeNameVariable);
        return string.IsNullOrWhiteSpace(pipeName) ? null : pipeName;
    }
}