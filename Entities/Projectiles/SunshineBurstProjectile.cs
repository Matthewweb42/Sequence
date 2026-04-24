using Godot;
using Sequence.Components.Combat;

namespace Sequence.Entities.Projectiles;

/// <summary>
/// Sunshine Burst projectile — deals bonus damage against undead and evil spirits.
/// </summary>
public partial class SunshineBurstProjectile : ProjectileController
{
    private const float TypeBonusMultiplier = 1.6f;

    public SunshineBurstProjectile()
    {
        Speed = 350f;
        Damage = 25f;
        MaxRange = 450f;
        PierceCount = 1;
        Team = CombatTeam.Player;
    }

    protected override float ComputeDamage(float baseDamage, Node target)
    {
        var tags = target.GetNodeOrNull<EntityTagComponent>("EntityTagComponent");
        if (tags != null && (tags.HasTag("undead") || tags.HasTag("evil_spirit")))
        {
            return baseDamage * TypeBonusMultiplier;
        }
        return baseDamage;
    }
}
