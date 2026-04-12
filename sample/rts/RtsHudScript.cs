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
        Engine.UI.UpdateText("funds-display", $"FUNDS {game.OreStockpile}");
        Engine.UI.UpdateText("power-display", $"POWER {game.HeadquartersHealth}");
        Engine.UI.UpdateText("rank-stars", game.WaveText);
        Engine.UI.UpdateText("slot-1-name", game.Slot1NameText);
        Engine.UI.UpdateText("slot-1-cost", game.Slot1CostText);
        Engine.UI.UpdateText("slot-2-name", game.Slot2NameText);
        Engine.UI.UpdateText("slot-2-cost", game.Slot2CostText);
        Engine.UI.UpdateText("slot-3-name", game.Slot3NameText);
        Engine.UI.UpdateText("slot-3-cost", game.Slot3CostText);
        Engine.UI.UpdateText("slot-4-name", game.Slot4NameText);
        Engine.UI.UpdateText("slot-4-cost", game.Slot4CostText);
        Engine.UI.UpdateText("slot-5-name", game.Slot5NameText);
        Engine.UI.UpdateText("slot-5-cost", game.Slot5CostText);
        Engine.UI.UpdateText("slot-6-name", game.Slot6NameText);
        Engine.UI.UpdateText("slot-6-cost", game.Slot6CostText);
        Engine.UI.UpdateText("slot-7-name", game.Slot7NameText);
        Engine.UI.UpdateText("slot-7-cost", game.Slot7CostText);
        Engine.UI.UpdateText("slot-8-name", game.Slot8NameText);
        Engine.UI.UpdateText("slot-8-cost", game.Slot8CostText);
        Engine.UI.UpdateText("queue-status", game.QueueSummary);
        Engine.UI.UpdateText("unit-name", game.UnitPanelName);
        Engine.UI.UpdateText("unit-health", game.UnitPanelHealth);
        Engine.UI.UpdateText("unit-stats", game.UnitPanelStats);
        Engine.UI.UpdateText("unit-orders", game.UnitPanelOrders);
        Engine.UI.UpdateText("forces", game.ForceSummary);
        Engine.UI.UpdateText("economy", game.EconomySummary);
        Engine.UI.UpdateText("message-title", game.MessageTitle);
        Engine.UI.UpdateText("message-subtitle", game.MessageSubtitle);
        Engine.UI.SetVisible("center-message", game.ShowCenterMessage);
        Engine.UI.SetVisible("help-panel", game.HelpVisible);
    }
}