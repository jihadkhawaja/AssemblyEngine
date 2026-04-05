using AssemblyEngine.Core;
using AssemblyEngine.Rendering;
using AssemblyEngine.Scripting;
using CoreVector2 = AssemblyEngine.Core.Vector2;
using Matrix4x4 = System.Numerics.Matrix4x4;
using NumericVector3 = System.Numerics.Vector3;

namespace FpsSample;

public sealed class FpsGameScript : GameScript
{
    private const float PlayerRadius = 0.38f;
    private const float EyeHeight = 1.05f;
    private const float WalkSpeed = 4.8f;
    private const float SprintSpeed = 6.9f;
    private const float TurnSpeed = 1.9f;
    private const float MouseSensitivity = 0.0095f;
    private const float PitchLimit = 0.72f;
    private const float ShotCooldownDuration = 0.18f;
    private const float ShotRange = 28f;
    private const float EnemyRadius = 0.46f;
    private const float EnemyTouchRange = 1.2f;
    private const float EnemyAttackInterval = 0.82f;
    private const float HurtCooldownDuration = 0.75f;
    private const float MuzzleFlashDuration = 0.08f;
    private const float HitConfirmDuration = 0.14f;
    private const int StartingHealth = 100;
    private const int ContactDamage = 12;

    private readonly Random _random = new();
    private readonly List<FpsEnemy> _enemies = [];
    private FpsArenaMap _arena = null!;
    private NumericVector3 _playerPosition;
    private CoreVector2 _lastMousePosition;
    private float _yaw;
    private float _pitch;
    private float _shotCooldown;
    private float _hurtCooldown;
    private float _messageTimer;
    private float _muzzleFlashTimer;
    private float _hitConfirmTimer;
    private float _missionTimer;
    private bool _extractionNotified;
    private bool _hasMouseSample;
    private string _messageTitle = string.Empty;
    private string _messageSubtitle = string.Empty;

    public int Health { get; private set; }

    public int ShotsFired { get; private set; }

    public int ShotsHit { get; private set; }

    public bool HelpVisible { get; private set; } = true;

    public bool MissionComplete { get; private set; }

    public bool GameOver { get; private set; }

    public float MissionTime => _missionTimer;

    public int EnemiesRemaining => _enemies.Count(enemy => enemy.Alive);

    public float Accuracy => ShotsFired == 0 ? 0f : (float)ShotsHit / ShotsFired;

    public string BackendLabel => Graphics.Backend == GraphicsBackend.Vulkan ? "Vulkan" : "Software";

    public bool ShowCenterMessage => GameOver || MissionComplete || _messageTimer > 0f;

    public string MessageTitle => ShowCenterMessage ? _messageTitle : string.Empty;

    public string MessageSubtitle => ShowCenterMessage ? _messageSubtitle : string.Empty;

    public string ObjectiveText
    {
        get
        {
            if (GameOver)
                return "Hull lost. Press R or Enter to redeploy into the arena.";

            if (MissionComplete)
                return "Citadel secure. Press R or Enter to start another run.";

            if (EnemiesRemaining > 0)
            {
                return EnemiesRemaining == 1
                    ? "One drone remains. Sweep the outpost before extraction unlocks."
                    : $"Sweep the outpost. {EnemiesRemaining} drones remain before extraction unlocks.";
            }

            return "Extraction beacon is online. Move into the green gate to finish the run.";
        }
    }

    public string HintText =>
        "WASD move | Mouse or arrows look | Left mouse or Space fire | Shift sprint | F1 help";

    public override void OnLoad()
    {
        _arena = FpsArenaMap.CreateDefault();
        StartRun();
    }

    public override void OnUpdate(float deltaTime)
    {
        if (IsKeyPressed(KeyCode.F1))
            HelpVisible = !HelpVisible;

        UpdateTimers(deltaTime);

        if (GameOver || MissionComplete)
        {
            CaptureMouseSample();
            if (IsKeyPressed(KeyCode.R) || IsKeyPressed(KeyCode.Enter))
                StartRun();

            return;
        }

        _missionTimer += deltaTime;
        UpdateLook(deltaTime);
        UpdateMovement(deltaTime);
        HandleShooting();
        UpdateEnemies(deltaTime);
        CheckExtraction();
    }

    public override void OnDraw()
    {
        DrawWorld();
        DrawWeapon();
        DrawCrosshair();
        DrawScreenFx();
    }

