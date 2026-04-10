using AssemblyEngine.Scripting;

namespace RtsSample;

public sealed class RtsHudScript : GameScript
{
    public override void OnDraw()
    {
        var game = Engine.Scripts.GetScript<RtsGameScript>();
        if (game is null || Engine.UI is null)
            return;

        Engine.UI.SetVisible("hud-root", game.MatchRunning);
        if (!game.MatchRunning)
        {
            Engine.UI.SetVisible("center-message", false);
            Engine.UI.SetVisible("help-panel", false);
            return;
        }

        Engine.UI.UpdateText("fps-counter", $"FPS {Fps} | {game.BackendLabel}");
        Engine.UI.UpdateText("ore", $"Ore {game.OreStockpile}/{game.OreGoal}");
        Engine.UI.UpdateText("hq", $"HQ {game.HeadquartersHealth}");
        Engine.UI.UpdateText("workers", $"Workers {game.WorkerCount}");
        Engine.UI.UpdateText("guards", $"Guards {game.GuardCount}");
        Engine.UI.UpdateText("wave", game.WaveText);
        Engine.UI.UpdateText("objective", game.ObjectiveText);
        Engine.UI.UpdateText("selection", game.SelectedSummary);
        Engine.UI.UpdateText("queue", game.QueueSummary);
        Engine.UI.UpdateText("queue-worker-detail", game.WorkerBuildButtonText);
        Engine.UI.UpdateText("queue-guard-detail", game.GuardBuildButtonText);
        Engine.UI.UpdateText("queue-building-detail", game.BuildingBuildButtonText);
        Engine.UI.UpdateText("queue-tower-detail", game.DefenseTowerBuildButtonText);
        Engine.UI.UpdateText("production-mode", game.ProductionModeText);
        Engine.UI.UpdateText("production-sites", game.ProductionSitesSummary);
        Engine.UI.UpdateText("forces", game.ForceSummary);
        Engine.UI.UpdateText("economy", game.EconomySummary);
        Engine.UI.UpdateText("message-title", game.MessageTitle);
        Engine.UI.UpdateText("message-subtitle", game.MessageSubtitle);
        Engine.UI.SetVisible("center-message", game.ShowCenterMessage);
        Engine.UI.SetVisible("help-panel", game.HelpVisible);
    }
}