using Godot;

namespace Sequence.Autoloads;

/// <summary>
/// Global event bus for decoupled communication between gameplay systems.
/// </summary>
public partial class SignalBus : Node
{
	public static SignalBus? Instance { get; private set; }

	[Signal] public delegate void HealthChangedEventHandler(Node entity, float current, float max);
	[Signal] public delegate void EntityDiedEventHandler(Node entity, Node? source);
	[Signal] public delegate void HitLandedEventHandler(Node attacker, Node victim, float amount);
	[Signal] public delegate void AggroAcquiredEventHandler(Node owner, Node target);
	[Signal] public delegate void AggroLostEventHandler(Node owner, Node target);
	[Signal] public delegate void StateChangedEventHandler(Node owner, string previousState, string nextState);
	[Signal] public delegate void SequenceAdvancedEventHandler(int newSequence);

	// HLD Section 9 signals
	[Signal] public delegate void RoomClearedEventHandler(int roomId);
	[Signal] public delegate void RoomEnteredEventHandler(int roomId);
	[Signal] public delegate void InventoryChangedEventHandler();
	[Signal] public delegate void AbilityActivatedEventHandler(Resource ability);
	[Signal] public delegate void SanityDepletedEventHandler();
	[Signal] public delegate void ArtifactPickedUpEventHandler(Resource artifact);
	[Signal] public delegate void ShrineConsumedEventHandler(int roomId);

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

	public void PublishEntityDied(Node entity, Node? source)
	{
		EmitSignal(SignalName.EntityDied, entity, source ?? entity);
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

	public void PublishRoomCleared(int roomId)
	{
		EmitSignal(SignalName.RoomCleared, roomId);
	}

	public void PublishRoomEntered(int roomId)
	{
		EmitSignal(SignalName.RoomEntered, roomId);
	}

	public void PublishInventoryChanged()
	{
		EmitSignal(SignalName.InventoryChanged);
	}

	public void PublishAbilityActivated(Resource ability)
	{
		EmitSignal(SignalName.AbilityActivated, ability);
	}

	public void PublishSanityDepleted()
	{
		EmitSignal(SignalName.SanityDepleted);
	}

	public void PublishArtifactPickedUp(Resource artifact)
	{
		EmitSignal(SignalName.ArtifactPickedUp, artifact);
	}

	public void PublishShrineConsumed(int roomId)
	{
		EmitSignal(SignalName.ShrineConsumed, roomId);
	}
}

