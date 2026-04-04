using AssemblyEngine.Interop;
using AssemblyEngine.Platform;

namespace AssemblyEngine.Core;

/// <summary>
/// High-level input queries for keyboard and mouse.
/// </summary>
public static class InputSystem
{
    public static bool IsKeyDown(KeyCode key) => NativeCore.IsKeyDown(EnginePlatform.Current.ToNativeKeyCode(key)) != 0;
    public static bool IsKeyPressed(KeyCode key) => NativeCore.IsKeyPressed(EnginePlatform.Current.ToNativeKeyCode(key)) != 0;
    public static int MouseX => NativeCore.GetMouseX();
    public static int MouseY => NativeCore.GetMouseY();
    public static Vector2 MousePosition => new(MouseX, MouseY);
    public static bool IsMouseDown(MouseButton button) => NativeCore.IsMouseDown(EnginePlatform.Current.ToNativeMouseButton(button)) != 0;
}
