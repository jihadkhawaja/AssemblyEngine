namespace AssemblyEngine.Engine;

/// <summary>
/// Base class for all components attached to entities.
/// Components hold data and optional behavior.
/// </summary>
public abstract class Component
{
    public Entity Entity { get; internal set; } = null!;
    public bool Enabled { get; set; } = true;

    public virtual void OnAttach() { }
    public virtual void OnDetach() { }
    public virtual void Update(float deltaTime) { }
    public virtual void Draw() { }
}
