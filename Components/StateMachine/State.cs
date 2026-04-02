namespace Sequence.Components.StateMachine;

/// <summary>
/// Base state type for the generic state machine component.
/// </summary>
public abstract class State
{
	protected State(string name)
	{
		Name = name;
	}

	public string Name { get; }

	public virtual void Enter(StateMachineComponent owner)
	{
	}

	public virtual void Update(StateMachineComponent owner, float delta)
	{
	}

	public virtual void Exit(StateMachineComponent owner)
	{
	}

	public virtual bool CanTransitionTo(string targetState)
	{
		return true;
	}
}

