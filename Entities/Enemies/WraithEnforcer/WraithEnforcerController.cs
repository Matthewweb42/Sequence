using Godot;

namespace Sequence.Entities.Enemies.WraithEnforcer;

/// <summary>
/// Aggressive melee charger. Transitions into a lunge at 180px range with a 2s recovery window.
/// </summary>
public partial class WraithEnforcerController : EnemyBase
{
    private const float LungeRange = 180f;
    private const float LungeTelegraphSeconds = 0.3f;
    private const float LungeSpeed = 600f;
    private const float LungeDistance = 200f;
    private const float LungeRecoverySeconds = 2.0f;

    private float _lungeTelegraphRemaining;
    private float _lungeDistanceRemaining;
    private float _lungeRecoveryRemaining;
    private float _lungeDirX;
    private bool _isLunging;
    private bool _inRecovery;

    public override void _Ready()
    {
        base._Ready();
        RegisterLungeStates();
    }

    private void RegisterLungeStates()
    {
        _fsm?.RegisterState(new EnemyBase.EnemySimpleState("LungeTelegraph"));
        _fsm?.RegisterState(new EnemyBase.EnemySimpleState("Lunge"));
        _fsm?.RegisterState(new EnemyBase.EnemySimpleState("LungeRecovery"));
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;

        // Handle lunge telegraph
        if (_lungeTelegraphRemaining > 0f)
        {
            _lungeTelegraphRemaining -= step;
            if (_lungeTelegraphRemaining <= 0f)
            {
                _lungeTelegraphRemaining = 0f;
                _isLunging = true;
                _lungeDistanceRemaining = LungeDistance;
                _hitbox?.ActivateWindow();
                _fsm?.TransitionNow("Lunge");
            }
            Velocity = new Vector2(0f, Velocity.Y);
            MoveAndSlide();
            return;
        }

        // Handle active lunge
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

            if (!IsOnFloor())
                Velocity += GetGravity() * step;

            MoveAndSlide();
            return;
        }

        // Handle recovery
        if (_inRecovery)
        {
            _lungeRecoveryRemaining -= step;
            if (_lungeRecoveryRemaining <= 0f)
            {
                _inRecovery = false;
                _fsm?.QueueTransition(_aggro?.HasTarget == true ? "Chase" : "Idle");
            }
            Velocity = new Vector2(0f, Velocity.Y);
            if (!IsOnFloor())
                Velocity += GetGravity() * step;
            MoveAndSlide();
            return;
        }

        base._PhysicsProcess(delta);
    }

    protected override void UpdateChase(Node2D? target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target))
        {
            Velocity = new Vector2(0f, Velocity.Y);
            _fsm?.QueueTransition("Idle");
            return;
        }

        var offset = target.GlobalPosition - GlobalPosition;
        var horizontalDistance = Mathf.Abs(offset.X);
        _lungeDirX = Mathf.Sign(offset.X);

        if (horizontalDistance <= LungeRange && CanStartAttack())
        {
            StartLungeTelegraph();
            return;
        }

        Velocity = new Vector2(_lungeDirX * MoveSpeed, Velocity.Y);

        if (CanJump && IsOnFloor() && offset.Y < -24f && horizontalDistance < 200f)
            Velocity = new Vector2(Velocity.X, -420f);
    }

    private void StartLungeTelegraph()
    {
        _fsm?.TransitionNow("LungeTelegraph");
        _lungeTelegraphRemaining = LungeTelegraphSeconds;
        _attackCooldownRemaining = AttackCooldownSeconds;
        OccupyAttackSlot();
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
}
