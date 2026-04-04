using AssemblyEngine.Core;
using NumericVector3 = System.Numerics.Vector3;

namespace AssemblyEngine.Engine;

/// <summary>
/// A game entity that holds a transform and a collection of components.
/// </summary>
public sealed class Entity
{
    private readonly List<Component> _components = [];
    private static int _nextId;
    private NumericVector3 _position3D;
    private NumericVector3 _scale3D = NumericVector3.One;
    private NumericVector3 _rotation3D;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public string Name { get; set; }
    public string Tag { get; set; } = "";
    public bool Active { get; set; } = true;
    public Vector2 Position
    {
        get => new(_position3D.X, _position3D.Y);
        set => _position3D = new NumericVector3(value.X, value.Y, _position3D.Z);
    }

    public Vector2 Scale
    {
        get => new(_scale3D.X, _scale3D.Y);
        set => _scale3D = new NumericVector3(value.X, value.Y, _scale3D.Z);
    }

    public float Rotation
    {
        get => _rotation3D.Z;
        set => _rotation3D.Z = value;
    }

    public NumericVector3 Position3D
    {
        get => _position3D;
        set => _position3D = value;
    }

    public NumericVector3 Scale3D
    {
        get => _scale3D;
        set => _scale3D = value;
    }

    public NumericVector3 Rotation3D
    {
        get => _rotation3D;
        set => _rotation3D = value;
    }

    public float Depth
    {
        get => _position3D.Z;
        set => _position3D.Z = value;
    }
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