    private void StartRun()
    {
        _enemies.Clear();
        foreach (var spawn in _arena.EnemySpawns)
        {
            _enemies.Add(new FpsEnemy(
                spawn,
                1.9f + ((float)_random.NextDouble() * 0.65f),
                (float)(_random.NextDouble() * MathF.Tau)));
        }

        _playerPosition = _arena.PlayerSpawn;
        _yaw = MathF.PI * 0.75f;
        _pitch = -0.06f;
        _lastMousePosition = MousePosition;
        _shotCooldown = 0f;
        _hurtCooldown = 0f;
        _messageTimer = 4f;
        _muzzleFlashTimer = 0f;
        _hitConfirmTimer = 0f;
        _missionTimer = 0f;
        _extractionNotified = false;
        _hasMouseSample = false;
        _messageTitle = "Citadel Breach";
        _messageSubtitle = "Sweep the drones, then push through the green extraction gate.";
        Health = StartingHealth;
        ShotsFired = 0;
        ShotsHit = 0;
        MissionComplete = false;
        GameOver = false;
        HelpVisible = true;
    }

    private void UpdateTimers(float deltaTime)
    {
        _shotCooldown = Math.Max(0f, _shotCooldown - deltaTime);
        _hurtCooldown = Math.Max(0f, _hurtCooldown - deltaTime);
        _muzzleFlashTimer = Math.Max(0f, _muzzleFlashTimer - deltaTime);
        _hitConfirmTimer = Math.Max(0f, _hitConfirmTimer - deltaTime);

        if (!GameOver && !MissionComplete)
            _messageTimer = Math.Max(0f, _messageTimer - deltaTime);
    }

    private void UpdateLook(float deltaTime)
    {
        var mouse = MousePosition;
        if (!_hasMouseSample)
        {
            _lastMousePosition = mouse;
            _hasMouseSample = true;
        }
        else
        {
            var deltaX = Math.Clamp(mouse.X - _lastMousePosition.X, -48f, 48f);
            var deltaY = Math.Clamp(mouse.Y - _lastMousePosition.Y, -48f, 48f);
            _lastMousePosition = mouse;
            _yaw += deltaX * MouseSensitivity;
            _pitch = Math.Clamp(_pitch - (deltaY * MouseSensitivity * 0.75f), -PitchLimit, PitchLimit);
        }

        var turnInput = 0f;
        if (IsKeyDown(KeyCode.Left))
            turnInput -= 1f;
        if (IsKeyDown(KeyCode.Right))
            turnInput += 1f;

        if (turnInput != 0f)
            _yaw += turnInput * TurnSpeed * deltaTime;
    }

    private void UpdateMovement(float deltaTime)
    {
        var forwardInput = 0f;
        if (IsKeyDown(KeyCode.W) || IsKeyDown(KeyCode.Up))
            forwardInput += 1f;
        if (IsKeyDown(KeyCode.S) || IsKeyDown(KeyCode.Down))
            forwardInput -= 1f;

        var strafeInput = 0f;
        if (IsKeyDown(KeyCode.D))
            strafeInput += 1f;
        if (IsKeyDown(KeyCode.A))
            strafeInput -= 1f;

        var forward = GetGroundForward();
        var right = new NumericVector3(-forward.Z, 0f, forward.X);
        var movement = (forward * forwardInput) + (right * strafeInput);
        if (movement.LengthSquared() > 1f)
            movement = NumericVector3.Normalize(movement);

        var speed = IsKeyDown(KeyCode.Shift) ? SprintSpeed : WalkSpeed;
        MoveBody(ref _playerPosition, movement * speed * deltaTime, PlayerRadius);
    }

    private void HandleShooting()
    {
        if (_shotCooldown > 0f)
            return;

        if (!IsMouseDown(MouseButton.Left) && !IsKeyPressed(KeyCode.Space))
            return;

        _shotCooldown = ShotCooldownDuration;
        _muzzleFlashTimer = MuzzleFlashDuration;
        ShotsFired++;

        var origin = CameraPosition;
        var direction = GetLookForward();
        var maxDistance = ShotRange;

        foreach (var block in _arena.SolidBlocks)
        {
            if (FpsCollision.TryRayBlock(origin, direction, block, maxDistance, out var distance))
                maxDistance = Math.Min(maxDistance, distance);
        }

        FpsEnemy? hitEnemy = null;
        var enemyDistance = maxDistance;
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive)
                continue;

