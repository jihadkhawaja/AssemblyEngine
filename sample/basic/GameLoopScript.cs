using AssemblyEngine.Core;
using AssemblyEngine.Engine;
using AssemblyEngine.Scripting;

namespace SampleGame;

/// <summary>
/// Drives the sample's arcade loop: collect sparks, dash through hunters, survive longer waves.
/// </summary>
public sealed class GameLoopScript : GameScript
{
    private const int StartingLives = 4;
    private const int MaxLives = 5;
    private const int SideMargin = 24;
    private const int TopMargin = 56;
    private const int BottomMargin = 72;
    private const float WaveClearDelay = 1.15f;
    private const float BannerDuration = 1.35f;
    private const float TwoPi = MathF.PI * 2f;

    private readonly Random _random = new();
    private readonly Dictionary<int, PickupMotion> _pickups = [];
    private readonly Dictionary<int, HunterMotion> _hunters = [];

    private SampleAudioScript _audio = null!;
    private MainScene _mainScene = null!;
    private PlayerScript _player = null!;
    private float _elapsed;
    private float _hunterSpawnTimer;
    private float _waveAdvanceTimer;
    private float _bannerTimer;
    private int _nextPickupId;
    private int _nextHunterId;
    private string _bannerTitle = string.Empty;
    private string _bannerSubtitle = string.Empty;

    public int Wave { get; private set; }
    public int Lives { get; private set; }
    public int BestScore { get; private set; }
    public bool GameOver { get; private set; }
    public bool ShowBanner => GameOver || _bannerTimer > 0f;
    public string BannerTitle => _bannerTitle;
    public string BannerSubtitle => _bannerSubtitle;
    public string ObjectiveText
    {
        get
        {
            if (GameOver)
                return "Press R or Enter to restart the run.";

            if (_waveAdvanceTimer > 0f)
                return "Arena recalibrating for the next wave.";

            return $"{_pickups.Count} sparks left | {_hunters.Count} hunters active | Combo x{_player.Combo}";
        }
    }

    public override void OnLoad()
    {
        _audio = Engine.Scripts.GetScript<SampleAudioScript>()
            ?? throw new InvalidOperationException("SampleAudioScript must be registered before GameLoopScript loads.");
        _mainScene = (MainScene)Scene;
        _player = Engine.Scripts.GetScript<PlayerScript>()
            ?? throw new InvalidOperationException("PlayerScript must be registered before GameLoopScript loads.");

        StartRun();
    }

    public override void OnUpdate(float deltaTime)
    {
        if (Engine.Scripts.GetScript<SettingsMenuScript>()?.IsOpen == true)
            return;

        if (_bannerTimer > 0f && !GameOver)
            _bannerTimer = Math.Max(0f, _bannerTimer - deltaTime);

        if (GameOver)
        {
            if (IsKeyPressed(KeyCode.R) || IsKeyPressed(KeyCode.Enter))
                StartRun();

            return;
        }

        _elapsed += deltaTime;

        if (_waveAdvanceTimer > 0f)
        {
            _waveAdvanceTimer = Math.Max(0f, _waveAdvanceTimer - deltaTime);
            if (_waveAdvanceTimer <= 0f)
                BeginWave(Wave + 1);
            return;
        }

        UpdatePickups(deltaTime);
        UpdateHunters(deltaTime);
        UpdateHunterSpawns(deltaTime);

        if (_pickups.Count == 0)
            CompleteWave();
    }

    public override void OnDraw()
    {
        DrawArenaBackdrop();
        DrawPickups();
        DrawHunters();
    }

    private void StartRun()
    {
        BestScore = Math.Max(BestScore, _player.Score);
        Lives = StartingLives;
        GameOver = false;
        _elapsed = 0f;
        _waveAdvanceTimer = 0f;
        _nextPickupId = 0;
        _nextHunterId = 0;

        BeginWave(1, resetScore: true);
    }

