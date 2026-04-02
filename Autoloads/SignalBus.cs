using Godot;

namespace Sequence.Autoloads;

/// <summary>
/// Global event bus for decoupled communication between gameplay systems.
/// </summary>
public partial class SignalBus : Node
{
	public static SignalBus Instance { get; private set; }

	[Signal] public delegate void HealthChangedEventHandler(Node entity, float current, float max);
	[Signal] public delegate void EntityDiedEventHandler(Node entity, Node source);
	[Signal] public delegate void HitLandedEventHandler(Node attacker, Node victim, float amount);
	[Signal] public delegate void AggroAcquiredEventHandler(Node owner, Node target);
	[Signal] public delegate void AggroLostEventHandler(Node owner, Node target);
	[Signal] public delegate void StateChangedEventHandler(Node owner, string previousState, string nextState);
	[Signal] public delegate void SequenceAdvancedEventHandler(int newSequence);

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void PublishHealthChanged(Node entity, float current, float max)
	{
		EmitSignal(SignalName.HealthChanged, entity, current, max);
	}

	public void PublishEntityDied(Node entity, Node source)
	{
		EmitSignal(SignalName.EntityDied, entity, source);
	}

	public void PublishHitLanded(Node attacker, Node victim, float amount)
	{
		EmitSignal(SignalName.HitLanded, attacker, victim, amount);
	}

	public void PublishAggroAcquired(Node owner, Node target)
	{
		EmitSignal(SignalName.AggroAcquired, owner, target);
	}

	public void PublishAggroLost(Node owner, Node target)
	{
		EmitSignal(SignalName.AggroLost, owner, target);
	}

	public void PublishStateChanged(Node owner, string previousState, string nextState)
	{
		EmitSignal(SignalName.StateChanged, owner, previousState, nextState);
	}

	public void PublishSequenceAdvanced(int newSequence)
	{
		EmitSignal(SignalName.SequenceAdvanced, newSequence);
	}
}

