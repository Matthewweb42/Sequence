using Godot;
using Sequence.Entities.Projectiles;

namespace Sequence.Entities.Enemies.Boss;

/// <summary>
/// The Sequence Warden — a 3-phase corrupted knight boss.
/// Phase 1: melee only.
/// Phase 2 (≤66% HP): adds 1 ranged orb per attack cycle, speeds up slightly.
/// Phase 3 (≤33% HP): 3-spread orb burst per cycle, faster again.
/// </summary>
public partial class SequenceWarden : EnemyBase
{
    [Export] public PackedScene? BossProjectileScene { get; set; }

    private BossPhaseComponent? _phase;
    private int _currentPhase = 1;
    private bool _rangedEnabled;
    private bool _spreadEnabled;

    // Invulnerability window on phase transition.
    private float _invulnRemaining;
    private const float InvulnDuration = 0.4f;

    // Screen flash (CanvasLayer added in _Ready).
    private ColorRect? _flashRect;
    private float _bossFlashRemaining = -1f;
    private float _bossFlashDuration = 0.6f;
    private Color _bossFlashColor;

    // Phase pulse animation.
    private float _pulseTime;

    public override void _Ready()
    {
        base._Ready();

        _phase = GetNodeOrNull<BossPhaseComponent>("BossPhaseComponent");
        if (_phase != null)
            _phase.BossPhaseChanged += OnPhaseChanged;

        BuildFlashRect();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_phase != null)
            _phase.BossPhaseChanged -= OnPhaseChanged;
    }

    // Called every physics frame.
    public override void _PhysicsProcess(double delta)
    {
        if (_invulnRemaining > 0f)
            _invulnRemaining -= (float)delta;

        base._PhysicsProcess(delta);
        TickBossFlash((float)delta);
        TickPhasePulse((float)delta);
    }

    // -----------------------------------------------------------------------
    // Phase transition
    // -----------------------------------------------------------------------

    private void OnPhaseChanged(int newPhase)
    {
        _currentPhase = newPhase;

        if (newPhase == 2)
        {
            MoveSpeed = 110f;
            _rangedEnabled = true;
            TriggerBossFlash(new Color(1f, 0.45f, 0f, 0.45f));
            StartInvuln();
        }
        else if (newPhase == 3)
        {
            MoveSpeed = 130f;
            AttackCooldownSeconds = 1.2f;
            _spreadEnabled = true;
            TriggerBossFlash(new Color(1f, 0.1f, 0.1f, 0.50f));
            StartInvuln();
        }
    }

    private void StartInvuln()
    {
        _invulnRemaining = InvulnDuration;
        var hurtbox = GetNodeOrNull<Sequence.Components.Hurtbox.HurtboxComponent>("HurtboxComponent");
        if (hurtbox != null)
        {
            hurtbox.Monitoring = false;
            GetTree().CreateTimer(InvulnDuration).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(hurtbox))
                    hurtbox.Monitoring = true;
            };
        }
    }

    // -----------------------------------------------------------------------
    // Attack hooks (override from EnemyBase)
    // -----------------------------------------------------------------------

    protected override void OnAttackLanded()
    {
        // Base handles melee hitbox activation (already called before this).
        if (_spreadEnabled)
        {
            // Phase 3: 3-spread burst at -20, 0, +20 degrees.
            SpawnBossProjectile(-20f);
            SpawnBossProjectile(0f);
            SpawnBossProjectile(20f);
        }
        else if (_rangedEnabled)
        {
            // Phase 2: single orb straight at player.
            SpawnBossProjectile(0f);
        }
    }

    // -----------------------------------------------------------------------
    // Projectile spawning
    // -----------------------------------------------------------------------

    private void SpawnBossProjectile(float angleDegrees)
    {
        if (BossProjectileScene == null) return;
        var root = GetTree()?.CurrentScene;
        if (root == null) return;

        var target = AggroTarget;
        Vector2 baseDir;
        if (target != null && GodotObject.IsInstanceValid(target))
        {
            baseDir = (target.GlobalPosition - GlobalPosition).Normalized();
        }
        else
        {
            baseDir = _sprite != null && _sprite.FlipH ? Vector2.Left : Vector2.Right;
        }

        var dir = baseDir.Rotated(Mathf.DegToRad(angleDegrees));

        var proj = BossProjectileScene.Instantiate<ProjectileController>();
        proj.Direction = dir;
        root.CallDeferred(Node.MethodName.AddChild, proj);
        // Position set after AddChild to ensure GlobalPosition works.
        proj.CallDeferred(Node2D.MethodName.SetGlobalPosition, GlobalPosition);
    }

    // -----------------------------------------------------------------------
    // Phase pulse visuals
    // -----------------------------------------------------------------------

    private void TickPhasePulse(float step)
    {
        if (_currentPhase < 2 || _sprite == null) return;
        if (_flashRemaining > 0f) return; // damage flash takes priority

        _pulseTime += step;

        float freq = _currentPhase == 3 ? 3.5f : 2.0f;
        float pulse = (Mathf.Sin(_pulseTime * freq) + 1f) * 0.5f; // 0..1

        Color tint;
        if (_currentPhase == 3)
        {
            // Red pulse
            tint = new Color(
                Mathf.Lerp(1f, 1.8f, pulse),
                Mathf.Lerp(0.5f, 0.2f, pulse),
                Mathf.Lerp(0.5f, 0.2f, pulse),
                1f);
        }
        else
        {
            // Orange pulse
            tint = new Color(
                Mathf.Lerp(1f, 1.6f, pulse),
                Mathf.Lerp(0.7f, 0.45f, pulse),
                Mathf.Lerp(0.5f, 0.1f, pulse),
                1f);
        }

        _sprite.Modulate = tint;
    }

    // -----------------------------------------------------------------------
    // Screen flash helpers
    // -----------------------------------------------------------------------

    private void BuildFlashRect()
    {
        _flashRect = new ColorRect();
        _flashRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _flashRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _flashRect.Color = new Color(0f, 0f, 0f, 0f);
        _flashRect.ZIndex = 100;

        var layer = new CanvasLayer();
        layer.Layer = 9;
        layer.AddChild(_flashRect);
        AddChild(layer);
    }

    private void TriggerBossFlash(Color color)
    {
        _bossFlashColor = color;
        _bossFlashDuration = 0.6f;
        _bossFlashRemaining = 0.6f;
        if (_flashRect != null) _flashRect.Color = color;
    }

    private void TickBossFlash(float delta)
    {
        if (_bossFlashRemaining < 0f || _flashRect == null) return;
        _bossFlashRemaining -= delta;
        if (_bossFlashRemaining <= 0f)
        {
            _bossFlashRemaining = -1f;
            _flashRect.Color = new Color(0f, 0f, 0f, 0f);
            return;
        }
        var alpha = (_bossFlashRemaining / _bossFlashDuration) * _bossFlashColor.A;
        _flashRect.Color = new Color(_bossFlashColor.R, _bossFlashColor.G, _bossFlashColor.B, alpha);
    }
}