    private void BeginWave(int wave, bool resetScore = false)
    {
        Wave = wave;
        _pickups.Clear();
        _hunters.Clear();
        _mainScene.ResetArena();

        if (resetScore)
            _player.ResetRun();
        else
            _player.BeginWave();

        SpawnPickups(4 + wave);

        var initialHunters = Math.Min(1 + (wave / 2), 4);
        for (var index = 0; index < initialHunters; index++)
            SpawnHunter();

        _hunterSpawnTimer = GetHunterSpawnInterval();
        ShowTransientBanner(
            $"Wave {Wave}",
            Wave == 1
                ? "Collect sparks. Dash through hunters to score big."
                : "Fresh sparks online. Hunters are getting faster.",
            BannerDuration);
        _audio.PlayWaveStart();
    }

    private void CompleteWave()
    {
        if (_waveAdvanceTimer > 0f || GameOver)
            return;

        _player.AddScore(60 + (Wave * 20));
        _player.BreakCombo();
        Lives = Math.Min(MaxLives, Lives + 1);

        foreach (var hunter in Scene.FindAllByTag("hunter"))
            Scene.RemoveEntity(hunter);

        _hunters.Clear();
        _waveAdvanceTimer = WaveClearDelay;
        ShowTransientBanner(
            $"Wave {Wave} Clear",
            Lives == MaxLives
                ? "Hull topped off. Next wave lands hotter."
                : "Hull restored by 1. Next wave lands hotter.",
            WaveClearDelay);
        _audio.PlayWaveClear();
    }

    private void EndRun()
    {
        GameOver = true;
        BestScore = Math.Max(BestScore, _player.Score);
        _bannerTitle = "System Breach";
        _bannerSubtitle = $"Score {_player.Score} | Best {BestScore} | Press R or Enter to jump back in.";
        _bannerTimer = 0f;
        _audio.PlayGameOver();
    }

    private void UpdatePickups(float deltaTime)
    {
        if (!_player.HasPlayer)
            return;

        var playerBounds = _player.Bounds;
        foreach (var pickup in Scene.FindAllByTag("pickup"))
        {
            if (!_pickups.TryGetValue(pickup.Id, out var motion))
                continue;

            motion.Angle += motion.Speed * deltaTime;
            var sway = new Vector2(MathF.Cos(motion.Angle), MathF.Sin(motion.Angle));
            pickup.Position = ClampToArena(motion.Anchor + (sway * motion.Radius), MainScene.PickupSize);

            var collider = pickup.GetComponent<BoxCollider>();
            if (collider is not null && collider.Bounds.Intersects(playerBounds))
            {
                _player.AddScore(18 + (Wave * 4));
                _audio.PlayPickup();
                _pickups.Remove(pickup.Id);
                Scene.RemoveEntity(pickup);
            }
        }
    }

    private void UpdateHunters(float deltaTime)
    {
        if (!_player.HasPlayer)
            return;

        var playerBounds = _player.Bounds;
        var playerCenter = _player.Center;

        foreach (var hunter in Scene.FindAllByTag("hunter"))
        {
            if (!_hunters.TryGetValue(hunter.Id, out var motion))
                continue;

            motion.Phase += deltaTime * (3.2f + (Wave * 0.15f));

            var hunterCenter = hunter.Position + new Vector2(MainScene.HunterSize / 2f, MainScene.HunterSize / 2f);
            var toPlayer = playerCenter - hunterCenter;
            var direct = toPlayer.LengthSquared > 0.001f ? toPlayer.Normalized : Vector2.Zero;
            var tangent = new Vector2(-direct.Y, direct.X) * (motion.Orbit * MathF.Sin(motion.Phase));
            var move = (direct + tangent).Normalized;
            hunter.Position = ClampToArena(hunter.Position + (move * motion.Speed * deltaTime), MainScene.HunterSize);

            var collider = hunter.GetComponent<BoxCollider>();
            if (collider is null || !collider.Bounds.Intersects(playerBounds))
                continue;

            if (_player.IsDashing)
            {
                _player.RegisterDashBreak(30 + (Wave * 6));
                _audio.PlayDashBreak();
                RemoveHunter(hunter);
                continue;
            }

            if (_player.TryTakeHit())
            {
                Lives--;
                _audio.PlayHit();
                RemoveHunter(hunter);

                if (Lives <= 0)
                {
                    EndRun();
                    return;
                }

                ShowTransientBanner("Hull Hit", $"{Lives} hull left. Keep moving.", 0.55f);
            }
        }
    }

