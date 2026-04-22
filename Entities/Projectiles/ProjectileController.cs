using Godot;
using Sequence.Components.Combat;
using Sequence.Components.Health;
using Sequence.Components.Hurtbox;
using Sequence.Components.Status;

namespace Sequence.Entities.Projectiles;

/// <summary>
/// Root script for projectile scenes. Moves in a set direction, deals damage on contact,
/// supports pierce, and optionally applies a status effect on hit.
/// </summary>
public partial class ProjectileController : Area2D
{
    [Export(PropertyHint.Range, "10,5000,1")] public float Speed { get; set; } = 400f;
    [Export(PropertyHint.Range, "0,10000,0.1")] public float Damage { get; set; } = 25f;
    [Export(PropertyHint.Range, "0,5000,1")] public float MaxRange { get; set; } = 600f;
    [Export(PropertyHint.Range, "0,20,1")] public int PierceCount { get; set; } = 1;
    [Export] public CombatTeam Team { get; set; } = CombatTeam.Player;
    [Export] public string StatusOnHit { get; set; } = string.Empty;
    [Export(PropertyHint.Range, "0,60,0.1")] public float StatusDuration { get; set; } = 3f;

    /// <summary>Set before adding to the scene tree.</summary>
    public Vector2 Direction { get; set; } = Vector2.Right;

    private float _distanceTraveled;
    private int _hitsRemaining;

    public override void _Ready()
    {
        _hitsRemaining = PierceCount;
        AreaEntered += OnAreaEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        var move = Direction * Speed * (float)delta;
        GlobalPosition += move;
        _distanceTraveled += move.Length();

        if (_distanceTraveled >= MaxRange)
        {
            QueueFree();
        }
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is not HurtboxComponent hurtbox) return;
        if (hurtbox.Team == Team) return;

        var target = hurtbox.GetParent();
        if (target == null) return;

        var health = target.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (health == null || health.IsDead) return;

        var finalDamage = ComputeDamage(Damage, target);
        health.TakeDamage(finalDamage, this);

        if (!string.IsNullOrEmpty(StatusOnHit))
        {
            var statusComp = target.GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");
            statusComp?.ApplyEffect(StatusOnHit, StatusDuration, tags: new[] { StatusOnHit, "debuff" });
        }

        _hitsRemaining--;
        if (_hitsRemaining <= 0)
        {
            QueueFree();
        }
    }

    /// <summary>
    /// Override in subclasses to apply type-bonus damage (e.g. bonus vs. undead).
    /// </summary>
    protected virtual float ComputeDamage(float baseDamage, Node target)
    {
        return baseDamage;
    }
}
