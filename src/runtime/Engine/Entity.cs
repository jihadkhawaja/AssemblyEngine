using AssemblyEngine.Core;

namespace AssemblyEngine.Engine;

/// <summary>
/// A game entity that holds a transform and a collection of components.
/// </summary>
public sealed class Entity
{
    private readonly List<Component> _components = [];
    private static int _nextId;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public string Name { get; set; }
    public string Tag { get; set; } = "";
    public bool Active { get; set; } = true;
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; } = Vector2.One;
    public float Rotation { get; set; }
    public Scene? Scene { get; internal set; }

    public Entity(string name = "Entity")
    {
        Name = name;
    }

    public T AddComponent<T>() where T : Component, new()
    {
        var component = new T { Entity = this };
        _components.Add(component);
        component.OnAttach();
        return component;
    }

    public T? GetComponent<T>() where T : Component
    {
        foreach (var c in _components)
            if (c is T typed) return typed;
        return null;
    }

    public bool HasComponent<T>() where T : Component =>
        GetComponent<T>() is not null;

    public void RemoveComponent<T>() where T : Component
    {
        for (int i = _components.Count - 1; i >= 0; i--)
        {
            if (_components[i] is T)
            {
                _components[i].OnDetach();
                _components.RemoveAt(i);
                return;
            }
        }
    }

    internal void Update(float deltaTime)
    {
        if (!Active) return;
        for (int i = 0; i < _components.Count; i++)
            if (_components[i].Enabled)
                _components[i].Update(deltaTime);
    }

    internal void Draw()
    {
        if (!Active) return;
        for (int i = 0; i < _components.Count; i++)
            if (_components[i].Enabled)
                _components[i].Draw();
    }

    internal void Destroy()
    {
        for (int i = _components.Count - 1; i >= 0; i--)
            _components[i].OnDetach();
        _components.Clear();
    }
}
