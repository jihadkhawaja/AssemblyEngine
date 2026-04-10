using AssemblyEngine.Networking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.Versioning;

namespace AssemblyEngine.Runtime.Tests.Networking;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class MultiplayerManagerTests
{
    [TestMethod]
    public async Task HostAndClientExchangePeerStateAndMessages()
    {
        await using var host = new MultiplayerManager();
        await using var client = new MultiplayerManager();

        await host.HostAsync(new MultiplayerHostOptions(0, "Host", MultiplayerTransportMode.Localhost));
        await client.JoinAsync(new MultiplayerJoinOptions("127.0.0.1", host.ListeningPort, "Client", MultiplayerTransportMode.Localhost));

        await PumpUntilAsync(() => host.Peers.Count == 2 && client.Peers.Count == 2, host, client);

        await client.SetReadyAsync(true);
        await PumpUntilAsync(() => host.Peers.Any(peer => !peer.IsLocal && peer.IsReady), host, client);

        MultiplayerMessageEventArgs? hostReceived = null;
        MultiplayerMessageEventArgs? clientReceived = null;
        host.MessageReceived += (_, args) => hostReceived = args;
        client.MessageReceived += (_, args) => clientReceived = args;

        await client.SendToHostAsync("rts", "command", new TestPayload(7, "client-order"));
        await PumpUntilAsync(() => hostReceived is not null, host, client);
        Assert.IsNotNull(hostReceived);
        Assert.AreEqual("command", hostReceived.Type);
        Assert.AreEqual("client-order", hostReceived.DeserializePayload<TestPayload>().Label);

        await host.BroadcastAsync("rts", "snapshot", new TestPayload(9, "host-sync"));
        await PumpUntilAsync(() => clientReceived is not null, host, client);
        Assert.IsNotNull(clientReceived);
        Assert.AreEqual("snapshot", clientReceived.Type);
        Assert.AreEqual(9, clientReceived.DeserializePayload<TestPayload>().Value);
    }

    [TestMethod]
    public async Task StartGameRaisesEventOnBothPeers()
    {
        await using var host = new MultiplayerManager();
        await using var client = new MultiplayerManager();

        await host.HostAsync(new MultiplayerHostOptions(0, "Host", MultiplayerTransportMode.Localhost));
        await client.JoinAsync(new MultiplayerJoinOptions("127.0.0.1", host.ListeningPort, "Client", MultiplayerTransportMode.Localhost));
        await PumpUntilAsync(() => host.Peers.Count == 2 && client.Peers.Count == 2, host, client);

        MultiplayerGameStartedEventArgs? hostStarted = null;
        MultiplayerGameStartedEventArgs? clientStarted = null;
        host.GameStarted += (_, args) => hostStarted = args;
        client.GameStarted += (_, args) => clientStarted = args;

        await host.StartGameAsync(new TestPayload(42, "launch"));
        await PumpUntilAsync(() => hostStarted is not null && clientStarted is not null, host, client);

        Assert.IsNotNull(hostStarted);
        Assert.IsNotNull(clientStarted);
        Assert.AreEqual(42, hostStarted.DeserializePayload<TestPayload>().Value);
        Assert.AreEqual("launch", clientStarted.DeserializePayload<TestPayload>().Label);
    }

    private static async Task PumpUntilAsync(Func<bool> condition, params MultiplayerManager[] managers)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            foreach (var manager in managers)
                manager.Pump();

            if (condition())
                return;

            await Task.Delay(10);
        }

        var diagnostics = string.Join(
            Environment.NewLine,
            managers.Select((manager, index) => $"manager[{index}] role={manager.Role} state={manager.State} peers={manager.Peers.Count} status={manager.StatusMessage} error={manager.LastException?.Message ?? "<none>"}"));
        Assert.Fail($"Timed out waiting for multiplayer managers to reach the expected state.{Environment.NewLine}{diagnostics}");
    }

    private sealed record TestPayload(int Value, string Label);
}