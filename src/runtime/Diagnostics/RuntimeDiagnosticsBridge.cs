using AssemblyEngine.Core;
using AssemblyEngine.Engine;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Channels;

namespace AssemblyEngine.Diagnostics;

internal sealed class RuntimeDiagnosticsBridge
{
    public static RuntimeDiagnosticsBridge Current { get; } = new();

    private readonly object _lifecycleGate = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentQueue<RuntimeDiagnosticsMessage> _pendingCommands = [];
    private readonly InjectedInputController _inputController = new();
    private readonly List<long> _pendingScreenshotRequests = [];

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _cancellationTokenSource;
    private Channel<RuntimeDiagnosticsMessage>? _outboundChannel;
    private Task? _readLoop;
    private Task? _writeLoop;
    private GameEngine? _engine;
    private long _logSequence;
    private bool _enabled;
    private bool _fatalCrashReported;
    private bool _processHandlersRegistered;

    public void Attach(GameEngine engine)
    {
        string? pipeName = RuntimeDiagnosticsEnvironment.GetPipeName();
        if (pipeName is null)
            return;

        lock (_lifecycleGate)
        {
            if (_enabled && ReferenceEquals(_engine, engine))
                return;

            StopLocked();

            var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            try
            {
                pipe.Connect(RuntimeDiagnosticsEnvironment.ConnectTimeoutMilliseconds);
            }
            catch
            {
                pipe.Dispose();
                return;
            }

            _engine = engine;
            _pipe = pipe;
            _cancellationTokenSource = new CancellationTokenSource();
            _outboundChannel = RuntimeDiagnosticsPipe.CreateOutboundChannel();
            _enabled = true;
            _fatalCrashReported = false;
            _inputController.Reset();
            ClearPendingCommands();
            RegisterProcessHandlers();

            _readLoop = Task.Run(() => ReadLoopAsync(pipe, _cancellationTokenSource.Token));
            _writeLoop = Task.Run(() => WriteLoopAsync(pipe, _outboundChannel.Reader, _cancellationTokenSource.Token));
        }

        EnqueueEvent(RuntimeDiagnosticsProtocol.SessionHelloEvent, BuildSessionInfo(engine));
        LogInfo("engine.diagnostics", $"Connected runtime diagnostics pipe '{pipeName}'.");
    }

    public void Detach(GameEngine engine, Exception? failure = null)
    {
        if (!_enabled || !ReferenceEquals(_engine, engine))
            return;

        if (failure is not null && !_fatalCrashReported)
            ReportFatalException("engine.run", failure);

        SendImmediateEvent(RuntimeDiagnosticsProtocol.EngineStoppedEvent, new RuntimeShutdownInfo(
            DateTimeOffset.UtcNow,
            failure is null,
            failure?.ToString()));

        lock (_lifecycleGate)
        {
            StopLocked();
        }
    }

    public void ProcessFrameStart(GameEngine engine)
    {
        if (!IsActiveFor(engine))
            return;

        while (_pendingCommands.TryDequeue(out var command))
            HandleCommand(engine, command);

        _inputController.ApplyPending();
    }