    private void UpdateHunterSpawns(float deltaTime)
    {
        if (_pickups.Count == 0)
            return;

        _hunterSpawnTimer = Math.Max(0f, _hunterSpawnTimer - deltaTime);
        if (_hunterSpawnTimer > 0f)
            return;

        var maxHunters = Math.Min(2 + (Wave * 2), 12);
        if (_hunters.Count >= maxHunters)
            return;

        SpawnHunter();
        _hunterSpawnTimer = GetHunterSpawnInterval();
    }

    private void SpawnPickups(int count)
    {
        var avoid = _player.Center;
        for (var index = 0; index < count; index++)
        {
            var position = RandomArenaPoint(MainScene.PickupSize, avoid, 100f);
            var pickup = _mainScene.CreatePickup($"Spark_{++_nextPickupId}", position);
            _pickups[pickup.Id] = new PickupMotion
            {
                Anchor = position,
                Angle = (float)(_random.NextDouble() * TwoPi),
                Radius = 5f + ((float)_random.NextDouble() * 8f),
                Speed = 1.6f + ((float)_random.NextDouble() * 2.2f),
                Phase = (float)(_random.NextDouble() * TwoPi)
            };
        }
    }

    private void SpawnHunter()
    {
        var hunter = _mainScene.CreateHunter($"Hunter_{++_nextHunterId}", RandomEdgePoint(MainScene.HunterSize));
        _hunters[hunter.Id] = new HunterMotion
        {
            Speed = 96f + (Wave * 12f) + ((float)_random.NextDouble() * 28f),
            Orbit = 0.18f + ((float)_random.NextDouble() * 0.32f),
            Phase = (float)(_random.NextDouble() * TwoPi)
        };
    }

    private void RemoveHunter(Entity hunter)
    {
        _hunters.Remove(hunter.Id);
        Scene.RemoveEntity(hunter);
    }

    private void ShowTransientBanner(string title, string subtitle, float duration)
    {
        if (GameOver)
            return;

        _bannerTitle = title;
        _bannerSubtitle = subtitle;
        _bannerTimer = duration;
    }

    private float GetHunterSpawnInterval() => Math.Max(0.48f, 1.35f - (Wave * 0.08f));

