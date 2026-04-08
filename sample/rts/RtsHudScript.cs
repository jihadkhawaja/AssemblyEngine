using AssemblyEngine.Scripting;

namespace RtsSample;

public sealed class RtsHudScript : GameScript
{
    public override void OnDraw()
    {
        var game = Engine.Scripts.GetScript<RtsGameScript>();
        if (game is null || Engine.UI is null)
            return;

        Engine.UI.UpdateText("fps-counter", $"FPS {Fps} | {game.BackendLabel}");
        Engine.UI.UpdateText("ore", $"Ore {game.OreStockpile}/{game.OreGoal}");
        Engine.UI.UpdateText("hq", $"HQ {game.HeadquartersHealth}");
        Engine.UI.UpdateText("workers", $"Workers {game.WorkerCount}");
        Engine.UI.UpdateText("guards", $"Guards {game.GuardCount}");
        Engine.UI.UpdateText("wave", game.WaveText);
        Engine.UI.UpdateText("objective", game.ObjectiveText);
        Engine.UI.UpdateText("selection", game.SelectedSummary);
        Engine.UI.UpdateText("selection-detail", game.SelectionDetail);
        Engine.UI.UpdateText("hint", game.HintText);
        Engine.UI.UpdateText("queue", game.QueueSummary);
        Engine.UI.UpdateText("queue-worker-button", game.WorkerBuildButtonText);
        Engine.UI.UpdateText("queue-guard-button", game.GuardBuildButtonText);
        Engine.UI.UpdateText("rally", game.RallySummary);
        Engine.UI.UpdateText("camera", game.CameraSummary);
        Engine.UI.UpdateText("forces", game.ForceSummary);
        Engine.UI.UpdateText("economy", game.EconomySummary);
        Engine.UI.UpdateText("map-hint", game.MapHintText);
        Engine.UI.UpdateText("roster-1", game.RosterLine1);
        Engine.UI.UpdateText("roster-2", game.RosterLine2);
        Engine.UI.UpdateText("roster-3", game.RosterLine3);
        Engine.UI.UpdateText("message-title", game.MessageTitle);
        Engine.UI.UpdateText("message-subtitle", game.MessageSubtitle);
        Engine.UI.SetVisible("center-message", game.ShowCenterMessage);
        Engine.UI.SetVisible("help-panel", game.HelpVisible);
    }
}