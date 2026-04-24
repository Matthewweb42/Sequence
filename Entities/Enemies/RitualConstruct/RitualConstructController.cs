using Godot;

namespace Sequence.Entities.Enemies.RitualConstruct;

/// <summary>
/// Stationary turret enemy. Fires an expanding AoE ring pulse every 3 seconds.
/// Does not chase or jump.
/// </summary>
public partial class RitualConstructController : EnemyBase
{
    private const float PulseInterval = 3.0f;
    private const float PulseExpandDuration = 0.5f;
    private const float PulseStartRadius = 20f;
    private const float PulseEndRadius = 160f;

    [Export] public NodePath? AoEHitboxPath { get; set; }

    private float _pulseTimer;
    private bool _formulaDropped;
    private Sequence.Components.Hitbox.HitboxComponent? _aoEHitbox;
    private CollisionShape2D? _aoEShape;
    private Tween? _activeTween;

    protected override bool CanJump => false;

    public override void _Ready()
    {
        base._Ready();
        _pulseTimer = PulseInterval;

        if (AoEHitboxPath != null && !AoEHitboxPath.IsEmpty)
            _aoEHitbox = GetNodeOrNull<Sequence.Components.Hitbox.HitboxComponent>(AoEHitboxPath);
        else
            _aoEHitbox = GetNodeOrNull<Sequence.Components.Hitbox.HitboxComponent>("AoEHitboxComponent");

        if (_aoEHitbox != null)
            _aoEShape = _aoEHitbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

        if (_health != null)
            _health.Died += OnDiedForFormulaDrop;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_health != null)
            _health.Died -= OnDiedForFormulaDrop;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Override completely: no movement, only pulse timer
        if (_health != null && _health.IsDead)
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        if (!IsOnFloor())
            Velocity += GetGravity() * (float)delta;
        else
            Velocity = new Vector2(0f, Velocity.Y);

        _pulseTimer -= (float)delta;
        if (_pulseTimer <= 0f)
        {
            _pulseTimer = PulseInterval;
            TriggerPulse();
        }

        MoveAndSlide();
    }

    protected override void UpdateChase(Node2D? target)
    {
        // Stationary — no chase movement
        Velocity = new Vector2(0f, Velocity.Y);
    }

    private void TriggerPulse()
    {
        if (_aoEHitbox == null || _aoEShape == null) return;

        _activeTween?.Kill();

        // Reset shape to starting radius
        if (_aoEShape.Shape is CircleShape2D circle)
        {
            circle.Radius = PulseStartRadius;
        }

        _aoEHitbox.ActivateWindow();

        _activeTween = CreateTween();
        if (_aoEShape.Shape is CircleShape2D circleShape)
        {
            _activeTween.TweenProperty(circleShape, "radius", PulseEndRadius, PulseExpandDuration);
        }
        _activeTween.TweenCallback(Callable.From(() =>
        {
            _aoEHitbox.DeactivateWindow();
            if (_aoEShape?.Shape is CircleShape2D c)
                c.Radius = PulseStartRadius;
        }));
    }

    private void OnDiedForFormulaDrop(Node? _)
    {
        if (_formulaDropped) return;
        _formulaDropped = true;

        var player = Autoloads.RunManager.Instance?.Player;
        var inventory = player?.GetNodeOrNull<Components.Inventory.InventoryComponent>("InventoryComponent");
        inventory?.DiscoverFormula("formula_s7");
    }
}
