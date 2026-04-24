using Godot;
using Sequence.Entities.Projectiles;

namespace Sequence.Entities.Enemies.CorruptedSeer;

/// <summary>
/// Ranged enemy that maintains a preferred distance band (250-400px) and fires slow telegraphed orbs.
/// Attaches to GoblinEnemy.tscn (or a dedicated scene) alongside EnemyBase.
/// </summary>
public partial class CorruptedSeerController : EnemyBase
{
    private const float PreferredMinDistance = 250f;
    private const float PreferredMaxDistance = 400f;
    private const float OrbWindupSeconds = 0.35f;

    [Export] public PackedScene? SeerOrbScene { get; set; }

    private bool _orbFired;

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
        var dirX = Mathf.Sign(offset.X);

        if (horizontalDistance < PreferredMinDistance)
        {
            // Retreat: move away from player
            Velocity = new Vector2(-dirX * MoveSpeed, Velocity.Y);
        }
        else if (horizontalDistance > PreferredMaxDistance)
        {
            // Advance: chase player
            Velocity = new Vector2(dirX * MoveSpeed, Velocity.Y);

            if (CanJump && IsOnFloor() && offset.Y < -24f && horizontalDistance < 200f)
                Velocity = new Vector2(Velocity.X, -420f);
        }
        else
        {
            // In preferred band: stop and attack
            Velocity = new Vector2(0f, Velocity.Y);
            if (CanStartAttack())
                StartAttack();
        }
    }

    protected override void OnAttackWindowOpened()
    {
        _orbFired = false;
    }

    protected override void OnAttackWindowActive(float elapsed)
    {
        // Fire orb once at the midpoint of the active window rather than melee hit
        if (!_orbFired && SeerOrbScene != null && elapsed >= OrbWindupSeconds)
        {
            _orbFired = true;
            FireOrb();
        }
        // Keep melee hitbox inactive — seer is ranged
        _hitbox?.DeactivateWindow();
    }

    private void FireOrb()
    {
        if (SeerOrbScene == null) return;

        var orb = SeerOrbScene.Instantiate<SeerOrbProjectile>();
        var target = _aggro?.CurrentTarget;
        var dir = target != null
            ? (target.GlobalPosition - GlobalPosition).Normalized()
            : (Velocity.X >= 0 ? Vector2.Right : Vector2.Left);

        orb.Direction = dir;
        GetTree()?.CurrentScene?.AddChild(orb);
        orb.GlobalPosition = GlobalPosition;
    }
}
