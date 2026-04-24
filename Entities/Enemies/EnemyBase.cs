using Godot;
using Sequence.Components.Aggro;
using Sequence.Components.Drop;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;
using Sequence.Components.StateMachine;
using Sequence.Components.Status;

namespace Sequence.Entities.Enemies;

/// <summary>
/// Base enemy orchestration script that wires components and transitions.
/// </summary>
public partial class EnemyBase : CharacterBody2D
{
	[Export(PropertyHint.Range, "0,2000,1")] public float MoveSpeed { get; set; } = 140f;
	[Export(PropertyHint.Range, "0,1000,1")] public float AttackRange { get; set; } = 32f;
	[Export(PropertyHint.Range, "0.01,5,0.01")] public float AttackCooldownSeconds { get; set; } = 1.1f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackWindupSeconds { get; set; } = 0.15f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackActiveSeconds { get; set; } = 0.12f;
	[Export] public NodePath? AggroPath { get; set; }
	[Export] public NodePath? HealthPath { get; set; }
	[Export] public NodePath? HitboxPath { get; set; }
	[Export] public NodePath? DropPath { get; set; }
	[Export] public NodePath? StateMachinePath { get; set; }
	[Export] public PackedScene? MaterialPickupScene { get; set; }

	protected AggroComponent? _aggro;
	protected HealthComponent? _health;
	protected HitboxComponent? _hitbox;
	protected DropComponent? _drop;
	protected StateMachineComponent? _fsm;
	protected StatusEffectComponent? _status;
	protected float _attackCooldownRemaining;
	private float _attackWindupRemaining;
	private float _attackActiveRemaining;

