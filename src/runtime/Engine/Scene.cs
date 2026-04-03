namespace AssemblyEngine.Engine;

/// <summary>
/// A scene holds a collection of entities and manages their lifecycle.
/// </summary>
public class Scene
{
    private readonly List<Entity> _entities = [];
    private readonly List<Entity> _pendingAdd = [];
    private readonly List<Entity> _pendingRemove = [];
    private bool _updating;

    public string Name { get; set; }
    public IReadOnlyList<Entity> Entities => _entities;

    public Scene(string name = "Scene")
    {
        Name = name;
    }

    public Entity CreateEntity(string name = "Entity")
    {
        var entity = new Entity(name) { Scene = this };
        if (_updating)
            _pendingAdd.Add(entity);
        else
            _entities.Add(entity);
        return entity;
    }

    public void AddEntity(Entity entity)
    {
        entity.Scene = this;
        if (_updating)
            _pendingAdd.Add(entity);
        else
            _entities.Add(entity);
    }

    public void RemoveEntity(Entity entity)
    {
        if (_updating)
            _pendingRemove.Add(entity);
        else
        {
            entity.Destroy();
            _entities.Remove(entity);
        }
    }

    public Entity? FindByName(string name)
    {
        foreach (var e in _entities)
            if (e.Name == name) return e;
        return null;
    }

    public Entity? FindByTag(string tag)
    {
        foreach (var e in _entities)
            if (e.Tag == tag) return e;
        return null;
    }

    public List<Entity> FindAllByTag(string tag)
    {
        var results = new List<Entity>();
        foreach (var e in _entities)
            if (e.Tag == tag) results.Add(e);
        return results;
    }

    public virtual void OnLoad() { }
    public virtual void OnUnload() { }

    public void Update(float deltaTime)
    {
        _updating = true;
        for (int i = 0; i < _entities.Count; i++)
            _entities[i].Update(deltaTime);
        _updating = false;

        ProcessPending();
    }

    public void Draw()
    {
        for (int i = 0; i < _entities.Count; i++)
            _entities[i].Draw();
    }

    private void ProcessPending()
    {
        if (_pendingAdd.Count > 0)
        {
            _entities.AddRange(_pendingAdd);
            _pendingAdd.Clear();
        }

        if (_pendingRemove.Count > 0)
        {
            foreach (var e in _pendingRemove)
            {
                e.Destroy();
                _entities.Remove(e);
            }
            _pendingRemove.Clear();
        }
    }

    public void Clear()
    {
        foreach (var e in _entities) e.Destroy();
        _entities.Clear();
        _pendingAdd.Clear();
        _pendingRemove.Clear();
    }
}
