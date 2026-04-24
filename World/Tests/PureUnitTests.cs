using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sequence.Components.Ability;
using Sequence.Components.Combat;

namespace Sequence.Tests;

[TestClass]
public class PureUnitTests
{
    [TestMethod]
    [TestCategory("PureUnit")]
    public void AbilityCooldownTracker_Tick_ReducesAndExpiresCooldown()
    {
        var tracker = new AbilityCooldownTracker();

        tracker.StartCooldown("basic_attack", 0.5f);
        Assert.IsTrue(tracker.IsOnCooldown("basic_attack"));

        tracker.Tick(0.2f);
        var remaining = tracker.GetCooldownRemaining("basic_attack");
        Assert.IsTrue(remaining > 0.29f && remaining < 0.31f);

        tracker.Tick(1f);
        Assert.IsFalse(tracker.IsOnCooldown("basic_attack"));
        Assert.AreEqual(0f, tracker.GetCooldownRemaining("basic_attack"));
    }

    [TestMethod]
    [TestCategory("PureUnit")]
    public void AbilityCooldownTracker_ZeroOrNegativeCooldown_ClearsCooldown()
    {
        var tracker = new AbilityCooldownTracker();

        tracker.StartCooldown("basic_attack", 1f);
        Assert.IsTrue(tracker.IsOnCooldown("basic_attack"));

        tracker.StartCooldown("basic_attack", 0f);
        Assert.IsFalse(tracker.IsOnCooldown("basic_attack"));

        tracker.StartCooldown("basic_attack", 1f);
        tracker.StartCooldown("basic_attack", -1f);
        Assert.IsFalse(tracker.IsOnCooldown("basic_attack"));
    }

    [TestMethod]
    [TestCategory("PureUnit")]
    public void CombatTeam_EnumValues_AreStable()
    {
        Assert.AreEqual(0, (int)CombatTeam.Neutral);
        Assert.AreEqual(1, (int)CombatTeam.Player);
        Assert.AreEqual(2, (int)CombatTeam.Enemy);
    }

    [TestMethod]
    [TestCategory("PureUnit")]
    public void CombatTeam_ContainsExpectedMembers()
    {
        var values = System.Enum.GetValues<CombatTeam>();
        Assert.AreEqual(3, values.Length);
        CollectionAssert.Contains(values, CombatTeam.Neutral);
        CollectionAssert.Contains(values, CombatTeam.Player);
        CollectionAssert.Contains(values, CombatTeam.Enemy);
    }
}
