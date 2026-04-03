namespace AssemblyEngine.Engine;

/// <summary>
/// Manages scene transitions and the active scene stack.
/// </summary>
public sealed class SceneManager
{
    private readonly Dictionary<string, Scene> _scenes = [];
    private Scene? _active;
    private Scene? _next;

    public Scene? ActiveScene => _active;

    public void Register(string name, Scene scene)
    {
        scene.Name = name;
        _scenes[name] = scene;
    }

    public void LoadScene(string name)
    {
        if (!_scenes.TryGetValue(name, out var scene))
            throw new InvalidOperationException($"Scene '{name}' not registered.");
        _next = scene;
    }

    internal void ProcessTransition()
    {
        if (_next is null) return;

        _active?.OnUnload();
        _active?.Clear();
        _active = _next;
        _next = null;
        _active.OnLoad();
    }

    internal void Update(float deltaTime)
    {
        ProcessTransition();
        _active?.Update(deltaTime);
    }

    internal void Draw()
    {
        _active?.Draw();
    }
}
