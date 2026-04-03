using AssemblyEngine.Engine;
using System.Reflection;

namespace AssemblyEngine.Scripting;

/// <summary>
/// Discovers and manages GameScript instances from loaded assemblies.
/// </summary>
public sealed class ScriptManager
{
    private readonly List<GameScript> _scripts = [];
    private readonly GameEngine _engine;

    public IReadOnlyList<GameScript> Scripts => _scripts;

    public ScriptManager(GameEngine engine)
    {
        _engine = engine;
    }

    public void RegisterScript(GameScript script)
    {
        script.Engine = _engine;
        _scripts.Add(script);
    }

    public void LoadScriptsFromAssembly(Assembly assembly)
    {
        var scriptTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(GameScript)) && !t.IsAbstract);

        foreach (var type in scriptTypes)
        {
            if (Activator.CreateInstance(type) is GameScript script)
            {
                RegisterScript(script);
            }
        }
    }

    public void LoadAll()
    {
        foreach (var s in _scripts)
            s.OnLoad();
    }

    public void UpdateAll(float deltaTime)
    {
        foreach (var s in _scripts)
            s.OnUpdate(deltaTime);
    }

    public void DrawAll()
    {
        foreach (var s in _scripts)
            s.OnDraw();
    }

    public void UnloadAll()
    {
        foreach (var s in _scripts)
            s.OnUnload();
        _scripts.Clear();
    }

    public T? GetScript<T>() where T : GameScript
    {
        foreach (var s in _scripts)
            if (s is T typed) return typed;
        return null;
    }
}
