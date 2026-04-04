using AssemblyEngine.Core;
using AssemblyEngine.Scripting;

namespace SampleGame;

/// <summary>
/// Provides a lightweight in-game settings panel for display-related sample options.
/// </summary>
public sealed class SettingsMenuScript : GameScript
{
    private static readonly WindowMode[] WindowModeOptions =
    [
        WindowMode.Windowed,
        WindowMode.MaximizedWindow,
        WindowMode.BorderlessFullscreen
    ];

    private static readonly ResolutionOption[] ResolutionOptions =
    [
        new(800, 600),
        new(1024, 768),
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080)
    ];

    private static readonly float[] UiScaleOptions = [0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f];

    private readonly SampleSettings _settings;
    private readonly string _settingsPath;
    private int _selectedRow;
    private int _windowModeIndex;
    private int _resolutionIndex;
    private int _uiScaleIndex;
    private string _statusText = "F1 opens settings.";

    public bool IsOpen { get; private set; }

    public SettingsMenuScript(SampleSettings settings, string settingsPath)
    {
        _settings = settings;
        _settingsPath = settingsPath;
    }

    public override void OnLoad()
    {
        SyncSelectionState();
        ApplyImmediateSettings();
        UpdateUi();
    }

    public override void OnUpdate(float deltaTime)
    {
        if (IsKeyPressed(KeyCode.F1))
        {
            IsOpen = !IsOpen;
            _statusText = IsOpen
                ? "Settings open. Window mode, resolution, VSync, and UI scale apply here."
                : "Settings closed.";
            return;
        }

        if (!IsOpen)
            return;

        if (IsKeyPressed(KeyCode.Escape))
        {
            IsOpen = false;
            _statusText = "Settings closed.";
            return;
        }

        if (IsKeyPressed(KeyCode.Up))
            _selectedRow = (_selectedRow + 3) % 4;

        if (IsKeyPressed(KeyCode.Down))
            _selectedRow = (_selectedRow + 1) % 4;

        if (IsKeyPressed(KeyCode.Left))
            AdjustSelectedSetting(-1);

        if (IsKeyPressed(KeyCode.Right))
            AdjustSelectedSetting(1);
    }

    public override void OnDraw()
    {
        UpdateUi();
    }

    private void AdjustSelectedSetting(int delta)
    {
        switch (_selectedRow)
        {
            case 0:
                _windowModeIndex = WrapIndex(_windowModeIndex + delta, WindowModeOptions.Length);
                var windowMode = WindowModeOptions[_windowModeIndex];
                if (Engine.SetWindowMode(windowMode))
                {
                    _settings.WindowMode = windowMode;
                    if (windowMode == WindowMode.Windowed)
                    {
                        Engine.Resize(_settings.Width, _settings.Height);
                        SaveSettings($"Window mode changed to {GetWindowModeLabel(windowMode)}.");
                    }
                    else
                    {
                        SaveSettings($"Window mode changed to {GetWindowModeLabel(windowMode)}.");
                    }
                }
                else
                {
                    _statusText = $"Window mode change to {GetWindowModeLabel(windowMode)} failed.";
                }
                break;

            case 1:
                _resolutionIndex = WrapIndex(_resolutionIndex + delta, ResolutionOptions.Length);
                var resolution = ResolutionOptions[_resolutionIndex];
                _settings.Width = resolution.Width;
                _settings.Height = resolution.Height;

                if (_settings.WindowMode == WindowMode.Windowed && Engine.Resize(resolution.Width, resolution.Height))
                {
                    SaveSettings($"Resolution changed to {resolution.Label}.");
                }
                else if (_settings.WindowMode != WindowMode.Windowed)
                {
                    SaveSettings($"Windowed resolution saved as {resolution.Label}.");
                }
                else
                {
                    _statusText = $"Resolution change to {resolution.Label} failed.";
                }
                break;

            case 2:
                _settings.VSyncEnabled = !_settings.VSyncEnabled;
                Engine.VSyncEnabled = _settings.VSyncEnabled;
                SaveSettings(_settings.VSyncEnabled ? "VSync enabled." : "VSync disabled.");
                break;

            case 3:
                _uiScaleIndex = WrapIndex(_uiScaleIndex + delta, UiScaleOptions.Length);
                _settings.UiScale = UiScaleOptions[_uiScaleIndex];
                Engine.UiScale = _settings.UiScale;
                SaveSettings($"UI scale set to {_settings.UiScale * 100f:0}%.");
                break;
        }
    }

    private void ApplyImmediateSettings()
    {
        Engine.VSyncEnabled = _settings.VSyncEnabled;
        Engine.UiScale = _settings.UiScale;
        Engine.SetWindowMode(_settings.WindowMode);
        if (_settings.WindowMode == WindowMode.Windowed)
            Engine.Resize(_settings.Width, _settings.Height);
    }

    private void SaveSettings(string statusText)
    {
        SampleSettingsStore.Save(_settingsPath, _settings);
        _statusText = statusText;
    }

    private void SyncSelectionState()
    {
        _windowModeIndex = Array.FindIndex(WindowModeOptions, option => option == _settings.WindowMode);
        if (_windowModeIndex < 0)
            _windowModeIndex = 0;

        _resolutionIndex = Array.FindIndex(
            ResolutionOptions,
            option => option.Width == _settings.Width && option.Height == _settings.Height);
        if (_resolutionIndex < 0)
            _resolutionIndex = 0;

        _uiScaleIndex = 0;
        var smallestDelta = float.MaxValue;
        for (var index = 0; index < UiScaleOptions.Length; index++)
        {
            var delta = Math.Abs(UiScaleOptions[index] - _settings.UiScale);
            if (delta < smallestDelta)
            {
                smallestDelta = delta;
                _uiScaleIndex = index;
            }
        }

        _settings.Width = ResolutionOptions[_resolutionIndex].Width;
        _settings.Height = ResolutionOptions[_resolutionIndex].Height;
        _settings.UiScale = UiScaleOptions[_uiScaleIndex];
    }

    private void UpdateUi()
    {
        if (Engine.UI is null)
            return;

        var currentWindowMode = Engine.WindowMode;
        _settings.WindowMode = currentWindowMode;
        _windowModeIndex = Array.FindIndex(WindowModeOptions, option => option == currentWindowMode);
        if (_windowModeIndex < 0)
            _windowModeIndex = 0;

        Engine.UI.SetVisible("settings-panel", IsOpen);
        Engine.UI.UpdateText("settings-title", "Display Settings");
        Engine.UI.UpdateText("settings-row-1", FormatRow(0, "Mode", GetWindowModeLabel(currentWindowMode)));
        Engine.UI.UpdateText(
            "settings-row-2",
            FormatRow(
                1,
                "Resolution",
                currentWindowMode == WindowMode.Windowed
                    ? $"{Engine.Width}x{Engine.Height}"
                    : $"{ResolutionOptions[_resolutionIndex].Label} | windowed"));
        Engine.UI.UpdateText("settings-row-3", FormatRow(2, "VSync", _settings.VSyncEnabled ? "On" : "Off"));
        Engine.UI.UpdateText("settings-row-4", FormatRow(3, "UI Scale", $"{_settings.UiScale * 100f:0}%"));
        Engine.UI.UpdateText(
            "settings-footer",
            IsOpen
                ? _statusText + " Up/Down select | Left/Right change | Esc/F1 close."
                : "F1 opens settings.");
    }

    private string FormatRow(int row, string label, string value)
    {
        var prefix = row == _selectedRow ? ">" : " ";
        return $"{prefix} {label}: {value}";
    }

    private static int WrapIndex(int index, int length)
    {
        return (index % length + length) % length;
    }

    private readonly record struct ResolutionOption(int Width, int Height)
    {
        public string Label => $"{Width}x{Height}";
    }

    private static string GetWindowModeLabel(WindowMode windowMode)
    {
        return windowMode switch
        {
            WindowMode.Windowed => "Windowed",
            WindowMode.MaximizedWindow => "Maximized",
            WindowMode.BorderlessFullscreen => "Borderless Fullscreen",
            _ => "Windowed"
        };
    }
}