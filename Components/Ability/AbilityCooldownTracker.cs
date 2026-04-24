using System;
using System.Collections.Generic;

namespace Sequence.Components.Ability;

/// <summary>
/// Tracks per-ability cooldown timers without depending on engine types.
/// </summary>
public sealed class AbilityCooldownTracker
{
	private readonly Dictionary<string, float> _cooldownsRemaining = new(StringComparer.Ordinal);

	public void Tick(float deltaSeconds)
	{
		if (deltaSeconds <= 0f || _cooldownsRemaining.Count == 0)
		{
			return;
		}

		var keys = new List<string>(_cooldownsRemaining.Keys);
		foreach (var key in keys)
		{
			var remaining = MathF.Max(0f, _cooldownsRemaining[key] - deltaSeconds);
			if (remaining <= 0f)
			{
				_cooldownsRemaining.Remove(key);
			}
			else
			{
				_cooldownsRemaining[key] = remaining;
			}
		}
	}

	public void StartCooldown(string abilityId, float cooldownSeconds)
	{
		if (string.IsNullOrWhiteSpace(abilityId))
		{
			return;
		}

		if (cooldownSeconds <= 0f)
		{
			_cooldownsRemaining.Remove(abilityId);
			return;
		}

		_cooldownsRemaining[abilityId] = cooldownSeconds;
	}

	public float GetCooldownRemaining(string abilityId)
	{
		if (string.IsNullOrWhiteSpace(abilityId))
		{
			return 0f;
		}

		return _cooldownsRemaining.TryGetValue(abilityId, out var remaining) ? remaining : 0f;
	}

	public bool IsOnCooldown(string abilityId)
	{
		return GetCooldownRemaining(abilityId) > 0f;
	}
}
