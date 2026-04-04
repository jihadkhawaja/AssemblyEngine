using System.ComponentModel;
using AssemblyEngine.Core;
using AssemblyEngine.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AssemblyEngine.RuntimeMcpServer.Tools;

[McpServerToolType]
public sealed class RuntimeSessionTools
{
    private readonly RuntimeGameSessionManager _manager;

    public RuntimeSessionTools(IServiceProvider serviceProvider)
    {
        _manager = serviceProvider.GetRequiredService<RuntimeGameSessionManager>();
    }

    [McpServerTool, Description("Launches a game executable under the AssemblyEngine runtime diagnostics bridge.")]
    public async Task<string> LaunchGame(
        [Description("Absolute or relative path to the game executable.")] string executablePath,
        [Description("Optional command-line arguments passed to the game.")] string? arguments = null,
        [Description("Optional working directory. Defaults to the executable folder.")] string? workingDirectory = null,
        [Description("How long to wait for the runtime bridge to connect before returning.")] int waitForBridgeMilliseconds = 3000,
        CancellationToken cancellationToken = default)
    {
        RuntimeSessionStatus status = await _manager.LaunchAsync(
            executablePath,
            arguments,
            workingDirectory,
            waitForBridgeMilliseconds,
            cancellationToken);

        return JsonText.Serialize(status);
    }

    [McpServerTool, Description("Returns the active session status. Use refreshState=true to ask the running game for a fresh state snapshot.")]
    public async Task<string> GetSessionStatus(
        [Description("When true, request a fresh runtime state snapshot before returning.")] bool refreshState = true,
        CancellationToken cancellationToken = default)
    {
        return JsonText.Serialize(await _manager.GetStatusAsync(refreshState, cancellationToken));
    }

    [McpServerTool, Description("Waits for new runtime log entries after the given sequence number and returns a JSON batch.")]
    public async Task<string> WaitForLogs(
        [Description("Return only entries with a sequence greater than this value.")] long afterSequence = 0,
        [Description("Maximum number of entries to return.")] int maxEntries = 50,
        [Description("How long to wait for new entries before returning, in milliseconds.")] int timeoutMilliseconds = 1000,
        CancellationToken cancellationToken = default)
    {
        return JsonText.Serialize(await _manager.WaitForLogsAsync(afterSequence, maxEntries, timeoutMilliseconds, cancellationToken));
    }

    [McpServerTool, Description("Captures the current game framebuffer and returns it as a PNG image.")]
    public async Task<ImageContentBlock> CaptureScreenshot(
        CancellationToken cancellationToken = default)
    {
        RuntimeScreenshot screenshot = await _manager.CaptureScreenshotAsync(cancellationToken);
        return ImageContentBlock.FromBytes(screenshot.ImageData, screenshot.MimeType);
    }

    [McpServerTool, Description("Sends keyboard input to the running game. action can be tap, down, or up.")]
    public async Task<string> SendKey(
        [Description("Engine key name, for example W, Space, Escape, Left, Enter, or F1.")] string key,
        [Description("Input action: tap, down, or up.")] string action = "tap",
        [Description("For tap, how many frames to hold the key before releasing.")] int holdFrames = 1,
        CancellationToken cancellationToken = default)
    {
        var command = new RuntimeKeyInputCommand(
            ParseEnum<KeyCode>(key, nameof(key)),
            ParseEnum<RuntimeInputAction>(action, nameof(action)),
            holdFrames);

        return JsonText.Serialize(await _manager.SendKeyAsync(command, cancellationToken));
    }

    [McpServerTool, Description("Moves the in-game mouse cursor to client coordinates relative to the game window.")]
    public async Task<string> MoveMouse(
        [Description("Client X coordinate inside the game window.")] int x,
        [Description("Client Y coordinate inside the game window.")] int y,
        CancellationToken cancellationToken = default)
    {
        return JsonText.Serialize(await _manager.MoveMouseAsync(new RuntimeMouseMoveCommand(x, y), cancellationToken));
    }

    [McpServerTool, Description("Clicks a mouse button at client coordinates inside the game window.")]
    public async Task<string> ClickMouse(
        [Description("Mouse button: Left, Right, or Middle.")] string button,
        [Description("Client X coordinate inside the game window.")] int x,
        [Description("Client Y coordinate inside the game window.")] int y,
        [Description("How many frames to hold the button before releasing.")] int holdFrames = 1,
        CancellationToken cancellationToken = default)
    {
        var command = new RuntimeMouseButtonInputCommand(
            ParseEnum<MouseButton>(button, nameof(button)),
            RuntimeInputAction.Tap,
            holdFrames,
            x,
            y);

        return JsonText.Serialize(await _manager.SendMouseButtonAsync(command, cancellationToken));
    }

    [McpServerTool, Description("Sets a mouse button state explicitly. action can be down or up.")]
    public async Task<string> SetMouseButton(
        [Description("Mouse button: Left, Right, or Middle.")] string button,
        [Description("Input action: down or up.")] string action,
        [Description("Optional client X coordinate to move to before the button state changes.")] int? x = null,
        [Description("Optional client Y coordinate to move to before the button state changes.")] int? y = null,
        CancellationToken cancellationToken = default)
    {
        RuntimeInputAction parsedAction = ParseEnum<RuntimeInputAction>(action, nameof(action));
        if (parsedAction == RuntimeInputAction.Tap)
            throw new ArgumentOutOfRangeException(nameof(action), action, "Use ClickMouse for tap actions.");

        var command = new RuntimeMouseButtonInputCommand(
            ParseEnum<MouseButton>(button, nameof(button)),
            parsedAction,
            1,
            x,
            y);

        return JsonText.Serialize(await _manager.SendMouseButtonAsync(command, cancellationToken));
    }

    [McpServerTool, Description("Stops the active game session. When force=true, kill the process tree immediately.")]
    public async Task<string> StopGame(
        [Description("Kill the process tree immediately instead of requesting a graceful close.")] bool force = false,
        CancellationToken cancellationToken = default)
    {
        return JsonText.Serialize(await _manager.StopAsync(force, cancellationToken));
    }

    private static TEnum ParseEnum<TEnum>(string value, string parameterName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, true, out var parsed))
            return parsed;

        throw new ArgumentOutOfRangeException(parameterName, value, $"Unknown {typeof(TEnum).Name} value '{value}'.");
    }
}