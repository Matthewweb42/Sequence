using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sequence.Components.Combat;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;
using Sequence.Components.Hurtbox;
using Sequence.Components.StateMachine;

namespace Sequence.Tests;

[TestClass]
[TestCategory("GodotRuntime")]
public class ComponentCoreTests
{
    [TestMethod]
    public void Health_TakeDamage_ClampsAtZero_AndDiesOnlyOnce()
    {
        var health = new HealthComponent
        {
            MaxHp = 10f,
            StartAtMaxHp = true
        };

        health._Ready();

        var deathCount = 0;
        health.Died += _ => deathCount++;

        var firstHitApplied = health.TakeDamage(15f);
        var secondHitApplied = health.TakeDamage(1f);

        Assert.IsTrue(firstHitApplied);
        Assert.IsFalse(secondHitApplied);
        Assert.AreEqual(0f, health.CurrentHp);
        Assert.IsTrue(health.IsDead);
        Assert.AreEqual(1, deathCount);
    }

    [TestMethod]
    public void Health_Heal_ClampsToMax()
    {
        var health = new HealthComponent
        {
            MaxHp = 20f,
            StartAtMaxHp = true
        };

        health._Ready();
        health.TakeDamage(7f);

        var healed = health.Heal(50f);

        Assert.IsTrue(healed);
        Assert.AreEqual(20f, health.CurrentHp);
    }

    [TestMethod]
    public void StateMachine_QueueTransition_ProcessesOnePerProcessTick()
    {
        var machine = new StateMachineComponent();
        var log = new List<string>();

        machine.RegisterState(new TestState("Idle", log), setAsInitial: true);
        machine.RegisterState(new TestState("Chase", log));

        machine.QueueTransition("Chase");
        machine.QueueTransition("Idle");

        machine._Process(0.016);
        Assert.AreEqual("Chase", machine.CurrentStateName);

        machine._Process(0.016);
        Assert.AreEqual("Idle", machine.CurrentStateName);
    }

    [TestMethod]
    public void StateMachine_TransitionNow_RespectsCanTransitionTo()
    {
        var machine = new StateMachineComponent();
        var log = new List<string>();

        machine.RegisterState(new GateState("Idle", log, allowedTargets: new HashSet<string>()), setAsInitial: true);
        machine.RegisterState(new TestState("Attack", log));

        var transitioned = machine.TransitionNow("Attack");

        Assert.IsFalse(transitioned);
        Assert.AreEqual("Idle", machine.CurrentStateName);
    }

    [TestMethod]
    public void Hurtbox_ReceiveHit_BlocksFriendlyFire()
    {
        var victim = new Godot.Node2D();
        var attacker = new Godot.Node2D();

        var health = new HealthComponent { MaxHp = 30f, StartAtMaxHp = true };
        var hurtbox = new HurtboxComponent { Team = CombatTeam.Player, IFrameDurationSeconds = 0f };
        var hitbox = new HitboxComponent { Team = CombatTeam.Player, Damage = 10f };

        victim.AddChild(health);
        victim.AddChild(hurtbox);
        attacker.AddChild(hitbox);

        health._Ready();
        hurtbox._Ready();
        hitbox._Ready();
        hitbox.ActivateWindow();

        var applied = hurtbox.ReceiveHit(hitbox);

        Assert.IsFalse(applied);
        Assert.AreEqual(30f, health.CurrentHp);
    }

    [TestMethod]
    public void Hurtbox_ReceiveHit_AppliesDamageWhenValid()
    {
        var victim = new Godot.Node2D();
        var attacker = new Godot.Node2D();

        var health = new HealthComponent { MaxHp = 30f, StartAtMaxHp = true };
        var hurtbox = new HurtboxComponent { Team = CombatTeam.Player, IFrameDurationSeconds = 0f };
        var hitbox = new HitboxComponent { Team = CombatTeam.Enemy, Damage = 7.5f };

        victim.AddChild(health);
        victim.AddChild(hurtbox);
        attacker.AddChild(hitbox);

        health._Ready();
        hurtbox._Ready();
        hitbox._Ready();
        hitbox.ActivateWindow();

        var applied = hurtbox.ReceiveHit(hitbox);

        Assert.IsTrue(applied);
        Assert.AreEqual(22.5f, health.CurrentHp);
    }

    private class TestState : State
    {
        private readonly List<string> _log;

        public TestState(string name, List<string> log) : base(name)
        {
            _log = log;
        }

        public override void Enter(StateMachineComponent owner)
        {
            _log.Add($"enter:{Name}");
        }

        public override void Exit(StateMachineComponent owner)
        {
            _log.Add($"exit:{Name}");
        }
    }

    private class GateState : TestState
    {
        private readonly HashSet<string> _allowedTargets;

        public GateState(string name, List<string> log, HashSet<string> allowedTargets) : base(name, log)
        {
            _allowedTargets = allowedTargets;
        }

        public override bool CanTransitionTo(string targetState)
        {
            return _allowedTargets.Contains(targetState);
        }
    }
}
