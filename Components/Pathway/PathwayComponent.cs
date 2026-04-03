using Godot;
using Sequence.Components.Ability;

namespace Sequence.Components.Pathway;

/// <summary>
/// Holds selected pathway metadata and applies initial unlocks.
/// </summary>
public partial class PathwayComponent : Node
{
	[Signal] public delegate void PathwaySelectedEventHandler(string pathwayId);

	[Export] public string PathwayId { get; set; } = "seeker";
	[Export] public string[] StartingAbilityIds { get; set; } = System.Array.Empty<string>();
	[Export] public NodePath? AbilityPath { get; set; }

	public override void _Ready()
	{
		EmitSignal(SignalName.PathwaySelected, PathwayId);
		ApplyStartingAbilities();
	}

	private void ApplyStartingAbilities()
	{
		var abilityComponent = AbilityPath != null && !AbilityPath.IsEmpty
			? GetNodeOrNull<AbilityComponent>(AbilityPath)
			: GetParent()?.GetNodeOrNull<AbilityComponent>("AbilityComponent");

		if (abilityComponent == null)
		{
			return;
		}

		foreach (var abilityId in StartingAbilityIds)
		{
			if (!string.IsNullOrWhiteSpace(abilityId))
			{
				abilityComponent.UnlockAbility(abilityId);
			}
		}
	}
}

