using AssemblyEngine.Core;
using AssemblyEngine.Engine;
using System.Runtime.Versioning;

namespace VisualNovelSample;

[SupportedOSPlatform("windows")]
public static class Program
{
    public static void Main()
    {
        var assetDir = Path.Combine(AppContext.BaseDirectory, "generated-assets", "visual-novel");
        var audioDir = Path.Combine(AppContext.BaseDirectory, "generated-audio", "visual-novel");
        VisualNovelAssetBuilder.EnsureAssets(assetDir);
        VisualNovelAudioAssets.EnsureAssets(audioDir);

        var engine = new GameEngine(1280, 720, "AssemblyEngine - Lantern Letters")
        {
            ClearColor = new Color(7, 11, 23),
            UiScale = 1f,
            VSyncEnabled = true
        };

        var savePath = Path.Combine(AppContext.BaseDirectory, "visual-novel-save.json");
        engine.Scenes.Register("story", new VisualNovelScene(assetDir));
        engine.Scripts.RegisterScript(new VisualNovelAudioScript(audioDir));
        engine.Scripts.RegisterScript(new VisualNovelScript(savePath));

        var uiDir = Path.Combine(AppContext.BaseDirectory, "ui");
        var htmlPath = Path.Combine(uiDir, "hud.html");
        var cssPath = Path.Combine(uiDir, "hud.css");
        if (File.Exists(htmlPath))
            engine.LoadUI(htmlPath, File.Exists(cssPath) ? cssPath : null);

        engine.Scenes.LoadScene("story");
        engine.Run();
    }
}