using Godot;
using Sequence.Components.Combat;
using Sequence.Components.Health;
using Sequence.Components.Hurtbox;
using Sequence.Components.Status;

namespace Sequence.Entities.Projectiles;

/// <summary>
/// Light of Holiness pillar — descends from above, vaporizes low-HP enemies,
/// and optionally applies the Notary Sigil debuff.
/// </summary>
public partial class LightPillarProjectile : Area2D
{
    public float VaporizeThreshold { get; set; } = 0.4f;
    public bool ApplyNotarySigil { get; set; }

    private const float PillarDamage = 60f;
    private const float DescendSpeed = 800f;
    private const float MaxTravel = 900f;

    private float _traveled;

    public override void _Ready()
    {
        AreaEntered += OnAreaEntered;
        Direction = Vector2.Down;
    }

    private Vector2 Direction { get; set; }

    public override void _PhysicsProcess(double delta)
    {
        var move = Direction * DescendSpeed * (float)delta;
        GlobalPosition += move;
        _traveled += move.Length();
        if (_traveled >= MaxTravel) QueueFree();
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is not HurtboxComponent hurtbox) return;
        if (hurtbox.Team == CombatTeam.Player) return;

        var target = hurtbox.GetParent();
        if (target == null) return;

        var health = target.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (health == null || health.IsDead) return;

        var damage = PillarDamage;
        if (health.CurrentHp / health.MaxHp < VaporizeThreshold)
        {
            damage = 99999f;
        }

        health.TakeDamage(damage, this);

        if (ApplyNotarySigil)
        {
            var status = target.GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");
            status?.ApplyEffect("notary_sigil", 10f,
                modifiers: new[] { new StatModifier("damage_received_multiplier", 1.2f, ModifierOp.Multiplicative) },
                tags: new[] { "debuff", "notary_sigil" });
        }
    }
}
