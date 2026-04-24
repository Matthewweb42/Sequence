using Godot;
using Sequence.Components.Combat;

namespace Sequence.Entities.Projectiles;

/// <summary>
/// Slow enemy projectile fired by the Corrupted Seer.
/// Travels in a straight line. Destructible by player melee via its own HurtboxComponent.
/// </summary>
public partial class SeerOrbProjectile : ProjectileController
{
    public SeerOrbProjectile()
    {
        Speed = 220f;
        Damage = 20f;
        MaxRange = 600f;
        PierceCount = 1;
        Team = CombatTeam.Enemy;
    }
}
