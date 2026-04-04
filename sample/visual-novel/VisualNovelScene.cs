using AssemblyEngine.Core;
using AssemblyEngine.Engine;

namespace VisualNovelSample;

public sealed class VisualNovelScene : Scene
{
    public const int ViewportWidth = 1280;
    public const int ViewportHeight = 720;
    public const int LayerWidth = ViewportWidth;

    public static readonly Vector2 IrisAnchor = new(84, 122);
    public static readonly Vector2 RowanAnchor = new(846, 128);

    private readonly string _assetDir;
    private readonly Dictionary<string, int> _spriteIds = [];

    public Entity Iris { get; private set; } = null!;
    public Entity Rowan { get; private set; } = null!;
    public Entity MidLayerA { get; private set; } = null!;
    public Entity MidLayerB { get; private set; } = null!;
    public Entity FrontLayerA { get; private set; } = null!;
    public Entity FrontLayerB { get; private set; } = null!;
    public SpriteComponent IrisSprite { get; private set; } = null!;
    public SpriteComponent RowanSprite { get; private set; } = null!;

    public VisualNovelScene(string assetDir) : base("Story")
    {
        _assetDir = assetDir;
    }

    public override void OnLoad()
    {
        EnsureSpritesLoaded();

        CreateSpriteEntity("BackdropBase", "background-base", Vector2.Zero, out _);
        MidLayerA = CreateSpriteEntity("MidLayerA", "background-mid", new Vector2(0, 0), out _);
        MidLayerB = CreateSpriteEntity("MidLayerB", "background-mid", new Vector2(LayerWidth, 0), out _);

        Iris = CreateSpriteEntity("Iris", "iris-idle", IrisAnchor, out var irisSprite);
        Iris.Tag = "character";
        IrisSprite = irisSprite;

        Rowan = CreateSpriteEntity("Rowan", "rowan-idle", RowanAnchor, out var rowanSprite);
        Rowan.Tag = "character";
        RowanSprite = rowanSprite;

        FrontLayerA = CreateSpriteEntity("FrontLayerA", "background-front", new Vector2(0, 0), out _);
        FrontLayerB = CreateSpriteEntity("FrontLayerB", "background-front", new Vector2(LayerWidth, 0), out _);
    }

    public int GetSpriteId(string key) => _spriteIds[key];

    private void EnsureSpritesLoaded()
    {
        if (_spriteIds.Count > 0)
            return;

        _spriteIds["background-base"] = LoadSprite("backdrop-base.bmp");
        _spriteIds["background-mid"] = LoadSprite("backdrop-mid.bmp");
        _spriteIds["background-front"] = LoadSprite("backdrop-front.bmp");
        _spriteIds["iris-idle"] = LoadSprite("iris-idle.bmp");
        _spriteIds["iris-talk"] = LoadSprite("iris-talk.bmp");
        _spriteIds["iris-blink"] = LoadSprite("iris-blink.bmp");
        _spriteIds["rowan-idle"] = LoadSprite("rowan-idle.bmp");
        _spriteIds["rowan-talk"] = LoadSprite("rowan-talk.bmp");
        _spriteIds["rowan-blink"] = LoadSprite("rowan-blink.bmp");
    }

    private int LoadSprite(string fileName)
    {
        var spriteId = Graphics.LoadSprite(Path.Combine(_assetDir, fileName));
        if (spriteId < 0)
            throw new InvalidOperationException($"Failed to load sample sprite '{fileName}'.");

        return spriteId;
    }

    private Entity CreateSpriteEntity(string name, string spriteKey, Vector2 position, out SpriteComponent sprite)
    {
        var entity = CreateEntity(name);
        entity.Position = position;
        sprite = entity.AddComponent<SpriteComponent>();
        sprite.SpriteId = _spriteIds[spriteKey];
        return entity;
    }
}