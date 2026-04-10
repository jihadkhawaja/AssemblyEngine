using AssemblyEngine.Core;

namespace RtsSample;

public sealed class RtsSampleSettings
{
    private const int DefaultMultiplayerPort = 40444;

    public int Width { get; set; } = 1280;

    public int Height { get; set; } = 720;

    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;

    public bool VSyncEnabled { get; set; } = true;

    public float UiScale { get; set; } = 1f;

    public GraphicsBackend PresentationBackend { get; set; } = GraphicsBackend.Vulkan;

    public string PlayerName { get; set; } = "Commander";

    public string PeerAddress { get; set; } = "127.0.0.1";

    public int MultiplayerPort { get; set; } = DefaultMultiplayerPort;

    public void Sanitize()
    {
        Width = Math.Clamp(Width, 960, 3840);
        Height = Math.Clamp(Height, 640, 2160);
        UiScale = Math.Clamp(UiScale, 0.75f, 2f);
        MultiplayerPort = Math.Clamp(MultiplayerPort, 1024, 65535);
        PlayerName = string.IsNullOrWhiteSpace(PlayerName) ? "Commander" : PlayerName.Trim();
        if (PlayerName.Length > 24)
            PlayerName = PlayerName[..24];
        PeerAddress = string.IsNullOrWhiteSpace(PeerAddress) ? "127.0.0.1" : PeerAddress.Trim();
        if (!Enum.IsDefined(WindowMode))
            WindowMode = WindowMode.Windowed;
        if (!Enum.IsDefined(PresentationBackend))
            PresentationBackend = GraphicsBackend.Software;
    }
}