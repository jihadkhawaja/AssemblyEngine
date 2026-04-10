using AssemblyEngine.Core;
using AssemblyEngine.Engine;

namespace RtsSample;

public static class Program
{
    public static void Main()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "sample-settings.json");
        var settings = RtsSampleSettingsStore.Load(settingsPath);
        var audioDir = Path.Combine(AppContext.BaseDirectory, "generated-audio", "rts");
        RtsAudioAssets.EnsureAssets(audioDir);

        var engine = new GameEngine(settings.Width, settings.Height, "AssemblyEngine - Command Center")
        {
            ClearColor = new Color(20, 16, 10),
            UiScale = settings.UiScale,
            VSyncEnabled = settings.VSyncEnabled,
            PresentationBackend = settings.PresentationBackend
        };

        engine.SetWindowMode(settings.WindowMode);
        engine.Scenes.Register("frontier", new RtsScene());
        engine.Scripts.RegisterScript(new RtsAudioScript(audioDir));
        engine.Scripts.RegisterScript(new RtsGameScript());
        engine.Scripts.RegisterScript(new RtsMenuScript(settings, settingsPath));
        engine.Scripts.RegisterScript(new RtsHudScript());

        var uiDir = Path.Combine(AppContext.BaseDirectory, "ui");
        var htmlPath = Path.Combine(uiDir, "hud.html");
        var cssPath = Path.Combine(uiDir, "hud.css");
        if (File.Exists(htmlPath))
            engine.LoadUI(htmlPath, File.Exists(cssPath) ? cssPath : null);

        engine.Scenes.LoadScene("frontier");
        engine.Run();
    }
}