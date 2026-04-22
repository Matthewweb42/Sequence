using Godot;
using System.Collections.Generic;
using Sequence.Components.Combat;
using Sequence.Components.Hurtbox;
using Sequence.Components.Status;

namespace Sequence.Components.Hitbox;

/// <summary>
/// Outgoing attack collider that applies damage to hurtboxes during active windows.
/// Optionally applies a status effect to targets on hit.
/// </summary>
public partial class HitboxComponent : Area2D
{
	[Signal] public delegate void HitboxActivatedEventHandler();
	[Signal] public delegate void HitboxDeactivatedEventHandler();

	[Export(PropertyHint.Range, "0,10000,0.1")] public float Damage { get; set; } = 10f;
	[Export] public CombatTeam Team { get; set; } = CombatTeam.Enemy;
	[Export] public bool ActiveOnReady { get; set; }

	/// <summary>
	/// If set, applies this status effect ID to the target's StatusEffectComponent on a successful hit.
	/// </summary>
	[Export] public string StatusEffectOnHit { get; set; } = string.Empty;
	[Export(PropertyHint.Range, "0,60,0.1")] public float StatusDurationSeconds { get; set; } = 3f;

	private readonly HashSet<ulong> _hitTargetsThisWindow = new();

	public bool IsAttackActive { get; private set; }

	public override void _Ready()
	{
		AreaEntered += OnAreaEntered;
		IsAttackActive = false;
		SetAttackActive(ActiveOnReady);
	}

	public void ActivateWindow()
	{
		SetAttackActive(true);
	}

	public void DeactivateWindow()
	{
		SetAttackActive(false);
	}

	public Node GetAttackerEntity()
	{
		return GetParent() ?? this;
	}

	private void SetAttackActive(bool active)
	{
		IsAttackActive = active;
		Monitoring = active;

		if (active)
		{
			_hitTargetsThisWindow.Clear();
			EmitSignal(SignalName.HitboxActivated);
			foreach (var overlap in GetOverlappingAreas())
			{
				OnAreaEntered(overlap);
			}
		}
		else
		{
			_hitTargetsThisWindow.Clear();
			EmitSignal(SignalName.HitboxDeactivated);
		}
	}

	private void OnAreaEntered(Area2D area)
	{
		if (!IsAttackActive)
		{
			return;
		}

		if (area is not HurtboxComponent hurtbox)
		{
			return;
		}

		var targetId = area.GetInstanceId();
		if (_hitTargetsThisWindow.Contains(targetId))
		{
			return;
		}

		_hitTargetsThisWindow.Add(targetId);

		var hit = hurtbox.ReceiveHit(this);

		if (hit && !string.IsNullOrEmpty(StatusEffectOnHit))
		{
			var target = hurtbox.GetParent();
			var statusComp = target?.GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");
			statusComp?.ApplyEffect(StatusEffectOnHit, StatusDurationSeconds,
				tags: new[] { StatusEffectOnHit, "debuff" });
		}
	}
}
