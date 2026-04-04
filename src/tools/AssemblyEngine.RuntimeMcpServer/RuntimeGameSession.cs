using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using AssemblyEngine.Diagnostics;

namespace AssemblyEngine.RuntimeMcpServer;

internal sealed class RuntimeGameSession : IAsyncDisposable
{
    private const int MaxBufferedLogs = 2000;

    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _logSignal = new(0, int.MaxValue);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<RuntimeDiagnosticsMessage>> _pendingResponses = [];
    private readonly Process _process;
    private readonly NamedPipeServerStream _pipe;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TaskCompletionSource<bool> _bridgeConnectedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<SessionLogRecord> _logs = [];

    private readonly string _executablePath;
    private readonly string _workingDirectory;
    private readonly string? _arguments;

    private Task? _transportTask;
    private long _nextLogSequence;
    private long _nextRequestId;
    private bool _bridgeConnected;
    private int? _exitCode;
    private RuntimeSessionInfo? _sessionInfo;
    private RuntimeStateSnapshot? _lastState;
    private RuntimeCrashInfo? _lastCrash;
    private RuntimeShutdownInfo? _shutdownInfo;

    private RuntimeGameSession(
        Process process,
        NamedPipeServerStream pipe,
        string executablePath,
        string workingDirectory,
        string? arguments)
    {
        _process = process;
        _pipe = pipe;
        _executablePath = executablePath;
        _workingDirectory = workingDirectory;
        _arguments = arguments;
    }

    public static RuntimeGameSession Start(RuntimeLaunchOptions options)
    {
        string pipeName = $"assemblyengine-runtime-{Guid.NewGuid():N}";
        var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = options.ExecutablePath,
                Arguments = options.Arguments ?? string.Empty,
                WorkingDirectory = options.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };

        process.StartInfo.Environment[RuntimeDiagnosticsEnvironment.PipeNameVariable] = pipeName;

