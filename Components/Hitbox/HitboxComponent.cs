using Godot;
using System.Collections.Generic;
using Sequence.Components.Combat;
using Sequence.Components.Hurtbox;

namespace Sequence.Components.Hitbox;

/// <summary>
/// Outgoing attack collider that applies damage to hurtboxes during active windows.
/// </summary>
public partial class HitboxComponent : Area2D
{
	[Signal] public delegate void HitboxActivatedEventHandler();
	[Signal] public delegate void HitboxDeactivatedEventHandler();

	[Export(PropertyHint.Range, "0,10000,0.1")] public float Damage { get; set; } = 10f;
	[Export] public CombatTeam Team { get; set; } = CombatTeam.Enemy;
	[Export] public bool ActiveOnReady { get; set; }

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
		hurtbox.ReceiveHit(this);
	}
}

