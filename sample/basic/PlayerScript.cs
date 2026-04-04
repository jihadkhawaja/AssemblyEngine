using AssemblyEngine.Core;
using AssemblyEngine.Engine;
using AssemblyEngine.Scripting;

namespace SampleGame;

/// <summary>
/// Handles player movement, dash state, and score/combo tracking.
/// </summary>
public sealed class PlayerScript : GameScript
{
    private const float MoveSpeed = 220f;
    private const float DashSpeed = 720f;
    private const float DashDuration = 0.13f;
    private const float DashCooldown = 0.42f;
    private const float HitFlashDuration = 0.75f;
    private const float ComboWindow = 2f;
    private const int SideMargin = 24;
    private const int TopMargin = 56;
    private const int BottomMargin = 72;

    private Entity? _player;
    private GameLoopScript? _loop;
    private float _dashTimer;
    private float _dashCooldown;
    private float _hurtTimer;
    private float _comboTimer;
    private Vector2 _dashDir;

    public const int PlayerSize = 32;
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public bool IsDashing => _dashTimer > 0f;
    public bool DashReady => _dashCooldown <= 0f;
    public float DashCharge => DashReady ? 1f : Math.Clamp(1f - (_dashCooldown / DashCooldown), 0f, 1f);
    public bool IsInvulnerable => _hurtTimer > 0f;
    public bool HasPlayer => _player is not null;
    public Vector2 Position => _player?.Position ?? Vector2.Zero;
    public Vector2 Center => _player is null
        ? Vector2.Zero
        : new Vector2(_player.Position.X + (PlayerSize / 2f), _player.Position.Y + (PlayerSize / 2f));
    public Rectangle Bounds => _player is null
        ? new Rectangle(0, 0, 0, 0)
        : new Rectangle(_player.Position.X, _player.Position.Y, PlayerSize, PlayerSize);

    public override void OnLoad()
    {
        _loop = Engine.Scripts.GetScript<GameLoopScript>();
        ResolvePlayer(resetMotion: true);
        CenterPlayer();
    }

    public override void OnUpdate(float deltaTime)
    {
        ResolvePlayer();

        if (Engine.Scripts.GetScript<SettingsMenuScript>()?.IsOpen == true)
            return;

        UpdateTimers(deltaTime);

        if (_player is null || _loop?.GameOver == true)
            return;

        var velocity = Vector2.Zero;
        if (IsKeyDown(KeyCode.W) || IsKeyDown(KeyCode.Up))
            velocity.Y = -1;
        if (IsKeyDown(KeyCode.S) || IsKeyDown(KeyCode.Down))
            velocity.Y = 1;
        if (IsKeyDown(KeyCode.A) || IsKeyDown(KeyCode.Left))
            velocity.X = -1;
        if (IsKeyDown(KeyCode.D) || IsKeyDown(KeyCode.Right))
            velocity.X = 1;

        if (velocity.LengthSquared > 0f)
            velocity = velocity.Normalized;

        if (IsKeyPressed(KeyCode.Space) && DashReady && velocity.LengthSquared > 0f)
        {
            _dashTimer = DashDuration;
            _dashCooldown = DashCooldown;
            _dashDir = velocity;
        }

        var activeVelocity = velocity;
        var speed = MoveSpeed;
        if (IsDashing)
        {
            activeVelocity = _dashDir;
            speed = DashSpeed;
        }

        _player.Position += activeVelocity * speed * deltaTime;

        var pos = _player.Position;
        pos.X = Math.Clamp(pos.X, SideMargin, Engine.Width - PlayerSize - SideMargin);
        pos.Y = Math.Clamp(pos.Y, TopMargin, Engine.Height - PlayerSize - BottomMargin);
        _player.Position = pos;
    }

    public void ResetRun()
    {
        Score = 0;
        Combo = 0;
        _comboTimer = 0f;
        ResetMotion();
        ResolvePlayer(resetMotion: true);
        CenterPlayer();
    }

    public void BeginWave()
    {
        Combo = 0;
        _comboTimer = 0f;
        ResetMotion();
        ResolvePlayer(resetMotion: true);
        CenterPlayer();
    }

    public void AddScore(int amount) => Score += amount;

    public int RegisterDashBreak(int baseScore)
    {
        Combo++;
        _comboTimer = ComboWindow;
        var awarded = baseScore + (Math.Max(0, Combo - 1) * 8);
        Score += awarded;
        return awarded;
    }

    public void BreakCombo()
    {
        Combo = 0;
        _comboTimer = 0f;
    }

    public bool TryTakeHit()
    {
        if (IsInvulnerable || IsDashing)
            return false;

        _hurtTimer = HitFlashDuration;
        BreakCombo();
        return true;
    }

    public override void OnDraw()
    {
        if (!ResolvePlayer())
            return;

        var pos = Position;

        if (IsDashing)
        {
            var trail = pos - (_dashDir * 12f);
            Graphics.DrawFilledRect(
                (int)trail.X,
                (int)trail.Y,
                PlayerSize,
                PlayerSize,
                new Color(80, 255, 220, 110));
        }

        if (IsInvulnerable && ((int)(_hurtTimer * 20f) % 2 == 0))
            return;

        var color = IsDashing
            ? new Color(255, 247, 138)
            : IsInvulnerable ? new Color(255, 120, 120) : new Color(110, 240, 255);

        Graphics.DrawFilledRect(
            (int)pos.X,
            (int)pos.Y,
            PlayerSize,
            PlayerSize,
            color);
        Graphics.DrawRect(
            (int)pos.X - 2,
            (int)pos.Y - 2,
            PlayerSize + 4,
            PlayerSize + 4,
            new Color(255, 255, 255, 160));
        Graphics.DrawLine(
            (int)Center.X,
            (int)pos.Y + 4,
            (int)Center.X,
            (int)pos.Y + PlayerSize - 4,
            new Color(8, 18, 26, 180));
        Graphics.DrawLine(
            (int)pos.X + 4,
            (int)Center.Y,
            (int)pos.X + PlayerSize - 4,
            (int)Center.Y,
            new Color(8, 18, 26, 180));
    }

    private void UpdateTimers(float deltaTime)
    {
        if (_dashTimer > 0f)
            _dashTimer = Math.Max(0f, _dashTimer - deltaTime);

        if (_dashCooldown > 0f)
            _dashCooldown = Math.Max(0f, _dashCooldown - deltaTime);

        if (_hurtTimer > 0f)
            _hurtTimer = Math.Max(0f, _hurtTimer - deltaTime);

        if (_comboTimer > 0f)
        {
            _comboTimer = Math.Max(0f, _comboTimer - deltaTime);
            if (_comboTimer <= 0f)
                BreakCombo();
        }
    }

    private bool ResolvePlayer(bool resetMotion = false)
    {
        var player = Scene.FindByName("Player");
        if (player is null)
        {
            _player = null;
            return false;
        }

        if (!ReferenceEquals(player, _player))
        {
            _player = player;
            ResetMotion();
        }

        if (resetMotion)
            ResetMotion();

        return true;
    }

    private void ResetMotion()
    {
        _dashTimer = 0f;
        _dashCooldown = 0f;
        _hurtTimer = 0f;
        _dashDir = Vector2.Zero;
    }

    private void CenterPlayer()
    {
        if (_player is null)
            return;

        _player.Position = new Vector2(
            (Engine.Width - PlayerSize) / 2f,
            (Engine.Height - PlayerSize) / 2f);
    }
}
