using AssemblyEngine.Platform;

namespace AssemblyEngine.Core;

/// <summary>
/// High-level input queries for keyboard and mouse.
/// </summary>
public static class InputSystem
{
    public static bool IsKeyDown(KeyCode key) => EngineHost.IsKeyDown(key);
    public static bool IsKeyPressed(KeyCode key) => EngineHost.IsKeyPressed(key);
    public static int MouseX => EngineHost.MouseX;
    public static int MouseY => EngineHost.MouseY;
    public static Vector2 MousePosition => new(MouseX, MouseY);
    public static bool IsMouseDown(MouseButton button) => EngineHost.IsMouseDown(button);
}
