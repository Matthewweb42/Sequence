using Godot;
using Sequence.Autoloads;
using Sequence.Components.Combat;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;

namespace Sequence.Components.Hurtbox;

/// <summary>
/// Receives hit payloads, validates them, and applies damage to a linked health component.
/// </summary>
public partial class HurtboxComponent : Area2D
{
	[Signal] public delegate void HitAcceptedEventHandler(Node attacker, float damage);
	[Signal] public delegate void HitRejectedEventHandler(Node? attacker, string reason);

	[Export] public CombatTeam Team { get; set; } = CombatTeam.Enemy;
	[Export] public bool IsInvulnerable { get; set; }
	[Export(PropertyHint.Range, "0,5,0.05")] public float IFrameDurationSeconds { get; set; } = 0.1f;
	[Export] public NodePath? HealthPath { get; set; }

	private HealthComponent? _health;
	private float _invulnTimer;

	public override void _Ready()
	{
		_health = ResolveHealthComponent();
	}

	public override void _Process(double delta)
	{
		if (_invulnTimer > 0f)
		{
			_invulnTimer -= (float)delta;
		}
	}

	public bool ReceiveHit(HitboxComponent? source)
	{
		if (source == null)
		{
			EmitSignal(SignalName.HitRejected, new Variant(), "no_source");
			return false;
		}

		var attacker = source.GetAttackerEntity();

		if (!source.IsAttackActive)
		{
			EmitSignal(SignalName.HitRejected, attacker, "hitbox_inactive");
			return false;
		}

		if (source.Damage <= 0f)
		{
			EmitSignal(SignalName.HitRejected, attacker, "non_positive_damage");
			return false;
		}

		if (IsInvulnerable || _invulnTimer > 0f)
		{
			EmitSignal(SignalName.HitRejected, attacker, "invulnerable");
			return false;
		}

		if (source.Team == Team)
		{
			EmitSignal(SignalName.HitRejected, attacker, "friendly_fire_blocked");
			return false;
		}

		if (_health == null)
		{
			_health = ResolveHealthComponent();
		}

		if (_health == null)
		{
			EmitSignal(SignalName.HitRejected, attacker, "missing_health_component");
			return false;
		}

		var applied = _health.TakeDamage(source.Damage, attacker);
		if (!applied)
		{
			EmitSignal(SignalName.HitRejected, attacker, "health_rejected");
			return false;
		}

		if (IFrameDurationSeconds > 0f)
		{
			_invulnTimer = IFrameDurationSeconds;
		}

		EmitSignal(SignalName.HitAccepted, attacker, source.Damage);
		SignalBus.Instance?.PublishHitLanded(attacker, GetVictimEntity(), source.Damage);
		return true;
	}

	public void SetTemporaryInvulnerability(float durationSeconds)
	{
		_invulnTimer = Mathf.Max(_invulnTimer, durationSeconds);
	}

	private HealthComponent? ResolveHealthComponent()
	{
		if (HealthPath != null && !HealthPath.IsEmpty)
		{
			return GetNodeOrNull<HealthComponent>(HealthPath);
		}

		return GetParent()?.GetNodeOrNull<HealthComponent>("HealthComponent");
	}

	private Node GetVictimEntity()
	{
		return GetParent() ?? this;
	}
}