    private Vector2 RandomArenaPoint(int size, Vector2 avoid, float avoidRadius)
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var x = _random.Next(SideMargin, Engine.Width - SideMargin - size);
            var y = _random.Next(TopMargin, Engine.Height - BottomMargin - size);
            var candidate = new Vector2(x, y);
            var center = candidate + new Vector2(size / 2f, size / 2f);
            if (Vector2.Distance(center, avoid) >= avoidRadius)
                return candidate;
        }

        return new Vector2((Engine.Width - size) / 2f, (Engine.Height - size) / 2f);
    }

    private Vector2 RandomEdgePoint(int size)
    {
        return _random.Next(4) switch
        {
            0 => new Vector2(_random.Next(SideMargin, Engine.Width - SideMargin - size), TopMargin),
            1 => new Vector2(_random.Next(SideMargin, Engine.Width - SideMargin - size), Engine.Height - BottomMargin - size),
            2 => new Vector2(SideMargin, _random.Next(TopMargin, Engine.Height - BottomMargin - size)),
            _ => new Vector2(Engine.Width - SideMargin - size, _random.Next(TopMargin, Engine.Height - BottomMargin - size))
        };
    }

    private Vector2 ClampToArena(Vector2 position, int size)
    {
        position.X = Math.Clamp(position.X, SideMargin, Engine.Width - SideMargin - size);
        position.Y = Math.Clamp(position.Y, TopMargin, Engine.Height - BottomMargin - size);
        return position;
    }

    private void DrawArenaBackdrop()
    {
        var pulse = 0.5f + (0.5f * MathF.Sin(_elapsed * 1.8f));
        var highlight = new Color(96, (byte)(170 + (40f * pulse)), 230, 120);

        for (var index = -40; index < Engine.Width + 80; index += 64)
        {
            var offset = (int)((_elapsed * 26f) % 64f);
            Graphics.DrawLine(
                index + offset,
                TopMargin,
                index - 20 + offset,
                Engine.Height - BottomMargin,
                new Color(24, 54, 78, 72));
        }

        Graphics.DrawRect(
            SideMargin - 6,
            TopMargin - 6,
            Engine.Width - (SideMargin * 2) + 12,
            Engine.Height - TopMargin - BottomMargin + 12,
            highlight);
        Graphics.DrawRect(
            SideMargin,
            TopMargin,
            Engine.Width - (SideMargin * 2),
            Engine.Height - TopMargin - BottomMargin,
            new Color(8, 18, 30, 200));
        Graphics.DrawCircle(
            Engine.Width / 2,
            (Engine.Height - BottomMargin + TopMargin) / 2,
            70 + (Wave * 5) + (int)(12f * MathF.Sin(_elapsed * 2.4f)),
            new Color(64, 140, 210, 56));
    }

    private void DrawPickups()
    {
        foreach (var pickup in Scene.FindAllByTag("pickup"))
        {
            if (!_pickups.TryGetValue(pickup.Id, out var motion))
                continue;

            var pulse = 0.55f + (0.45f * MathF.Sin((_elapsed * 5f) + motion.Phase));
            var center = pickup.Position + new Vector2(MainScene.PickupSize / 2f, MainScene.PickupSize / 2f);
            var halo = 8 + (int)(4f * pulse);

            Graphics.DrawCircle(center, halo + 4, new Color(255, 214, 102, 90));
            Graphics.DrawFilledRect(
                (int)pickup.Position.X,
                (int)pickup.Position.Y,
                MainScene.PickupSize,
                MainScene.PickupSize,
                new Color(255, 206, 86));
            Graphics.DrawRect(
                (int)pickup.Position.X - 1,
                (int)pickup.Position.Y - 1,
                MainScene.PickupSize + 2,
                MainScene.PickupSize + 2,
                new Color(255, 255, 255, 180));
            Graphics.DrawLine(
                (int)center.X - 5,
                (int)center.Y,
                (int)center.X + 5,
                (int)center.Y,
                new Color(110, 52, 0, 200));
            Graphics.DrawLine(
                (int)center.X,
                (int)center.Y - 5,
                (int)center.X,
                (int)center.Y + 5,
                new Color(110, 52, 0, 200));
        }
    }

    private void DrawHunters()
    {
        foreach (var hunter in Scene.FindAllByTag("hunter"))
        {
            if (!_hunters.TryGetValue(hunter.Id, out var motion))
                continue;

            var pulse = 0.5f + (0.5f * MathF.Sin((_elapsed * 8f) + motion.Phase));
            var body = new Color((byte)(200f + (30f * pulse)), (byte)(70f + (60f * pulse)), 90);
            var x = (int)hunter.Position.X;
            var y = (int)hunter.Position.Y;

            Graphics.DrawFilledRect(x, y, MainScene.HunterSize, MainScene.HunterSize, body);
            Graphics.DrawRect(x - 2, y - 2, MainScene.HunterSize + 4, MainScene.HunterSize + 4, new Color(255, 238, 238, 180));
            Graphics.DrawLine(x + 5, y + 8, x + MainScene.HunterSize - 5, y + 8, new Color(20, 10, 16, 220));
            Graphics.DrawCircle(
                x + (MainScene.HunterSize / 2),
                y + MainScene.HunterSize + 4,
                4 + (int)(2f * pulse),
                new Color(255, 90, 90, 80));
        }
    }

    private sealed class PickupMotion
    {
        public Vector2 Anchor { get; set; }
        public float Angle { get; set; }
        public float Radius { get; set; }
        public float Speed { get; set; }
        public float Phase { get; set; }
    }

    private sealed class HunterMotion
    {
        public float Speed { get; set; }
        public float Orbit { get; set; }
        public float Phase { get; set; }
    }
}