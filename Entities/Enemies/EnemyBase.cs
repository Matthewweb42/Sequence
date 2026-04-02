using Godot;
using Sequence.Components.Aggro;
using Sequence.Components.Health;
using Sequence.Components.StateMachine;

namespace Sequence.Entities.Enemies;

/// <summary>
/// Base enemy orchestration script that wires components and transitions.
/// </summary>
public partial class EnemyBase : CharacterBody2D
{
	[Export(PropertyHint.Range, "0,2000,1")] public float MoveSpeed { get; set; } = 140f;
	[Export] public NodePath AggroPath { get; set; }
	[Export] public NodePath HealthPath { get; set; }
	[Export] public NodePath StateMachinePath { get; set; }

	private AggroComponent _aggro;
	private HealthComponent _health;
	private StateMachineComponent _fsm;

	public override void _Ready()
	{
		_aggro = ResolveNode<AggroComponent>(AggroPath, "AggroComponent");
		_health = ResolveNode<HealthComponent>(HealthPath, "HealthComponent");
		_fsm = ResolveNode<StateMachineComponent>(StateMachinePath, "StateMachineComponent");

		RegisterDefaultStates();

		if (_aggro != null)
		{
			_aggro.AggroAcquired += OnAggroAcquired;
			_aggro.AggroLost += OnAggroLost;
		}

		if (_health != null)
		{
			_health.Died += OnDied;
		}
	}

	public override void _ExitTree()
	{
		if (_aggro != null)
		{
			_aggro.AggroAcquired -= OnAggroAcquired;
			_aggro.AggroLost -= OnAggroLost;
		}

		if (_health != null)
		{
			_health.Died -= OnDied;
		}
	}

	private void RegisterDefaultStates()
	{
		if (_fsm == null)
		{
			return;
		}

		_fsm.RegisterState(new EnemySimpleState("Idle"), setAsInitial: true);
		_fsm.RegisterState(new EnemySimpleState("Chase"));
		_fsm.RegisterState(new EnemySimpleState("Attack"));
		_fsm.RegisterState(new EnemySimpleState("Death"));
	}

	private void OnAggroAcquired(Node2D target)
	{
		_fsm?.QueueTransition("Chase");
	}

	private void OnAggroLost(Node2D target)
	{
		_fsm?.QueueTransition("Idle");
	}

	private void OnDied(Node source)
	{
		_fsm?.TransitionNow("Death");
	}

	private T ResolveNode<T>(NodePath path, string fallbackName) where T : Node
	{
		if (path != null && !path.IsEmpty)
		{
			return GetNodeOrNull<T>(path);
		}

		return GetNodeOrNull<T>(fallbackName);
	}

	private class EnemySimpleState : State
	{
		public EnemySimpleState(string name) : base(name)
		{
		}
	}
}

