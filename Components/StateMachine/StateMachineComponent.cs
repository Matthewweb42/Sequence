using Godot;
using System.Collections.Generic;
using Sequence.Autoloads;

namespace Sequence.Components.StateMachine;

/// <summary>
/// Generic FSM component with queued transitions for deterministic per-frame processing.
/// </summary>
public partial class StateMachineComponent : Node
{
	[Signal] public delegate void StateChangedEventHandler(string previousState, string nextState);

	[Export] public bool ProcessInProcessLoop { get; set; } = true;

	private readonly Dictionary<string, State> _states = new();
	private readonly Queue<string> _transitionQueue = new();

	public State? CurrentState { get; private set; }
	public string CurrentStateName => CurrentState?.Name ?? string.Empty;

	public bool RegisterState(State state, bool setAsInitial = false)
	{
		if (state == null || string.IsNullOrWhiteSpace(state.Name))
		{
			return false;
		}

		if (_states.ContainsKey(state.Name))
		{
			return false;
		}

		_states[state.Name] = state;

		if (setAsInitial && CurrentState == null)
		{
			CurrentState = state;
			CurrentState.Enter(this);
			EmitSignal(SignalName.StateChanged, string.Empty, CurrentState.Name);
			SignalBus.Instance?.PublishStateChanged(GetParentOrSelf(), string.Empty, CurrentState.Name);
		}

		return true;
	}

	public bool HasState(string stateName)
	{
		return _states.ContainsKey(stateName);
	}

	public bool QueueTransition(string targetState)
	{
		if (!_states.ContainsKey(targetState))
		{
			return false;
		}

		_transitionQueue.Enqueue(targetState);
		return true;
	}

	public bool TransitionNow(string targetState)
	{
		if (!_states.TryGetValue(targetState, out var nextState))
		{
			return false;
		}

		if (CurrentState != null)
		{
			if (CurrentState.Name == targetState)
			{
				return false;
			}

			if (!CurrentState.CanTransitionTo(targetState))
			{
				return false;
			}
		}

		var previousName = CurrentState?.Name ?? string.Empty;
		CurrentState?.Exit(this);
		CurrentState = nextState;
		CurrentState.Enter(this);

		EmitSignal(SignalName.StateChanged, previousName, CurrentState.Name);
		SignalBus.Instance?.PublishStateChanged(GetParentOrSelf(), previousName, CurrentState.Name);
		return true;
	}

	public override void _Process(double delta)
	{
		if (!ProcessInProcessLoop)
		{
			return;
		}

		if (_transitionQueue.Count > 0)
		{
			var requestedState = _transitionQueue.Dequeue();
			TransitionNow(requestedState);
		}

		CurrentState?.Update(this, (float)delta);
	}

	private Node GetParentOrSelf()
	{
		return GetParent() ?? this;
	}
}

