using AssemblyEngine.Scripting;

namespace FpsSample;

public sealed class FpsHudScript : GameScript
{
    public override void OnDraw()
    {
        var game = Engine.Scripts.GetScript<FpsGameScript>();
        if (game is null || Engine.UI is null)
            return;

        Engine.UI.UpdateText("fps-counter", $"FPS {Fps} | {game.BackendLabel}");
        Engine.UI.UpdateText("health", $"Hull {game.Health}");
        Engine.UI.UpdateText("drones", $"Drones {game.EnemiesRemaining}");
        Engine.UI.UpdateText("accuracy", $"Accuracy {game.Accuracy * 100f:0}%");
        Engine.UI.UpdateText("timer", $"Time {game.MissionTime:0.0}s");
        Engine.UI.UpdateText("objective", game.ObjectiveText);
        Engine.UI.UpdateText("hint", game.HintText);
        Engine.UI.UpdateText("message-title", game.MessageTitle);
        Engine.UI.UpdateText("message-subtitle", game.MessageSubtitle);
        Engine.UI.SetVisible("center-message", game.ShowCenterMessage);
        Engine.UI.SetVisible("help-panel", game.HelpVisible);
    }
}