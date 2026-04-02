using Godot;
using System.Collections.Generic;

namespace Sequence.Components.Ability;

/// <summary>
/// Runtime registry and activation gateway for abilities.
/// </summary>
public partial class AbilityComponent : Node
{
	[Signal] public delegate void AbilityActivatedEventHandler(string abilityId);
	[Signal] public delegate void AbilityBlockedEventHandler(string abilityId, string reason);
	[Signal] public delegate void AbilityCooldownStartedEventHandler(string abilityId, float cooldownSeconds);

	[Export] public bool EnableDebugAbility { get; set; }
	[Export] public string DebugAbilityId { get; set; } = "basic_attack";
	[Export(PropertyHint.Range, "0,120,0.1")] public float DefaultCooldownSeconds { get; set; } = 0.5f;

	private readonly Dictionary<string, float> _cooldownsRemaining = new();
	private readonly HashSet<string> _unlockedAbilities = new();

	public override void _Process(double delta)
	{
		var step = (float)delta;
		if (_cooldownsRemaining.Count == 0)
		{
			return;
		}

		var keys = new List<string>(_cooldownsRemaining.Keys);
		foreach (var key in keys)
		{
			_cooldownsRemaining[key] = Mathf.Max(0f, _cooldownsRemaining[key] - step);
		}
	}

	public void UnlockAbility(string abilityId)
	{
		if (!string.IsNullOrWhiteSpace(abilityId))
		{
			_unlockedAbilities.Add(abilityId);
		}
	}

	public bool TryActivate(string abilityId, float cooldownSeconds = -1f)
	{
		if (string.IsNullOrWhiteSpace(abilityId))
		{
			EmitSignal(SignalName.AbilityBlocked, abilityId, "invalid_ability_id");
			return false;
		}

		if (!_unlockedAbilities.Contains(abilityId))
		{
			EmitSignal(SignalName.AbilityBlocked, abilityId, "ability_locked");
			return false;
		}

		if (_cooldownsRemaining.TryGetValue(abilityId, out var remaining) && remaining > 0f)
		{
			EmitSignal(SignalName.AbilityBlocked, abilityId, "cooldown_active");
			return false;
		}

		var cd = cooldownSeconds >= 0f ? cooldownSeconds : DefaultCooldownSeconds;
		_cooldownsRemaining[abilityId] = cd;

		EmitSignal(SignalName.AbilityActivated, abilityId);
		EmitSignal(SignalName.AbilityCooldownStarted, abilityId, cd);
		return true;
	}
}

