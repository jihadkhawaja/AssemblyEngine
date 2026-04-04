using AssemblyEngine.Diagnostics;

namespace AssemblyEngine.RuntimeMcpServer;

internal sealed class RuntimeGameSessionManager : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RuntimeGameSession? _activeSession;

    public async Task<RuntimeSessionStatus> LaunchAsync(
        string executablePath,
        string? arguments,
        string? workingDirectory,
        int waitForBridgeMilliseconds,
        CancellationToken cancellationToken)
    {
        string resolvedExecutablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(resolvedExecutablePath))
            throw new FileNotFoundException("The target executable was not found.", resolvedExecutablePath);

        string resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetDirectoryName(resolvedExecutablePath) ?? Environment.CurrentDirectory
            : Path.GetFullPath(workingDirectory);

        var launchOptions = new RuntimeLaunchOptions(resolvedExecutablePath, arguments, resolvedWorkingDirectory);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activeSession is not null)
            {
                await _activeSession.StopAsync(force: true, cancellationToken);
                await _activeSession.DisposeAsync();
            }

            _activeSession = RuntimeGameSession.Start(launchOptions);
        }
        finally
        {
            _gate.Release();
        }

        if (_activeSession is not null && waitForBridgeMilliseconds > 0)
            await _activeSession.WaitForBridgeConnectionAsync(waitForBridgeMilliseconds, cancellationToken);

        return _activeSession?.GetStatus() ?? RuntimeSessionStatus.Idle;
    }

    public RuntimeSessionStatus GetStatus()
    {
        return _activeSession?.GetStatus() ?? RuntimeSessionStatus.Idle;
    }

    public async Task<RuntimeSessionStatus> GetStatusAsync(bool refreshState, CancellationToken cancellationToken)
    {
        if (_activeSession is null)
            return RuntimeSessionStatus.Idle;

        return await _activeSession.GetStatusAsync(refreshState, cancellationToken);
    }

    public async Task<SessionLogBatch> WaitForLogsAsync(long afterSequence, int maxEntries, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (_activeSession is null)
            return new SessionLogBatch(afterSequence, []);

        return await _activeSession.WaitForLogsAsync(afterSequence, maxEntries, timeoutMilliseconds, cancellationToken);
    }

    public async Task<RuntimeScreenshot> CaptureScreenshotAsync(CancellationToken cancellationToken)
    {
        return await GetRequiredSession().CaptureScreenshotAsync(cancellationToken);
    }

    public async Task<RuntimeCommandAccepted> SendKeyAsync(RuntimeKeyInputCommand command, CancellationToken cancellationToken)
    {
        return await GetRequiredSession().SendKeyAsync(command, cancellationToken);
    }

    public async Task<RuntimeCommandAccepted> MoveMouseAsync(RuntimeMouseMoveCommand command, CancellationToken cancellationToken)
    {
        return await GetRequiredSession().MoveMouseAsync(command, cancellationToken);
    }

    public async Task<RuntimeCommandAccepted> SendMouseButtonAsync(RuntimeMouseButtonInputCommand command, CancellationToken cancellationToken)
    {
        return await GetRequiredSession().SendMouseButtonAsync(command, cancellationToken);
    }

    public async Task<RuntimeSessionStatus> StopAsync(bool force, CancellationToken cancellationToken)
    {
        if (_activeSession is null)
            return RuntimeSessionStatus.Idle;

        await _activeSession.StopAsync(force, cancellationToken);
        return _activeSession.GetStatus();
    }

    public async ValueTask DisposeAsync()
    {
        if (_activeSession is not null)
            await _activeSession.DisposeAsync();

        _gate.Dispose();
    }

    private RuntimeGameSession GetRequiredSession()
    {
        return _activeSession ?? throw new InvalidOperationException("No active game session is currently running.");
    }
}