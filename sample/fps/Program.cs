using AssemblyEngine.Core;
using AssemblyEngine.Engine;

namespace FpsSample;

public static class Program
{
    public static void Main()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "sample-settings.json");
        var settings = FpsSampleSettingsStore.Load(settingsPath);

        var engine = new GameEngine(settings.Width, settings.Height, "AssemblyEngine - Citadel Breach")
        {
            ClearColor = new Color(6, 14, 24),
            UiScale = settings.UiScale,
            VSyncEnabled = settings.VSyncEnabled,
            PresentationBackend = settings.PresentationBackend
        };

        engine.SetWindowMode(settings.WindowMode);
        engine.Scenes.Register("arena", new FpsArenaScene());
        engine.Scripts.RegisterScript(new FpsGameScript());
        engine.Scripts.RegisterScript(new FpsHudScript());

        var uiDir = Path.Combine(AppContext.BaseDirectory, "ui");
        var htmlPath = Path.Combine(uiDir, "hud.html");
        var cssPath = Path.Combine(uiDir, "hud.css");
        if (File.Exists(htmlPath))
            engine.LoadUI(htmlPath, File.Exists(cssPath) ? cssPath : null);

        engine.Scenes.LoadScene("arena");
        engine.Run();
    }
}