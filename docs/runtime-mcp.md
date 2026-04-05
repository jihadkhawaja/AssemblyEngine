# Runtime MCP Server

AssemblyEngine now includes a stdio MCP server that can launch a game, stream runtime logs, inspect state, capture the current game framebuffer, and inject keyboard or mouse input into the running engine.

## What It Exposes

- `launch_game`: starts a game executable with the runtime diagnostics bridge enabled
- `get_session_status`: returns the current process and runtime state snapshot as JSON
- `wait_for_logs`: returns buffered runtime and process logs after a sequence cursor
- `capture_screenshot`: returns the current game framebuffer as a PNG image
- `send_key`: sends `tap`, `down`, or `up` keyboard actions using engine key names
- `move_mouse`: moves the in-game mouse to client coordinates inside the game window
- `click_mouse`: taps a mouse button at client coordinates inside the game window
- `set_mouse_button`: sets a mouse button to `down` or `up` explicitly
- `stop_game`: requests a graceful close or kills the process tree

## Build

The standard build script now builds the MCP server into `build/output/mcp`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1
```

You can also build the server directly:

```powershell
dotnet build .\src\tools\AssemblyEngine.RuntimeMcpServer\AssemblyEngine.RuntimeMcpServer.csproj -c Release
```

## Run As An MCP Server

Use the built apphost or `dotnet` with the project file. For current VS Code builds, put this in `.vscode/mcp.json` so the workspace server is auto-discovered:

```json
{
  "servers": {
    "assemblyengine-runtime": {
      "type": "stdio",
      "command": "dotnet",
      "cwd": "${workspaceFolder}",
      "args": [
        "run",
        "--project",
        "src/tools/AssemblyEngine.RuntimeMcpServer/AssemblyEngine.RuntimeMcpServer.csproj",
        "-c",
        "Release"
      ]
    }
  }
}
```

Older workspace-setting-based MCP examples are no longer auto-discovered by VS Code. If you previously stored the server under the `mcp` section of `AssemblyEngine.code-workspace`, move it to `.vscode/mcp.json`.

If you prefer the built executable, the default path after `shell/build.ps1` is `build/output/mcp/AssemblyEngine.RuntimeMcpServer.exe`.

## Typical Workflow

1. Build the engine and sample game.
2. Start the MCP server from your MCP client.
3. Call `launch_game` with the path to your game executable, for example `build/output/SampleGame.exe`.
4. Poll `wait_for_logs` with the last returned sequence number to tail runtime activity and crash details.
5. Call `capture_screenshot` whenever the agent needs to see the current frame.
6. Drive the game with `send_key`, `move_mouse`, `click_mouse`, and `set_mouse_button`.

## Notes

- Screenshots are captured through the runtime diagnostics bridge at frame end, so they reflect the engine's current rendered frame without sampling the desktop.
- Capture fails if the engine window is not initialized yet.
- Synthetic input is injected at the engine input layer after `PollEvents()`, which makes `tap` actions visible to gameplay code on the next update.
- Mouse coordinates are client coordinates relative to the game window.
- Runtime logs include both structured engine events and any redirected child-process stdout or stderr.