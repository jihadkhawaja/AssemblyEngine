using AssemblyEngine.Core;
using AssemblyEngine.Scripting;

namespace SampleGame;

/// <summary>
/// Updates the HTML overlay with the current run state.
/// </summary>
public sealed class HudScript : GameScript
{
    public override void OnDraw()
    {
        var loop = Engine.Scripts.GetScript<GameLoopScript>();
        var player = Engine.Scripts.GetScript<PlayerScript>();

        if (Engine.UI is not null)
        {
            Engine.UI.UpdateText("fps-counter", $"FPS {Fps}");
            Engine.UI.UpdateText("score", $"Score {player?.Score ?? 0}");
            Engine.UI.UpdateText("wave", $"Wave {loop?.Wave ?? 0}");
            Engine.UI.UpdateText("lives", $"Hull {loop?.Lives ?? 0}");
            Engine.UI.UpdateText("best", $"Best {loop?.BestScore ?? 0}");
            Engine.UI.UpdateText(
                "dash",
                player is null
                    ? "Dash offline"
                    : player.DashReady ? "Dash READY" : $"Dash {player.DashCharge * 100f:0}%");
            Engine.UI.UpdateText("objective", loop?.ObjectiveText ?? "Collect sparks.");
            Engine.UI.UpdateText(
                "hint",
                loop?.GameOver == true
                    ? "Press R or Enter to restart the run."
                    : $"Combo x{player?.Combo ?? 0} | Space smashes hunters while dashing.");
            Engine.UI.UpdateText("message-title", loop?.BannerTitle ?? string.Empty);
            Engine.UI.UpdateText("message-subtitle", loop?.BannerSubtitle ?? string.Empty);
            Engine.UI.SetVisible("center-message", loop?.ShowBanner == true);
            return;
        }

        Graphics.DrawFilledRect(10, 10, 280, 24, new Color(0, 0, 0, 180));
    }
}
