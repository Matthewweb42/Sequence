using Godot;
using Sequence.Autoloads;

namespace Sequence.Components.Sequence;

/// <summary>
/// Tracks sequence progression and emits advancement events.
/// </summary>
public partial class SequenceComponent : Node
{
	[Signal] public delegate void SequenceAdvancedEventHandler(int newSequence);

	[Export(PropertyHint.Range, "0,9,1")] public int StartingSequence { get; set; } = 9;
	[Export(PropertyHint.Range, "0,9,1")] public int FinalSequence { get; set; } = 5;

	public int CurrentSequence { get; private set; }

	public override void _Ready()
	{
		CurrentSequence = Mathf.Clamp(StartingSequence, 0, 9);
	}

	public bool TryAdvance()
	{
		if (CurrentSequence <= FinalSequence)
		{
			return false;
		}

		CurrentSequence -= 1;
		EmitSignal(SignalName.SequenceAdvanced, CurrentSequence);
		SignalBus.Instance?.PublishSequenceAdvanced(CurrentSequence);
		return true;
	}
}

