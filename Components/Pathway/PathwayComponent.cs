using Godot;
using Sequence.Components.Ability;
using Sequence.Components.Sequence;

namespace Sequence.Components.Pathway;

/// <summary>
/// Holds selected pathway metadata and applies initial unlocks.
/// Also listens to SequenceComponent.SequenceAdvanced to unlock abilities
/// as the player progresses through sequences.
/// </summary>
public partial class PathwayComponent : Node
{
    [Signal] public delegate void PathwaySelectedEventHandler(string pathwayId);

    [Export] public string PathwayId { get; set; } = "seeker";
    [Export] public string[] StartingAbilityIds { get; set; } = System.Array.Empty<string>();
    [Export] public NodePath? AbilityPath { get; set; }

    /// <summary>
    /// Parallel arrays: SequenceUnlockLevels[i] is the sequence number at which
    /// SequenceUnlockAbilityIds[i] should be unlocked.
    /// </summary>
    [Export] public int[] SequenceUnlockLevels { get; set; } = System.Array.Empty<int>();
    [Export] public string[] SequenceUnlockAbilityIds { get; set; } = System.Array.Empty<string>();

    private AbilityComponent? _ability;

    public override void _Ready()
    {
        _ability = AbilityPath != null && !AbilityPath.IsEmpty
            ? GetNodeOrNull<AbilityComponent>(AbilityPath)
            : GetParent()?.GetNodeOrNull<AbilityComponent>("AbilityComponent");

        EmitSignal(SignalName.PathwaySelected, PathwayId);
        ApplyStartingAbilities();

        var sequence = GetParent()?.GetNodeOrNull<SequenceComponent>("SequenceComponent");
        if (sequence != null)
        {
            sequence.SequenceAdvanced += OnSequenceAdvanced;
        }
    }

    private void ApplyStartingAbilities()
    {
        if (_ability == null) return;

        foreach (var abilityId in StartingAbilityIds)
        {
            if (!string.IsNullOrWhiteSpace(abilityId))
            {
                _ability.UnlockAbility(abilityId);
            }
        }
    }

    private void OnSequenceAdvanced(int newSequence)
    {
        if (_ability == null) return;

        for (var i = 0; i < SequenceUnlockLevels.Length && i < SequenceUnlockAbilityIds.Length; i++)
        {
            if (SequenceUnlockLevels[i] == newSequence && !string.IsNullOrWhiteSpace(SequenceUnlockAbilityIds[i]))
            {
                _ability.UnlockAbility(SequenceUnlockAbilityIds[i]);
            }
        }
    }
}
