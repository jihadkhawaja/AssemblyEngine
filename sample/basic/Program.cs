using AssemblyEngine.Core;
using AssemblyEngine.Engine;

namespace SampleGame;

public static class Program
{
    public static void Main()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "sample-settings.json");
        var settings = SampleSettingsStore.Load(settingsPath);
        var audioDir = Path.Combine(AppContext.BaseDirectory, "generated-audio", "basic");
        SampleAudioAssets.EnsureAssets(audioDir);

        var engine = new GameEngine(settings.Width, settings.Height, "AssemblyEngine - Dash Harvest")
        {
            ClearColor = new Color(5, 10, 18),
            UiScale = settings.UiScale,
            VSyncEnabled = settings.VSyncEnabled,
            PresentationBackend = settings.PresentationBackend
        };
        engine.SetWindowMode(settings.WindowMode);

        // Register scenes
        engine.Scenes.Register("main", new MainScene());

        // Register game scripts
        engine.Scripts.RegisterScript(new SampleAudioScript(audioDir));
        engine.Scripts.RegisterScript(new SettingsMenuScript(settings, settingsPath));
        engine.Scripts.RegisterScript(new GameLoopScript());
        engine.Scripts.RegisterScript(new PlayerScript());
        engine.Scripts.RegisterScript(new HudScript());

        // Load UI overlay
        var uiDir = Path.Combine(AppContext.BaseDirectory, "ui");
        var htmlPath = Path.Combine(uiDir, "hud.html");
        var cssPath = Path.Combine(uiDir, "hud.css");
        if (File.Exists(htmlPath))
            engine.LoadUI(htmlPath, File.Exists(cssPath) ? cssPath : null);

        // Load the main scene and run
        engine.Scenes.LoadScene("main");
        engine.Run();
    }
}
