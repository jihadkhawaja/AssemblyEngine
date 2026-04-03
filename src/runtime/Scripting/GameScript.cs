using AssemblyEngine.Core;
using AssemblyEngine.Engine;

namespace AssemblyEngine.Scripting;

/// <summary>
/// Base class for user game scripts. Subclass this in your game project
/// to define game behavior. Scripts are loaded and managed by ScriptManager.
/// </summary>
public abstract class GameScript
{
    public GameEngine Engine { get; internal set; } = null!;
    public Scene Scene => Engine.Scenes.ActiveScene!;

    /// <summary>Called once when the script is first loaded.</summary>
    public virtual void OnLoad() { }

    /// <summary>Called every frame before drawing.</summary>
    public virtual void OnUpdate(float deltaTime) { }

    /// <summary>Called every frame for rendering.</summary>
    public virtual void OnDraw() { }

    /// <summary>Called when the script is unloaded.</summary>
    public virtual void OnUnload() { }

    // Convenience accessors
    protected static bool IsKeyDown(KeyCode key) => InputSystem.IsKeyDown(key);
    protected static bool IsKeyPressed(KeyCode key) => InputSystem.IsKeyPressed(key);
    protected static Vector2 MousePosition => InputSystem.MousePosition;
    protected static bool IsMouseDown(MouseButton btn) => InputSystem.IsMouseDown(btn);
    protected static float DeltaTime => Time.DeltaTime;
    protected static int Fps => Time.Fps;
}
