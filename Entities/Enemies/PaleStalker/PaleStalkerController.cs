using Godot;

namespace Sequence.Entities.Enemies.PaleStalker;

/// <summary>
/// Stealth ambusher. Invisible until 120px. Pauses, fizzles visible, then lunges.
/// Re-enters invisibility after recovery if player leaves aggro radius.
/// </summary>
public partial class PaleStalkerController : EnemyBase
{
    private const float InvisibleAlpha = 0f;
    private const float VisibleAlpha = 1f;
    private const float FizzleDuration = 0.3f;
    private const float PauseDuration = 0.4f;
    private const float LungeSpeed = 700f;
    private const float LungeDistance = 160f;
    private const float LungeRecoverySeconds = 1.5f;

    private Sprite2D? _sprite;
    private float _pauseRemaining;
    private float _fizzleElapsed;
    private bool _isFizzling;
    private bool _isLunging;
    private bool _inRecovery;
    private float _lungeDistanceRemaining;
    private float _lungeRecoveryRemaining;
    private float _lungeDirX;
    private bool _formulaDropped;

    protected override bool CanJump => false;

    public override void _Ready()
    {
        base._Ready();
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        SetInvisible();

        _fsm?.RegisterState(new EnemyBase.EnemySimpleState("AmbushPause"));
        _fsm?.RegisterState(new EnemyBase.EnemySimpleState("Fizzle"));
        _fsm?.RegisterState(new EnemyBase.EnemySimpleState("Lunge"));
        _fsm?.RegisterState(new EnemyBase.EnemySimpleState("LungeRecovery"));

        if (_aggro != null)
        {
            _aggro.AggroAcquired += OnAggroAcquiredStalker;
            _aggro.AggroLost += OnAggroLostStalker;
        }

        if (_health != null)
            _health.Died += OnDiedForFormulaDrop;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_aggro != null)
        {
            _aggro.AggroAcquired -= OnAggroAcquiredStalker;
            _aggro.AggroLost -= OnAggroLostStalker;
        }
        if (_health != null)
            _health.Died -= OnDiedForFormulaDrop;
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;

        if (_health != null && _health.IsDead)
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        if (!IsOnFloor())
            Velocity += GetGravity() * step;

        // Ambush pause
        if (_pauseRemaining > 0f)
        {
            _pauseRemaining -= step;
            if (_pauseRemaining <= 0f)
            {
                _pauseRemaining = 0f;
                StartFizzle();
            }
            Velocity = new Vector2(0f, Velocity.Y);
            MoveAndSlide();
            return;
        }

        // Fizzle-in
        if (_isFizzling)
        {
            _fizzleElapsed += step;
            var t = Mathf.Clamp(_fizzleElapsed / FizzleDuration, 0f, 1f);
            SetAlpha(t);
            if (_fizzleElapsed >= FizzleDuration)
            {
                _isFizzling = false;
                SetAlpha(1f);
                StartLunge();
            }
            Velocity = new Vector2(0f, Velocity.Y);
            MoveAndSlide();
            return;
        }

        // Active lunge
        if (_isLunging)
        {
            if (_lungeDistanceRemaining > 0f)
            {
                var move = LungeSpeed * step;
                _lungeDistanceRemaining -= move;
                Velocity = new Vector2(_lungeDirX * LungeSpeed, Velocity.Y);
            }
            else
            {
                EndLunge();
            }
            MoveAndSlide();
            return;
        }

        // Recovery
        if (_inRecovery)
        {
            _lungeRecoveryRemaining -= step;
            if (_lungeRecoveryRemaining <= 0f)
            {
                _inRecovery = false;
                SetInvisible();
                _fsm?.QueueTransition(_aggro?.HasTarget == true ? "Chase" : "Idle");
            }
            Velocity = new Vector2(0f, Velocity.Y);
            MoveAndSlide();
            return;
        }

        base._PhysicsProcess(delta);
    }

    private void OnAggroAcquiredStalker(Node2D target)
    {
        var offset = target.GlobalPosition - GlobalPosition;
        _lungeDirX = Mathf.Sign(offset.X);
        _pauseRemaining = PauseDuration;
        _fsm?.TransitionNow("AmbushPause");
    }

    private void OnAggroLostStalker(Node2D? target)
    {
        if (_inRecovery)
        {
            // If player left during recovery, go invisible early
            SetInvisible();
        }
    }

    private void StartFizzle()
    {
        _isFizzling = true;
        _fizzleElapsed = 0f;
        _fsm?.TransitionNow("Fizzle");
    }

    private void StartLunge()
    {
        _isLunging = true;
        _lungeDistanceRemaining = LungeDistance;
        _hitbox?.ActivateWindow();
        _attackCooldownRemaining = AttackCooldownSeconds;
        OccupyAttackSlot();
        _fsm?.TransitionNow("Lunge");
    }

    private void EndLunge()
    {
        _isLunging = false;
        _hitbox?.DeactivateWindow();
        ReleaseAttackSlot();
        _inRecovery = true;
        _lungeRecoveryRemaining = LungeRecoverySeconds;
        _fsm?.TransitionNow("LungeRecovery");
    }

    private void SetInvisible() => SetAlpha(InvisibleAlpha);
    private void SetAlpha(float alpha)
    {
        if (_sprite == null) return;
        var c = _sprite.Modulate;
        c.A = alpha;
        _sprite.Modulate = c;
    }

    private void OnDiedForFormulaDrop(Node? _)
    {
        if (_formulaDropped) return;
        _formulaDropped = true;
        var player = Autoloads.RunManager.Instance?.Player;
        var inventory = player?.GetNodeOrNull<Components.Inventory.InventoryComponent>("InventoryComponent");
        inventory?.DiscoverFormula("formula_s5");
    }
}
