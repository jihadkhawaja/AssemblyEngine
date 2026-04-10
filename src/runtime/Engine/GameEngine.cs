using AssemblyEngine.Core;
using AssemblyEngine.Diagnostics;
using AssemblyEngine.Interop;
using AssemblyEngine.Networking;
using AssemblyEngine.Platform;
using AssemblyEngine.Scripting;
using AssemblyEngine.UI;
using System.Runtime.ExceptionServices;

namespace AssemblyEngine.Engine;

/// <summary>
/// Main engine class. Initializes the native core, runs the game loop,
/// and coordinates scenes, scripts, and UI.
/// </summary>
public sealed class GameEngine
{
    private bool _initialized;
    private float _uiScale = 1f;
    private bool _vSyncEnabled = true;
    private GraphicsBackend _presentationBackend = GraphicsBackend.Software;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public string Title { get; private set; }
    public WindowMode WindowMode { get; private set; } = WindowMode.Windowed;
    public Color ClearColor { get; set; } = Color.CornflowerBlue;
    public GraphicsBackend PresentationBackend
    {
        get => _presentationBackend;
        set
        {
            _presentationBackend = value;
            Graphics.SetPreferredBackend(value);
        }
    }

    public SceneManager Scenes { get; } = new();
    public ScriptManager Scripts { get; }
    public MultiplayerManager Multiplayer { get; } = new();
    public UIDocument? UI { get; private set; }

    public float UiScale
    {
        get => _uiScale;
        set
        {
            _uiScale = Math.Clamp(value, 0.75f, 2f);
            if (UI is not null)
                UI.RenderScale = _uiScale;
        }
    }

    public bool VSyncEnabled
    {
        get => _vSyncEnabled;
        set
        {
            _vSyncEnabled = value;
            Graphics.SetVSyncEnabled(value);
            if (_initialized)
                NativeCore.SetVSyncEnabled(value ? 1 : 0);
        }
    }

    internal bool IsInitialized => _initialized;

    public GameEngine(int width = 800, int height = 600, string title = "AssemblyEngine")
    {
        Width = width;
        Height = height;
        Title = title;
        Scripts = new ScriptManager(this);
    }

    public void LoadUI(string htmlPath, string? cssPath = null)
    {
        var html = File.ReadAllText(htmlPath);
        var css = cssPath is not null ? File.ReadAllText(cssPath) : null;
        UI = UIDocument.Parse(html, css);
        UI.RenderScale = UiScale;
        RuntimeDiagnosticsBridge.Current.LogInfo("engine.ui", $"Loaded UI from '{Path.GetFileName(htmlPath)}'.", cssPath is null ? htmlPath : $"{htmlPath} | {cssPath}");
    }

    public void Initialize()
    {
        if (_initialized) return;

        RuntimeDiagnosticsBridge.Current.Attach(this);
        RuntimeDiagnosticsBridge.Current.LogInfo("engine.initialize", $"Initializing '{Title}'.");

        int result;
        try
        {
            result = NativeCore.Init(Width, Height, Title);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            RuntimeDiagnosticsBridge.Current.LogError("engine.initialize", ex);
            throw EnginePlatform.Current.CreateNativeCoreLoadException(ex);
        }

        if (result == 0)
        {
            var exception = new InvalidOperationException("Failed to initialize native engine core.");
            RuntimeDiagnosticsBridge.Current.LogError("engine.initialize", exception);
            throw exception;
        }

        NativeCore.SetVSyncEnabled(VSyncEnabled ? 1 : 0);
        Graphics.SetPreferredBackend(PresentationBackend);
        Graphics.SetVSyncEnabled(VSyncEnabled);
        var desiredWindowMode = WindowMode;
        SyncWindowState();

        if (WindowMode != desiredWindowMode)
        {
            if (NativeCore.SetWindowMode((int)desiredWindowMode) == 0)
                throw new InvalidOperationException($"Failed to apply window mode '{desiredWindowMode}'.");
            SyncWindowState();
        }

        SyncWindowState();

        _initialized = true;
        RuntimeDiagnosticsBridge.Current.NotifyEngineStarted(this);
    }

    public bool Resize(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        if (!_initialized)
        {
            Width = width;
            Height = height;
            return true;
        }

        if (NativeCore.ResizeWindow(width, height) == 0)
            return false;

        SyncWindowState();
        RuntimeDiagnosticsBridge.Current.LogInfo("engine.window", $"Resized window to {Width}x{Height}.");
        return true;
    }

    public bool SetWindowMode(WindowMode windowMode)
    {
        if (!Enum.IsDefined(windowMode))
            throw new ArgumentOutOfRangeException(nameof(windowMode), windowMode, "Unknown window mode.");

        if (!_initialized)
        {
            WindowMode = windowMode;
            return true;
        }

        if (NativeCore.SetWindowMode((int)windowMode) == 0)
            return false;

        SyncWindowState();
        RuntimeDiagnosticsBridge.Current.LogInfo("engine.window", $"Changed window mode to '{WindowMode}'.");
        return true;
    }

    public void Run()
    {
        if (!_initialized)
            Initialize();

        Exception? failure = null;
        Exception? cleanupFailure = null;

        try
        {
            Scenes.ProcessTransition();
            Scripts.LoadAll();
            RuntimeDiagnosticsBridge.Current.LogInfo("engine.run", "Game loop started.");

            while (NativeCore.PollEvents() != 0)
            {
                SyncWindowState();
                RuntimeDiagnosticsBridge.Current.ProcessFrameStart(this);
                Multiplayer.Pump();
                float dt = Time.DeltaTime;

                Scenes.Update(dt);
                Scripts.UpdateAll(dt);

                Graphics.BeginFrame(Width, Height);
                Graphics.Clear(ClearColor);
                Scenes.Draw();
                Scripts.DrawAll();

                UI?.Render(Width, Height);
                Graphics.EndFrame();
                RuntimeDiagnosticsBridge.Current.ProcessFrameEnd(this);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
            RuntimeDiagnosticsBridge.Current.ReportFatalException("engine.run", ex);
        }
        finally
        {
            try
            {
                Scripts.UnloadAll();
            }
            catch (Exception ex)
            {
                cleanupFailure ??= ex;
                RuntimeDiagnosticsBridge.Current.LogError("engine.shutdown", ex);
            }

            if (_initialized)
            {
                try
                {
                    Multiplayer.StopAsync().GetAwaiter().GetResult();
                    Graphics.Shutdown();
                    NativeCore.Shutdown();
                }
                catch (Exception ex)
                {
                    cleanupFailure ??= ex;
                    RuntimeDiagnosticsBridge.Current.LogError("engine.shutdown", ex);
                }
            }

            _initialized = false;
            RuntimeDiagnosticsBridge.Current.Detach(this, failure ?? cleanupFailure);
        }

        if (failure is null && cleanupFailure is not null)
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Quit()
    {
        // Signal the engine to stop (sets running = 0 via a fake key event)
        // The next PollEvents call will return 0
    }

    private void SyncWindowState()
    {
        Width = NativeCore.GetWindowWidth();
        Height = NativeCore.GetWindowHeight();
        WindowMode = FromNativeWindowMode(NativeCore.GetWindowMode());
    }

    private static WindowMode FromNativeWindowMode(int windowMode)
    {
        return Enum.IsDefined(typeof(WindowMode), windowMode)
            ? (WindowMode)windowMode
            : WindowMode.Windowed;
    }
}