        var session = new RuntimeGameSession(process, pipe, options.ExecutablePath, options.WorkingDirectory, options.Arguments);
        session.Start();
        return session;
    }

    public RuntimeSessionStatus GetStatus()
    {
        lock (_stateGate)
        {
            string state = _process.HasExited
                ? "exited"
                : _bridgeConnected ? "running" : "starting";

            return new RuntimeSessionStatus(
                state,
                _bridgeConnected,
                _process.HasExited ? _sessionInfo?.ProcessId ?? _process.Id : _process.Id,
                _exitCode,
                _executablePath,
                _sessionInfo,
                _lastState,
                _lastCrash,
                _shutdownInfo,
                _logs.Count);
        }
    }

    public async Task<RuntimeSessionStatus> GetStatusAsync(bool refreshState, CancellationToken cancellationToken)
    {
        if (refreshState && _bridgeConnected)
        {
            var state = await RequestStateAsync(cancellationToken);
            lock (_stateGate)
            {
                _lastState = state;
            }
        }

        return GetStatus();
    }

    public async Task<bool> WaitForBridgeConnectionAsync(int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (_bridgeConnected)
            return true;

        if (timeoutMilliseconds <= 0)
            return _bridgeConnected;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMilliseconds);

        try
        {
            await _bridgeConnectedSource.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return _bridgeConnected;
    }

    public async Task<SessionLogBatch> WaitForLogsAsync(long afterSequence, int maxEntries, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        SessionLogBatch batch = GetLogsAfter(afterSequence, maxEntries);
        if (batch.Entries.Count > 0 || timeoutMilliseconds <= 0)
            return batch;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMilliseconds);

        try
        {
            await _logSignal.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return GetLogsAfter(afterSequence, maxEntries);
    }

    public Task<RuntimeScreenshot> CaptureScreenshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Window capture is only available on Windows.");

        return Task.FromResult(RuntimeWindowCapture.CaptureClientArea(_process));
    }

    public Task<RuntimeStateSnapshot> RequestStateAsync(CancellationToken cancellationToken)
    {
        return SendCommandAsync<object?, RuntimeStateSnapshot>(RuntimeDiagnosticsProtocol.GetStateCommand, null, cancellationToken);
    }

    public Task<RuntimeCommandAccepted> SendKeyAsync(RuntimeKeyInputCommand command, CancellationToken cancellationToken)
    {
        return SendCommandAsync<RuntimeKeyInputCommand, RuntimeCommandAccepted>(RuntimeDiagnosticsProtocol.KeyInputCommand, command, cancellationToken);
    }

    public Task<RuntimeCommandAccepted> MoveMouseAsync(RuntimeMouseMoveCommand command, CancellationToken cancellationToken)
    {
        return SendCommandAsync<RuntimeMouseMoveCommand, RuntimeCommandAccepted>(RuntimeDiagnosticsProtocol.MouseMoveCommand, command, cancellationToken);
    }

    public Task<RuntimeCommandAccepted> SendMouseButtonAsync(RuntimeMouseButtonInputCommand command, CancellationToken cancellationToken)
    {
        return SendCommandAsync<RuntimeMouseButtonInputCommand, RuntimeCommandAccepted>(RuntimeDiagnosticsProtocol.MouseButtonCommand, command, cancellationToken);
    }

    public async Task StopAsync(bool force, CancellationToken cancellationToken)
    {
        if (_process.HasExited)
            return;

        if (!force)
        {
            try
            {
                _process.CloseMainWindow();
            }
            catch
            {
            }

            using var gracefulTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            gracefulTimeout.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await _process.WaitForExitAsync(gracefulTimeout.Token);
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch
        {
        }

        foreach (var pending in _pendingResponses.Values)
            pending.TrySetCanceled();

        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        if (_transportTask is not null)
        {
            try
            {
                await _transportTask;
            }
            catch
            {
            }
        }

        _pipe.Dispose();
        _process.Dispose();
        _writeLock.Dispose();
        _logSignal.Dispose();
        _cancellationTokenSource.Dispose();
    }

    private void Start()
    {
        _process.Exited += OnProcessExited;
        _process.OutputDataReceived += OnProcessOutputDataReceived;
        _process.ErrorDataReceived += OnProcessErrorDataReceived;

        if (!_process.Start())
            throw new InvalidOperationException($"Failed to start '{_executablePath}'.");

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        AppendLog("server", "info", "process.launch", $"Launched process {_process.Id}.", _arguments);
        _transportTask = Task.Run(() => RunTransportAsync(_cancellationTokenSource.Token));
    }

    private async Task RunTransportAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _pipe.WaitForConnectionAsync(cancellationToken);

            lock (_stateGate)
            {
                _bridgeConnected = true;
            }

            _bridgeConnectedSource.TrySetResult(true);
            AppendLog("server", "info", "runtime.bridge", "Runtime diagnostics bridge connected.", null);

            while (!cancellationToken.IsCancellationRequested)
            {
                RuntimeDiagnosticsMessage? message = await RuntimeDiagnosticsPipe.ReadMessageAsync(_pipe, cancellationToken);
                if (message is null)
                    break;

                HandleIncomingMessage(message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (EndOfStreamException)
        {
        }
        catch (IOException ex)
        {
            AppendLog("server", "warning", "runtime.bridge", "Runtime diagnostics transport closed.", ex.Message);
        }
        finally
        {
            lock (_stateGate)
            {
                _bridgeConnected = false;
            }

            FailPendingResponses(new InvalidOperationException("Runtime diagnostics transport is no longer connected."));
        }
    }

    private void HandleIncomingMessage(RuntimeDiagnosticsMessage message)
    {
        if (string.Equals(message.Kind, RuntimeDiagnosticsProtocol.EventKind, StringComparison.Ordinal))
        {
            HandleEvent(message);
            return;
        }

        if (string.Equals(message.Kind, RuntimeDiagnosticsProtocol.ResponseKind, StringComparison.Ordinal) &&
            message.RequestId is long requestId &&
            _pendingResponses.TryRemove(requestId, out var response))
        {
            response.TrySetResult(message);
        }
    }

    private void HandleEvent(RuntimeDiagnosticsMessage message)
    {
        switch (message.Name)
        {
            case RuntimeDiagnosticsProtocol.SessionHelloEvent:
                lock (_stateGate)
                {
                    _sessionInfo = RuntimeDiagnosticsPipe.DeserializePayload<RuntimeSessionInfo>(message);
                }
                break;

            case RuntimeDiagnosticsProtocol.LogEvent:
                var runtimeLog = RuntimeDiagnosticsPipe.DeserializePayload<RuntimeLogEntry>(message);
                AppendLog("runtime", runtimeLog.Level.ToString(), runtimeLog.Category, runtimeLog.Message, runtimeLog.Detail, runtimeLog.TimestampUtc);
                break;

            case RuntimeDiagnosticsProtocol.EngineStartedEvent:
                lock (_stateGate)
                {
                    _lastState = RuntimeDiagnosticsPipe.DeserializePayload<RuntimeStateSnapshot>(message);
                }
                break;

            case RuntimeDiagnosticsProtocol.EngineStoppedEvent:
                lock (_stateGate)
                {
                    _shutdownInfo = RuntimeDiagnosticsPipe.DeserializePayload<RuntimeShutdownInfo>(message);
                }
                break;

            case RuntimeDiagnosticsProtocol.EngineCrashedEvent:
                lock (_stateGate)
                {
                    _lastCrash = RuntimeDiagnosticsPipe.DeserializePayload<RuntimeCrashInfo>(message);
                }
                break;
        }
    }

    private async Task<TResponse> SendCommandAsync<TPayload, TResponse>(string name, TPayload payload, CancellationToken cancellationToken)
    {
        EnsureBridgeConnected();

        long requestId = Interlocked.Increment(ref _nextRequestId);
        var responseSource = new TaskCompletionSource<RuntimeDiagnosticsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingResponses[requestId] = responseSource;

        RuntimeDiagnosticsMessage command = RuntimeDiagnosticsPipe.CreateCommand(name, requestId, payload);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await RuntimeDiagnosticsPipe.WriteMessageAsync(_pipe, command, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        using var registration = cancellationToken.Register(() => responseSource.TrySetCanceled(cancellationToken));
        RuntimeDiagnosticsMessage response = await responseSource.Task;
        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new InvalidOperationException(response.Error);

        if (typeof(TResponse) == typeof(RuntimeCommandAccepted) && response.Payload.ValueKind == JsonValueKind.Undefined)
            return (TResponse)(object)new RuntimeCommandAccepted(true, "Accepted.");

        return RuntimeDiagnosticsPipe.DeserializePayload<TResponse>(response);
    }

    private void EnsureBridgeConnected()
    {
        if (_process.HasExited)
            throw new InvalidOperationException("The game process has already exited.");

        if (!_bridgeConnected || !_pipe.IsConnected)
            throw new InvalidOperationException("The runtime diagnostics bridge is not connected yet.");
    }

    private void AppendLog(string source, string level, string category, string message, string? detail, DateTimeOffset? timestamp = null)
    {
        lock (_stateGate)
        {
            _logs.Add(new SessionLogRecord(
                Interlocked.Increment(ref _nextLogSequence),
                timestamp ?? DateTimeOffset.UtcNow,
                source,
                level,
                category,
                message,
                detail));

            if (_logs.Count > MaxBufferedLogs)
                _logs.RemoveRange(0, _logs.Count - MaxBufferedLogs);
        }

        if (_logSignal.CurrentCount == 0)
            _logSignal.Release();
    }

    private SessionLogBatch GetLogsAfter(long afterSequence, int maxEntries)
    {
        maxEntries = Math.Clamp(maxEntries, 1, 500);

        lock (_stateGate)
        {
            var entries = _logs.Where(log => log.Sequence > afterSequence)
                .OrderBy(log => log.Sequence)
                .Take(maxEntries)
                .ToArray();

            long nextSequence = entries.Length == 0 ? afterSequence : entries[^1].Sequence;
            return new SessionLogBatch(nextSequence, entries);
        }
    }

    private void FailPendingResponses(Exception exception)
    {
        foreach (var pendingResponse in _pendingResponses.ToArray())
        {
            if (_pendingResponses.TryRemove(pendingResponse.Key, out var response))
                response.TrySetException(exception);
        }
    }

    private void OnProcessExited(object? sender, EventArgs eventArgs)
    {
        lock (_stateGate)
        {
            _exitCode = _process.ExitCode;
        }

        AppendLog("server", "info", "process.exit", $"Process exited with code {_process.ExitCode}.", null);
    }

    private void OnProcessOutputDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            AppendLog("stdout", "info", "process.stdout", eventArgs.Data, null);
    }

    private void OnProcessErrorDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            AppendLog("stderr", "error", "process.stderr", eventArgs.Data, null);
    }
}