            var center = enemy.Position + new NumericVector3(0f, 0.85f, 0f);
            if (!FpsCollision.TryRaySphere(origin, direction, center, EnemyRadius, maxDistance, out var distance))
                continue;

            if (distance < enemyDistance)
            {
                enemyDistance = distance;
                hitEnemy = enemy;
            }
        }

        if (hitEnemy is null)
            return;

        hitEnemy.Alive = false;
        ShotsHit++;
        _hitConfirmTimer = HitConfirmDuration;

        if (EnemiesRemaining == 0)
            ActivateExtraction();
    }

    private void UpdateEnemies(float deltaTime)
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive)
                continue;

            enemy.HoverPhase += deltaTime * 3.2f;
            enemy.AttackCooldown = Math.Max(0f, enemy.AttackCooldown - deltaTime);

            var toPlayer = _playerPosition - enemy.Position;
            toPlayer.Y = 0f;
            var distance = toPlayer.Length();

            if (distance > EnemyTouchRange)
            {
                var direction = distance > 0.001f ? toPlayer / distance : NumericVector3.Zero;
                var tangent = new NumericVector3(-direction.Z, 0f, direction.X);
                var drift = tangent * (0.22f * MathF.Sin((_missionTimer * 1.4f) + enemy.HoverPhase));
                var desired = direction + drift;

                if (desired.LengthSquared() > 0.001f)
                {
                    var position = enemy.Position;
                    MoveBody(ref position, NumericVector3.Normalize(desired) * enemy.Speed * deltaTime, EnemyRadius);
                    enemy.Position = position;
                }
            }
            else if (enemy.AttackCooldown <= 0f && _hurtCooldown <= 0f)
            {
                Health = Math.Max(0, Health - ContactDamage);
                _hurtCooldown = HurtCooldownDuration;
                enemy.AttackCooldown = EnemyAttackInterval;
                ShowTransientMessage("Hull Impact", $"{Health} integrity left. Keep moving.", 0.55f);

                if (Health <= 0)
                    LoseRun();
            }
        }
    }

    private void CheckExtraction()
    {
        if (EnemiesRemaining > 0)
            return;

        if (NumericVector3.Distance(_playerPosition, _arena.ExitPosition) > FpsArenaMap.ExitTriggerRadius)
            return;

        MissionComplete = true;
        HelpVisible = false;
        _messageTitle = "Citadel Secure";
        _messageSubtitle = $"Time {_missionTimer:0.0}s | Accuracy {Accuracy * 100f:0}% | Press R or Enter to rerun.";
    }

    private void ActivateExtraction()
    {
        if (_extractionNotified)
            return;

        _extractionNotified = true;
        ShowTransientMessage("Extraction Online", "The gate is green. Push into the beacon to finish the run.", 2.8f);
    }

    private void LoseRun()
    {
        GameOver = true;
        HelpVisible = false;
        _messageTitle = "Run Failed";
        _messageSubtitle = $"Accuracy {Accuracy * 100f:0}% | Press R or Enter to redeploy.";
    }

    private void ShowTransientMessage(string title, string subtitle, float duration)
    {
        if (GameOver || MissionComplete)
            return;

        _messageTitle = title;
        _messageSubtitle = subtitle;
        _messageTimer = duration;
    }

    private void DrawWorld()
    {
        var cameraPosition = CameraPosition;
        Graphics.SetCamera(new Camera3D
        {
            Position = cameraPosition,
            Target = cameraPosition + GetLookForward(),
            FieldOfView = 1.16f,
            NearPlane = 0.05f,
            FarPlane = 90f
        });

        DrawFloor();
        DrawArenaEnvelope();
        DrawSolidBlocks();
        DrawExitBeacon();
        DrawEnemies();
        Graphics.ResetCamera();
    }

    private void DrawFloor()
    {
        Graphics.DrawCube(
            CreateTransform(new NumericVector3(0f, -0.2f, 0f), new NumericVector3(_arena.Width + 1.8f, 0.34f, _arena.Depth + 1.8f)),
            new Color(7, 16, 24));

        foreach (var panel in _arena.FloorPanels)
        {
            var pulse = 0.5f + (0.5f * MathF.Sin((_missionTimer * 0.8f) + (panel.X * 0.14f) + (panel.Z * 0.08f)));
            var color = new Color(
                (byte)(16f + (10f * pulse)),
                (byte)(48f + (22f * pulse)),
                (byte)(68f + (32f * pulse)),
                200);

            Graphics.DrawCube(
                CreateTransform(panel + new NumericVector3(0f, -0.045f, 0f), new NumericVector3(FpsArenaMap.CellSize * 0.72f, 0.05f, FpsArenaMap.CellSize * 0.72f)),
                color);
        }
    }

    private void DrawArenaEnvelope()
    {
        Graphics.DrawCube(
            CreateTransform(new NumericVector3(0f, 1.45f, 0f), new NumericVector3(_arena.Width + 0.8f, 3.2f, _arena.Depth + 0.8f)),
            new Color(76, 198, 222, 52),
            wireframe: true);
    }

    private void DrawSolidBlocks()
    {
        foreach (var block in _arena.SolidBlocks)
        {
            var transform = CreateTransform(block.Center, block.Scale);
            Graphics.DrawCube(transform, block.Color);

            if (block.Scale.Y < FpsArenaMap.WallHeight)
            {
                Graphics.DrawCube(
                    CreateTransform(block.Center + new NumericVector3(0f, 0.04f, 0f), block.Scale + new NumericVector3(0.12f, 0.08f, 0.12f)),
                    new Color(245, 214, 170, 72),
                    wireframe: true);
            }
        }
    }

    private void DrawExitBeacon()
    {
        var active = EnemiesRemaining == 0;
        var pulse = 0.5f + (0.5f * MathF.Sin(_missionTimer * 4.2f));
        var frameColor = active
            ? new Color(96, 255, (byte)(170f + (50f * pulse)), 200)
            : new Color(84, 126, 150, 116);

        Graphics.DrawCube(
            CreateTransform(_arena.ExitPosition + new NumericVector3(0f, 0.16f, 0f), new NumericVector3(1.9f, 0.32f, 1.9f)),
            new Color(18, 34, 44));
        Graphics.DrawCube(
            CreateTransform(_arena.ExitPosition + new NumericVector3(-0.72f, 1.02f, 0f), new NumericVector3(0.24f, 2.04f, 0.24f)),
            frameColor);
        Graphics.DrawCube(
            CreateTransform(_arena.ExitPosition + new NumericVector3(0.72f, 1.02f, 0f), new NumericVector3(0.24f, 2.04f, 0.24f)),
            frameColor);
        Graphics.DrawCube(
            CreateTransform(_arena.ExitPosition + new NumericVector3(0f, 1.92f, 0f), new NumericVector3(1.74f, 0.24f, 0.24f)),
            frameColor);
        Graphics.DrawCube(
            CreateTransform(_arena.ExitPosition + new NumericVector3(0f, 1.16f, 0f), new NumericVector3(1.26f + (active ? pulse * 0.34f : 0f), 2.18f, 1.26f + (active ? pulse * 0.34f : 0f))),
            frameColor,
            wireframe: true);
    }

    private void DrawEnemies()
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive)
                continue;

            var hover = 0.08f * MathF.Sin((_missionTimer * 5f) + enemy.HoverPhase);
            var distance = NumericVector3.Distance(_playerPosition, enemy.Position);
            var charge = 1f - Math.Clamp((distance - 1.2f) / 8f, 0f, 1f);
            var bodyColor = new Color(
                (byte)(164f + (62f * charge)),
                (byte)(56f + (48f * charge)),
                (byte)(68f + (30f * charge)));
            var headColor = new Color(240, 228, 188);
            var center = enemy.Position + new NumericVector3(0f, 0.76f + hover, 0f);

            Graphics.DrawCube(CreateTransform(center, new NumericVector3(0.86f, 1.18f, 0.86f)), bodyColor);
            Graphics.DrawCube(CreateTransform(center + new NumericVector3(0f, 0.92f, 0f), new NumericVector3(0.46f, 0.46f, 0.46f)), headColor);
            Graphics.DrawCube(CreateTransform(center, new NumericVector3(1.02f, 1.34f, 1.02f)), new Color(255, 194, 120, 96), wireframe: true);
        }
    }

    private void DrawWeapon()
    {
        var baseX = Engine.Width - 264;
        var baseY = Engine.Height - 148;
        var bodyColor = MissionComplete
            ? new Color(96, 255, 176)
            : GameOver ? new Color(224, 110, 110) : new Color(108, 226, 255);

        Graphics.DrawFilledRect(baseX, baseY + 44, 186, 70, new Color(10, 18, 28, 228));
        Graphics.DrawFilledRect(baseX + 22, baseY + 12, 82, 104, bodyColor);
        Graphics.DrawFilledRect(baseX + 96, baseY + 34, 116, 28, new Color(231, 173, 82));
        Graphics.DrawFilledRect(baseX + 122, baseY + 24, 46, 18, new Color(246, 226, 170));
        Graphics.DrawRect(baseX + 22, baseY + 12, 82, 104, new Color(214, 244, 255, 180));
        Graphics.DrawRect(baseX, baseY + 44, 186, 70, new Color(120, 176, 210, 120));

        if (_muzzleFlashTimer > 0f)
        {
            var flash = (byte)(120f + (120f * (_muzzleFlashTimer / MuzzleFlashDuration)));
            Graphics.DrawCircle(baseX + 196, baseY + 48, 18, new Color(255, 220, 110, flash));
            Graphics.DrawCircle(baseX + 210, baseY + 48, 10, new Color(255, 247, 198, flash));
        }
    }

    private void DrawCrosshair()
    {
        var centerX = Engine.Width / 2;
        var centerY = Engine.Height / 2;
        var color = _hitConfirmTimer > 0f
            ? new Color(255, 214, 98)
            : EnemiesRemaining == 0 ? new Color(110, 255, 180) : new Color(236, 246, 255);

        Graphics.DrawLine(centerX - 11, centerY, centerX - 3, centerY, color);
        Graphics.DrawLine(centerX + 3, centerY, centerX + 11, centerY, color);
        Graphics.DrawLine(centerX, centerY - 11, centerX, centerY - 3, color);
        Graphics.DrawLine(centerX, centerY + 3, centerX, centerY + 11, color);
    }

    private void DrawScreenFx()
    {
        if (_hurtCooldown > 0f)
        {
            var alpha = (byte)(38f * Math.Clamp(_hurtCooldown / HurtCooldownDuration, 0f, 1f));
            Graphics.DrawFilledRect(0, 0, Engine.Width, Engine.Height, new Color(120, 16, 16, alpha));
        }
    }

    private NumericVector3 CameraPosition
    {
        get
        {
            var bobStrength = 0.028f * (IsPlayerMoving() ? 1f : 0.35f);
            var bob = MathF.Sin(_missionTimer * 8.5f) * bobStrength;
            return _playerPosition + new NumericVector3(0f, EyeHeight + bob, 0f);
        }
    }

    private NumericVector3 GetLookForward()
    {
        var cosPitch = MathF.Cos(_pitch);
        return NumericVector3.Normalize(new NumericVector3(
            MathF.Sin(_yaw) * cosPitch,
            MathF.Sin(_pitch),
            -MathF.Cos(_yaw) * cosPitch));
    }

    private NumericVector3 GetGroundForward() =>
        NumericVector3.Normalize(new NumericVector3(MathF.Sin(_yaw), 0f, -MathF.Cos(_yaw)));

    private bool IsPlayerMoving()
    {
        return IsKeyDown(KeyCode.W) || IsKeyDown(KeyCode.Up) ||
            IsKeyDown(KeyCode.S) || IsKeyDown(KeyCode.Down) ||
            IsKeyDown(KeyCode.A) || IsKeyDown(KeyCode.D);
    }

    private void CaptureMouseSample()
    {
        _lastMousePosition = MousePosition;
        _hasMouseSample = true;
    }

    private void MoveBody(ref NumericVector3 position, NumericVector3 delta, float radius)
    {
        if (MathF.Abs(delta.X) > 0.0001f)
        {
            var proposed = position + new NumericVector3(delta.X, 0f, 0f);
            if (!Collides(proposed, radius))
                position = proposed;
        }

        if (MathF.Abs(delta.Z) > 0.0001f)
        {
            var proposed = position + new NumericVector3(0f, 0f, delta.Z);
            if (!Collides(proposed, radius))
                position = proposed;
        }
    }

    private bool Collides(NumericVector3 position, float radius)
    {
        foreach (var block in _arena.SolidBlocks)
        {
            if (FpsCollision.CircleIntersectsBlockXZ(position, radius, block))
                return true;
        }

        return false;
    }

    private static Matrix4x4 CreateTransform(NumericVector3 center, NumericVector3 scale) =>
        Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(center);
}