using Godot;
using Sequence.Components.Sanity;
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
	[Export(PropertyHint.Range, "0,1000,0.1")] public float DefaultSanityCost { get; set; } = 5f;
	[Export] public NodePath? SanityPath { get; set; }

	private readonly AbilityCooldownTracker _cooldownTracker = new();
	private readonly HashSet<string> _unlockedAbilities = new();
	private SanityComponent? _sanity;

	public override void _Ready()
	{
		_sanity = ResolveSanity();
	}

	public override void _Process(double delta)
	{
		_cooldownTracker.Tick((float)delta);

		if (_sanity == null || !GodotObject.IsInstanceValid(_sanity))
		{
			_sanity = ResolveSanity();
		}
	}

	public void UnlockAbility(string abilityId)
	{
		if (!string.IsNullOrWhiteSpace(abilityId))
		{
			_unlockedAbilities.Add(abilityId);
		}
	}

	public bool IsUnlocked(string abilityId)
	{
		return !string.IsNullOrWhiteSpace(abilityId) && _unlockedAbilities.Contains(abilityId);
	}

	public bool TryActivate(string abilityId, float cooldownSeconds = -1f, float sanityCost = -1f)
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

		if (_cooldownTracker.IsOnCooldown(abilityId))
		{
			EmitSignal(SignalName.AbilityBlocked, abilityId, "cooldown_active");
			return false;
		}

		var cost = sanityCost >= 0f ? sanityCost : DefaultSanityCost;
		if (cost > 0f)
		{
			var sanity = _sanity ??= ResolveSanity();
			if (sanity == null)
			{
				EmitSignal(SignalName.AbilityBlocked, abilityId, "missing_sanity_component");
				return false;
			}

			if (!sanity.TrySpend(cost))
			{
				EmitSignal(SignalName.AbilityBlocked, abilityId, "insufficient_sanity");
				return false;
			}
		}

		var cd = cooldownSeconds >= 0f ? cooldownSeconds : DefaultCooldownSeconds;
		_cooldownTracker.StartCooldown(abilityId, cd);

		EmitSignal(SignalName.AbilityActivated, abilityId);
		EmitSignal(SignalName.AbilityCooldownStarted, abilityId, cd);
		return true;
	}

	public float GetCooldownRemaining(string abilityId)
	{
		return _cooldownTracker.GetCooldownRemaining(abilityId);
	}

	private SanityComponent? ResolveSanity()
	{
		if (SanityPath != null && !SanityPath.IsEmpty)
		{
			return GetNodeOrNull<SanityComponent>(SanityPath);
		}

		return GetParent()?.GetNodeOrNull<SanityComponent>("SanityComponent");
	}
}