	public override void _Ready()
	{
		AddToGroup("enemies");

		_aggro = ResolveNode<AggroComponent>(AggroPath, "AggroComponent");
		_health = ResolveNode<HealthComponent>(HealthPath, "HealthComponent");
		_hitbox = ResolveNode<HitboxComponent>(HitboxPath, "HitboxComponent");
		_drop = ResolveNode<DropComponent>(DropPath, "DropComponent");
		_fsm = ResolveNode<StateMachineComponent>(StateMachinePath, "StateMachineComponent");
		_status = GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");

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

		if (_drop != null)
		{
			_drop.DropRequested += OnDropRequested;
		}

		if (_status != null)
		{
			_status.EffectRemoved += OnStatusEffectRemoved;
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

		if (_drop != null)
		{
			_drop.DropRequested -= OnDropRequested;
		}

		if (_status != null)
		{
			_status.EffectRemoved -= OnStatusEffectRemoved;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var step = (float)delta;
		TickAttackTimers(step);

		if (_health != null && _health.IsDead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// Gravity
		if (!IsOnFloor())
		{
			Velocity += GetGravity() * step;
		}

		// Contract bound: cannot move or attack
		if (_status != null && _status.HasTag("contract_bound"))
		{
			Velocity = new Vector2(0f, Velocity.Y);
			MoveAndSlide();
			return;
		}

		var currentState = _fsm?.CurrentStateName ?? string.Empty;
		var target = _aggro?.CurrentTarget;

		switch (currentState)
		{
			case "Chase":
				UpdateChase(target);
				break;
			case "Attack":
				Velocity = new Vector2(0f, Velocity.Y);
				break;
			case "Death":
				Velocity = new Vector2(0f, Velocity.Y);
				_hitbox?.DeactivateWindow();
				break;
			default:
				Velocity = new Vector2(0f, Velocity.Y);
				break;
		}

		// Blind: randomize horizontal movement direction
		if (_status != null && _status.HasTag("blind") && Velocity.X != 0f)
		{
			Velocity = new Vector2(GD.Randf() * 2f - 1f, Velocity.Y).Normalized() * new Vector2(Velocity.X, 0f).Length();
		}

		MoveAndSlide();
	}

	protected virtual bool CanJump => true;

	private void RegisterDefaultStates()
	{
		if (_fsm == null)
		{
			return;
		}

		_fsm.RegisterState(new EnemySimpleState("Idle"), setAsInitial: true);
		_fsm.RegisterState(new EnemySimpleState("Chase"));
		_fsm.RegisterState(new EnemySimpleState("Attack"));
		_fsm.RegisterState(new EnemySimpleState("Stagger"));
		_fsm.RegisterState(new EnemySimpleState("Death"));
	}

	private void OnAggroAcquired(Node2D target)
	{
		_fsm?.QueueTransition("Chase");
	}

	private void OnAggroLost(Node2D? target)
	{
		_fsm?.QueueTransition("Idle");
	}

	private void OnDied(Node? source)
	{
		_hitbox?.DeactivateWindow();
		Velocity = Vector2.Zero;
		_fsm?.TransitionNow("Death");
	}

	private void OnDropRequested(Node ownerEntity, Vector2 worldPosition)
	{
		SpawnMaterialPickup(worldPosition);
	}

	private void OnStatusEffectRemoved(string effectId)
	{
		// Contract broken: deal holy breach damage
		if (effectId == "contract_bound" && _health != null && !_health.IsDead)
		{
			_health.TakeDamage(20f, null);
		}
	}

	protected virtual void UpdateChase(Node2D? target)
	{
		if (target == null || !GodotObject.IsInstanceValid(target))
		{
			Velocity = Vector2.Zero;
			_fsm?.QueueTransition("Idle");
			return;
		}

		var offset = target.GlobalPosition - GlobalPosition;
		var horizontalDistance = Mathf.Abs(offset.X);

		if (horizontalDistance <= AttackRange)
		{
			Velocity = new Vector2(0f, Velocity.Y);
			if (CanStartAttack())
			{
				StartAttack();
			}
			return;
		}

		var dirX = Mathf.Sign(offset.X);
		Velocity = new Vector2(dirX * MoveSpeed, Velocity.Y);

		if (CanJump && IsOnFloor() && offset.Y < -24f && horizontalDistance < 200f)
		{
			Velocity = new Vector2(Velocity.X, -420f);
		}
	}

	protected virtual void OnAttackWindowOpened() { }

	protected virtual void OnAttackWindowActive(float elapsed) { }

	protected bool CanStartAttack()
	{
		return _attackCooldownRemaining <= 0f && _attackWindupRemaining <= 0f && _attackActiveRemaining <= 0f;
	}

	protected void StartAttack()
	{
		_fsm?.TransitionNow("Attack");
		_attackCooldownRemaining = AttackCooldownSeconds;
		_attackWindupRemaining = AttackWindupSeconds;
		_attackActiveRemaining = 0f;
		_hitbox?.DeactivateWindow();
	}

	protected void OccupyAttackSlot() { }
	protected void ReleaseAttackSlot() { }

	private void TickAttackTimers(float step)
	{
		if (_attackCooldownRemaining > 0f)
		{
			_attackCooldownRemaining = Mathf.Max(0f, _attackCooldownRemaining - step);
		}

		if (_attackWindupRemaining > 0f)
		{
			_attackWindupRemaining -= step;
			if (_attackWindupRemaining <= 0f)
			{
				_attackWindupRemaining = 0f;
				_hitbox?.ActivateWindow();
				_attackActiveRemaining = AttackActiveSeconds;
				OnAttackWindowOpened();
			}
		}

		if (_attackActiveRemaining > 0f)
		{
			var elapsed = AttackActiveSeconds - _attackActiveRemaining;
			OnAttackWindowActive(Mathf.Max(0f, elapsed));
			_attackActiveRemaining -= step;
			if (_attackActiveRemaining <= 0f)
			{
				_attackActiveRemaining = 0f;
				_hitbox?.DeactivateWindow();
				if (_health == null || !_health.IsDead)
				{
					if (_aggro != null && _aggro.HasTarget)
					{
						_fsm?.QueueTransition("Chase");
					}
					else
					{
						_fsm?.QueueTransition("Idle");
					}
				}
			}
		}
	}

	private void SpawnMaterialPickup(Vector2 worldPosition)
	{
		var root = GetTree()?.CurrentScene;
		if (root == null || MaterialPickupScene == null)
		{
			return;
		}

		var pickup = MaterialPickupScene.Instantiate<Node2D>();
		pickup.Name = "MaterialPickup";
		root.AddChild(pickup);
		pickup.GlobalPosition = worldPosition;
	}

	private T? ResolveNode<T>(NodePath? path, string fallbackName) where T : Node
	{
		if (path != null && !path.IsEmpty)
		{
			return GetNodeOrNull<T>(path);
		}

		return GetNodeOrNull<T>(fallbackName);
	}

	public class EnemySimpleState : State
	{
		public EnemySimpleState(string name) : base(name)
		{
		}
	}
}
