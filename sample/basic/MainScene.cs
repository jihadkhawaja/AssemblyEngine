using AssemblyEngine.Core;
using AssemblyEngine.Engine;

namespace SampleGame;

/// <summary>
/// The main scene exposes simple factories used by the sample game loop.
/// </summary>
public sealed class MainScene : Scene
{
    public const int PickupSize = 18;
    public const int HunterSize = 28;

    public MainScene() : base("Main") { }

    public override void OnLoad() => ResetArena();

    public Entity ResetArena()
    {
        Clear();

        var player = CreateEntity("Player");
        player.Tag = "player";
        player.Position = new Vector2(384, 284);

        var collider = player.AddComponent<BoxCollider>();
        collider.Width = PlayerScript.PlayerSize;
        collider.Height = PlayerScript.PlayerSize;

        return player;
    }

    public Entity CreatePickup(string name, Vector2 position)
    {
        var pickup = CreateEntity(name);
        pickup.Tag = "pickup";
        pickup.Position = position;

        var collider = pickup.AddComponent<BoxCollider>();
        collider.Width = PickupSize;
        collider.Height = PickupSize;
        return pickup;
    }

    public Entity CreateHunter(string name, Vector2 position)
    {
        var hunter = CreateEntity(name);
        hunter.Tag = "hunter";
        hunter.Position = position;

        var collider = hunter.AddComponent<BoxCollider>();
        collider.Width = HunterSize;
        collider.Height = HunterSize;
        return hunter;
    }
}
