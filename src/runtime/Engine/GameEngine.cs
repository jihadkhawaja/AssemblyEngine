using AssemblyEngine.Core;
using AssemblyEngine.Interop;
using AssemblyEngine.Platform;
using AssemblyEngine.Scripting;
using AssemblyEngine.UI;

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

    public int Width { get; private set; }
    public int Height { get; private set; }
    public string Title { get; private set; }
    public WindowMode WindowMode { get; private set; } = WindowMode.Windowed;
    public Color ClearColor { get; set; } = Color.CornflowerBlue;
    public SceneManager Scenes { get; } = new();
    public ScriptManager Scripts { get; }
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
            if (_initialized)
                NativeCore.SetVSyncEnabled(value ? 1 : 0);
        }
    }

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
    }

    public void Initialize()
    {
        if (_initialized) return;

        int result;
        try
        {
            result = NativeCore.Init(Width, Height, Title);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            throw EnginePlatform.Current.CreateNativeCoreLoadException(ex);
        }

        if (result == 0)
            throw new InvalidOperationException("Failed to initialize native engine core.");

        NativeCore.SetVSyncEnabled(VSyncEnabled ? 1 : 0);
        if (NativeCore.SetWindowMode((int)WindowMode) == 0)
            throw new InvalidOperationException($"Failed to apply window mode '{WindowMode}'.");

        SyncWindowState();

        _initialized = true;
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
        return true;
    }

    public void Run()
    {
        if (!_initialized)
            Initialize();

        Scenes.ProcessTransition();
        Scripts.LoadAll();

        while (NativeCore.PollEvents() != 0)
        {
            SyncWindowState();
            float dt = Time.DeltaTime;

            // Update phase
            Scenes.Update(dt);
            Scripts.UpdateAll(dt);

            // Draw phase
            Graphics.Clear(ClearColor);
            Scenes.Draw();
            Scripts.DrawAll();

            // Draw UI overlay
            UI?.Render(Width, Height);

            NativeCore.Present();
        }

        Scripts.UnloadAll();
        NativeCore.Shutdown();
        _initialized = false;
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