    public void ProcessFrameEnd(GameEngine engine)
    {
        if (!IsActiveFor(engine))
            return;

        long[] screenshotRequests;
        lock (_pendingScreenshotRequests)
        {
            if (_pendingScreenshotRequests.Count == 0)
                return;

            screenshotRequests = _pendingScreenshotRequests.ToArray();
            _pendingScreenshotRequests.Clear();
        }

        try
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Framebuffer capture is only available on Windows.");

            RuntimeScreenshot screenshot = RuntimeFrameCapture.CapturePng();
            foreach (long requestId in screenshotRequests)
                EnqueueResponse(RuntimeDiagnosticsProtocol.CaptureScreenshotCommand, requestId, screenshot);
        }
        catch (Exception ex)
        {
            LogError("engine.screenshot", ex);
            foreach (long requestId in screenshotRequests)
                EnqueueErrorResponse(RuntimeDiagnosticsProtocol.CaptureScreenshotCommand, requestId, ex.Message);
        }
    }

    public void NotifyEngineStarted(GameEngine engine)
    {
        if (!IsActiveFor(engine))
            return;

        EnqueueEvent(RuntimeDiagnosticsProtocol.EngineStartedEvent, BuildStateSnapshot(engine));
        LogInfo("engine.initialize", $"Initialized '{engine.Title}' ({engine.Width}x{engine.Height}).");
    }

    public void NotifySceneChanged(string? previousScene, string currentScene, int entityCount)
    {
        if (!_enabled)
            return;

        EnqueueEvent(RuntimeDiagnosticsProtocol.SceneChangedEvent, new RuntimeSceneChangeInfo(
            DateTimeOffset.UtcNow,
            previousScene,
            currentScene,
            entityCount));

        string message = previousScene is null
            ? $"Loaded scene '{currentScene}'."
            : $"Scene changed from '{previousScene}' to '{currentScene}'.";

        LogInfo("engine.scene", message, $"Entity count: {entityCount}");
    }

    public void LogInfo(string category, string message, string? detail = null) => Log(RuntimeDiagnosticsLevel.Info, category, message, detail);
    public void LogWarning(string category, string message, string? detail = null) => Log(RuntimeDiagnosticsLevel.Warning, category, message, detail);

    public void LogError(string category, Exception exception)
    {
        Log(RuntimeDiagnosticsLevel.Error, category, exception.Message, exception.ToString());
    }

    public void ReportFatalException(string category, Exception exception)
    {
        LogError(category, exception);
        if (!_enabled || _fatalCrashReported)
            return;

        _fatalCrashReported = true;
        SendImmediateEvent(RuntimeDiagnosticsProtocol.EngineCrashedEvent, new RuntimeCrashInfo(
            DateTimeOffset.UtcNow,
            category,
            exception.Message,
            exception.ToString()));
    }

    private void Log(RuntimeDiagnosticsLevel level, string category, string message, string? detail)
    {
        if (!_enabled)
            return;

        long sequence = Interlocked.Increment(ref _logSequence);
        EnqueueEvent(RuntimeDiagnosticsProtocol.LogEvent, new RuntimeLogEntry(
            sequence,
            DateTimeOffset.UtcNow,
            level,
            category,
            message,
            detail));
    }

    private void HandleCommand(GameEngine engine, RuntimeDiagnosticsMessage command)
    {
        if (command.RequestId is not long requestId)
            return;

        try
        {
            switch (command.Name)
            {
                case RuntimeDiagnosticsProtocol.GetStateCommand:
                    EnqueueResponse(RuntimeDiagnosticsProtocol.GetStateCommand, requestId, BuildStateSnapshot(engine));
                    break;

                case RuntimeDiagnosticsProtocol.CaptureScreenshotCommand:
                    lock (_pendingScreenshotRequests)
                    {
                        _pendingScreenshotRequests.Add(requestId);
                    }
                    break;

                case RuntimeDiagnosticsProtocol.KeyInputCommand:
                    _inputController.QueueKey(RuntimeDiagnosticsPipe.DeserializePayload<RuntimeKeyInputCommand>(command));
                    EnqueueResponse(RuntimeDiagnosticsProtocol.KeyInputCommand, requestId, new RuntimeCommandAccepted(true, "Queued keyboard input."));
                    break;

                case RuntimeDiagnosticsProtocol.MouseMoveCommand:
                    _inputController.QueueMouseMove(RuntimeDiagnosticsPipe.DeserializePayload<RuntimeMouseMoveCommand>(command));
                    EnqueueResponse(RuntimeDiagnosticsProtocol.MouseMoveCommand, requestId, new RuntimeCommandAccepted(true, "Queued mouse movement."));
                    break;

                case RuntimeDiagnosticsProtocol.MouseButtonCommand:
                    _inputController.QueueMouseButton(RuntimeDiagnosticsPipe.DeserializePayload<RuntimeMouseButtonInputCommand>(command));
                    EnqueueResponse(RuntimeDiagnosticsProtocol.MouseButtonCommand, requestId, new RuntimeCommandAccepted(true, "Queued mouse button input."));
                    break;

                default:
                    EnqueueErrorResponse(command.Name, requestId, $"Unknown diagnostics command '{command.Name}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError("engine.diagnostics", ex);
            EnqueueErrorResponse(command.Name, requestId, ex.Message);
        }
    }

    private RuntimeSessionInfo BuildSessionInfo(GameEngine engine)
    {
        var process = Process.GetCurrentProcess();
        string assemblyVersion = typeof(GameEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        return new RuntimeSessionInfo(
            DateTimeOffset.UtcNow,
            process.Id,
            process.ProcessName,
            AppContext.BaseDirectory,
            assemblyVersion,
            engine.Title,
            engine.Width,
            engine.Height);
    }

    private RuntimeStateSnapshot BuildStateSnapshot(GameEngine engine)
    {
        bool initialized = engine.IsInitialized;
        int mouseX = initialized ? InputSystem.MouseX : 0;
        int mouseY = initialized ? InputSystem.MouseY : 0;
        int fps = initialized ? Time.Fps : 0;
        float uiScale = engine.UiScale;
        float deltaTime = initialized ? Time.DeltaTime : 0f;

        if (fps < 0)
            fps = 0;

        if (!float.IsFinite(uiScale))
            uiScale = 1f;

        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            deltaTime = 0f;

        return new RuntimeStateSnapshot(
            DateTimeOffset.UtcNow,
            initialized,
            engine.Title,
            engine.Width,
            engine.Height,
            engine.WindowMode,
            uiScale,
            engine.VSyncEnabled,
            ToHex(engine.ClearColor),
            engine.Scenes.ActiveScene?.Name,
            engine.Scenes.RegisteredSceneCount,
            engine.Scenes.ActiveScene?.Entities.Count ?? 0,
            engine.Scripts.Scripts.Count,
            mouseX,
            mouseY,
            fps,
            deltaTime);
    }

    private void EnqueueEvent<TPayload>(string name, TPayload payload)
    {
        _outboundChannel?.Writer.TryWrite(RuntimeDiagnosticsPipe.CreateEvent(name, payload));
    }

    private void EnqueueResponse<TPayload>(string name, long requestId, TPayload payload)
    {
        _outboundChannel?.Writer.TryWrite(RuntimeDiagnosticsPipe.CreateResponse(name, requestId, payload));
    }

    private void EnqueueErrorResponse(string name, long requestId, string error)
    {
        _outboundChannel?.Writer.TryWrite(RuntimeDiagnosticsPipe.CreateErrorResponse(name, requestId, error));
    }

    private void SendImmediateEvent<TPayload>(string name, TPayload payload)
    {
        RuntimeDiagnosticsMessage message = RuntimeDiagnosticsPipe.CreateEvent(name, payload);
        NamedPipeClientStream? pipe;

        lock (_lifecycleGate)
        {
            pipe = _pipe;
        }

        if (pipe is null || !pipe.IsConnected)
            return;

        _writeLock.Wait();
        try
        {
            RuntimeDiagnosticsPipe.WriteMessageAsync(pipe, message, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(NamedPipeClientStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RuntimeDiagnosticsMessage? message = await RuntimeDiagnosticsPipe.ReadMessageAsync(pipe, cancellationToken);
                if (message is null)
                    break;

                if (string.Equals(message.Kind, RuntimeDiagnosticsProtocol.CommandKind, StringComparison.Ordinal))
                    _pendingCommands.Enqueue(message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (EndOfStreamException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            lock (_lifecycleGate)
            {
                StopLocked();
            }
        }
    }

    private async Task WriteLoopAsync(NamedPipeClientStream pipe, ChannelReader<RuntimeDiagnosticsMessage> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (RuntimeDiagnosticsMessage message in reader.ReadAllAsync(cancellationToken))
            {
                await _writeLock.WaitAsync(cancellationToken);
                try
                {
                    await RuntimeDiagnosticsPipe.WriteMessageAsync(pipe, message, cancellationToken);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
    }

    private bool IsActiveFor(GameEngine engine)
    {
        return _enabled && ReferenceEquals(_engine, engine);
    }

    private void RegisterProcessHandlers()
    {
        if (_processHandlersRegistered)
            return;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _processHandlersRegistered = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
        {
            ReportFatalException("process.unhandled", exception);
            return;
        }

        if (eventArgs.ExceptionObject is not null)
            ReportFatalException("process.unhandled", new InvalidOperationException(eventArgs.ExceptionObject.ToString()));
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        ReportFatalException("process.unobservedTask", eventArgs.Exception);
    }

    private void StopLocked()
    {
        _enabled = false;
        _fatalCrashReported = false;
        _engine = null;

        _cancellationTokenSource?.Cancel();
        _outboundChannel?.Writer.TryComplete();

        try
        {
            _pipe?.Dispose();
        }
        catch
        {
        }

        _pipe = null;
        _cancellationTokenSource = null;
        _outboundChannel = null;
        _readLoop = null;
        _writeLoop = null;

        _inputController.Reset();
        ClearPendingCommands();

        lock (_pendingScreenshotRequests)
        {
            _pendingScreenshotRequests.Clear();
        }
    }

    private void ClearPendingCommands()
    {
        while (_pendingCommands.TryDequeue(out _))
        {
        }
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }
}