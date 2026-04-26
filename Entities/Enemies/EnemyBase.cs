using Godot;
using Sequence.Components.Aggro;
using Sequence.Components.Animation;
using Sequence.Components.Drop;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;
using Sequence.Components.Hurtbox;
using Sequence.Components.StateMachine;
using Sequence.Components.Status;

namespace Sequence.Entities.Enemies;

/// <summary>
/// Base enemy orchestration script that wires components and transitions.
/// </summary>
public partial class EnemyBase : CharacterBody2D
{
	[Export(PropertyHint.Range, "0,2000,1")] public float MoveSpeed { get; set; } = 140f;
	[Export(PropertyHint.Range, "0,3000,1")] public float JumpVelocity { get; set; } = 700f;
	[Export(PropertyHint.Range, "0,1000,1")] public float JumpVerticalThreshold { get; set; } = 24f;
	[Export(PropertyHint.Range, "0.05,3,0.05")] public float JumpCooldownSeconds { get; set; } = 0.5f;
	[Export(PropertyHint.Range, "0,1000,1")] public float AttackRange { get; set; } = 32f;
	[Export(PropertyHint.Range, "0,500,1")] public float HitboxForwardOffset { get; set; } = 20f;
	[Export(PropertyHint.Range, "0.01,5,0.01")] public float AttackCooldownSeconds { get; set; } = 1.1f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackWindupSeconds { get; set; } = 0.15f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackActiveSeconds { get; set; } = 0.12f;
	[Export(PropertyHint.Range, "0,2000,1")] public float HitKnockbackSpeed { get; set; } = 220f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float HitStopSeconds { get; set; } = 0.08f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float FlashSeconds { get; set; } = 0.08f;
	[Export(PropertyHint.Range, "0,5,0.05")] public float DespawnDelaySeconds { get; set; } = 0.4f;
	[Export] public bool DebugCombat { get; set; }
	[Export] public NodePath? AggroPath { get; set; }
	[Export] public NodePath? HealthPath { get; set; }
	[Export] public NodePath? HitboxPath { get; set; }
	[Export] public NodePath? HurtboxPath { get; set; }
	[Export] public NodePath? DropPath { get; set; }
	[Export] public NodePath? StateMachinePath { get; set; }
	[Export] public PackedScene? MaterialPickupScene { get; set; }
	[Export] public Texture2D? AttackSpriteSheet { get; set; }
	[Export(PropertyHint.Range, "16,512,1")] public int SpriteFrameSize { get; set; } = 150;
	[Export(PropertyHint.Range, "1,60,1")] public int AttackFrameCount { get; set; } = 12;

	private AggroComponent? _aggro;
	private HealthComponent? _health;
	private HitboxComponent? _hitbox;
	private HurtboxComponent? _hurtbox;
	private DropComponent? _drop;
	private StateMachineComponent? _fsm;
	private StatusEffectComponent? _status;
	private Sprite2D? _sprite;
	private SpriteAnimator? _animator;
	private float _attackCooldownRemaining;
	private float _attackWindupRemaining;
	private float _attackActiveRemaining;
	private float _hitStopRemaining;
	private float _flashRemaining;
	private float _debugTimer;
	private float _jumpCooldownRemaining;
	private Color _baseModulate = Colors.White;

	public override void _Ready()
	{
		AddToGroup("enemies");

		_aggro = ResolveNode<AggroComponent>(AggroPath, "AggroComponent");
		_health = ResolveNode<HealthComponent>(HealthPath, "HealthComponent");
		_hitbox = ResolveNode<HitboxComponent>(HitboxPath, "HitboxComponent");
		_hurtbox = ResolveNode<HurtboxComponent>(HurtboxPath, "HurtboxComponent");
		_drop = ResolveNode<DropComponent>(DropPath, "DropComponent");
		_fsm = ResolveNode<StateMachineComponent>(StateMachinePath, "StateMachineComponent");
		_status = GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite != null)
			_baseModulate = _sprite.Modulate;

		SetupAnimator();

		var childNames = new System.Text.StringBuilder();
		foreach (var c in GetChildren())
		{
			childNames.Append(c.Name).Append('(').Append(c.GetType().Name).Append(") ");
		}
		GD.Print($"[EnemyBase] {Name} _Ready: aggro={(_aggro != null)} health={(_health != null)} hitbox={(_hitbox != null)} hurtbox={(_hurtbox != null)} fsm={(_fsm != null)}");
		GD.Print($"[EnemyBase] {Name} children: {childNames}");

		RegisterDefaultStates();

		GD.Print($"[EnemyBase] {Name} post-register: state='{_fsm?.CurrentStateName}'");

		if (_aggro != null)
		{
			_aggro.AggroAcquired += OnAggroAcquired;
			_aggro.AggroLost += OnAggroLost;
		}

		if (_health != null)
		{
			_health.Died += OnDied;
		}

		if (_hurtbox != null)
		{
			_hurtbox.HitAccepted += OnHurtboxHit;
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

		if (_hurtbox != null)
		{
			_hurtbox.HitAccepted -= OnHurtboxHit;
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

	private void SetupAnimator()
	{
		if (_sprite == null || AttackSpriteSheet == null) return;
		_animator = new SpriteAnimator(_sprite, SpriteFrameSize, SpriteFrameSize);
		_animator.RegisterClip("attack", AttackSpriteSheet, 0, 0, AttackFrameCount, defaultFps: 24f, loop: false);
		// Snap to frame 0 of the attack sheet for the static idle/chase pose.
		_animator.Play("attack");
		_animator.Stop();
	}

	public override void _PhysicsProcess(double delta)
	{
		var step = (float)delta;
		TickAttackTimers(step);
		TickFlash(step);
		_animator?.Tick(step);
		if (_jumpCooldownRemaining > 0f)
			_jumpCooldownRemaining = Mathf.Max(0f, _jumpCooldownRemaining - step);

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

		// Hit-stop: let knockback velocity ride out, skip state-driven movement
		if (_hitStopRemaining > 0f)
		{
			_hitStopRemaining -= step;
			MoveAndSlide();
			return;
		}

		var currentState = _fsm?.CurrentStateName ?? string.Empty;
		var target = _aggro?.CurrentTarget;

		if (DebugCombat)
		{
			_debugTimer += step;
			if (_debugTimer >= 1f)
			{
				_debugTimer = 0f;
				var hasTarget = _aggro?.HasTarget ?? false;
				GD.Print($"[Enemy] {Name} state={currentState} hasTarget={hasTarget} target={target?.Name ?? "null"} pos={GlobalPosition}");
			}
		}

		switch (currentState)
		{
			case "Chase":
				UpdateChase(target);
				break;
			case "Attack":
				Velocity = new Vector2(0f, Velocity.Y);
				FaceTarget(target);
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

	private void FaceTarget(Node2D? target)
	{
		if (target == null) return;
		var dx = target.GlobalPosition.X - GlobalPosition.X;
		if (Mathf.Abs(dx) < 0.1f) return;

		var facing = dx < 0f ? -1 : 1;
		if (_sprite != null)
			_sprite.FlipH = facing < 0;
		if (_hitbox != null)
			_hitbox.Position = new Vector2(facing * HitboxForwardOffset, 0f);
	}

	private void OnHurtboxHit(Node attacker, float damage)
	{
		if (DebugCombat)
		{
			var hp = _health?.CurrentHp ?? -1f;
			var max = _health?.MaxHp ?? -1f;
			GD.Print($"[Enemy] {Name} hit by {attacker?.Name} for {damage}. HP {hp}/{max}");
		}

		if (attacker is Node2D attackerNode)
		{
			float dir = Mathf.Sign(GlobalPosition.X - attackerNode.GlobalPosition.X);
			if (dir == 0f) dir = 1f;
			Velocity = new Vector2(dir * HitKnockbackSpeed, -HitKnockbackSpeed * 0.4f);
		}

		_hitStopRemaining = HitStopSeconds;
		_flashRemaining = FlashSeconds;
		if (_sprite != null)
			_sprite.Modulate = new Color(1.6f, 0.6f, 0.6f, 1f);
	}

	private void TickFlash(float step)
	{
		if (_flashRemaining <= 0f || _sprite == null) return;
		_flashRemaining -= step;
		if (_flashRemaining <= 0f)
		{
			_flashRemaining = 0f;
			_sprite.Modulate = _baseModulate;
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
		if (DebugCombat)
			GD.Print($"[Enemy] {Name} aggro acquired → Chase. target={target?.Name}");
		_fsm?.TransitionNow("Chase");
	}

	private void OnAggroLost(Node2D? target)
	{
		if (DebugCombat)
			GD.Print($"[Enemy] {Name} aggro lost → Idle");
		_fsm?.TransitionNow("Idle");
	}

	private void OnDied(Node? source)
	{
		_hitbox?.DeactivateWindow();
		Velocity = Vector2.Zero;
		_fsm?.TransitionNow("Death");

		// Stop being a target / collider while the corpse fades out.
		if (_aggro != null)
			_aggro.Monitoring = false;
		if (_hurtbox != null)
			_hurtbox.Monitoring = false;
		CollisionLayer = 0;

		var timer = GetTree()?.CreateTimer(DespawnDelaySeconds);
		if (timer != null)
			timer.Timeout += QueueFree;
		else
			QueueFree();
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

	private void UpdateChase(Node2D? target)
	{
		if (target == null || !GodotObject.IsInstanceValid(target))
		{
			Velocity = Vector2.Zero;
			_fsm?.TransitionNow("Idle");
			return;
		}

		var offset = target.GlobalPosition - GlobalPosition;
		var horizontalDistance = Mathf.Abs(offset.X);

		FaceTarget(target);

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
		var newVelocity = new Vector2(dirX * MoveSpeed, Velocity.Y);

		if (IsOnFloor() && _jumpCooldownRemaining <= 0f)
		{
			// Jump if target is above us, or if we're horizontally blocked but the target is still away
			var targetIsAbove = -offset.Y > JumpVerticalThreshold;
			var blockedHorizontally = IsOnWall() && Mathf.Abs(Velocity.X) < 1f;
			if (targetIsAbove || blockedHorizontally)
			{
				newVelocity.Y = -JumpVelocity;
				_jumpCooldownRemaining = JumpCooldownSeconds;
			}
		}

		Velocity = newVelocity;
	}

	private bool CanStartAttack()
	{
		return _attackCooldownRemaining <= 0f && _attackWindupRemaining <= 0f && _attackActiveRemaining <= 0f;
	}

	private void StartAttack()
	{
		_fsm?.TransitionNow("Attack");
		_attackCooldownRemaining = AttackCooldownSeconds;
		_attackWindupRemaining = AttackWindupSeconds;
		_attackActiveRemaining = 0f;
		_hitbox?.DeactivateWindow();
		// Stretch the swing animation across windup + active so the visual lands with the hitbox.
		_animator?.Play("attack", AttackWindupSeconds + AttackActiveSeconds);
	}

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
			}
		}

		if (_attackActiveRemaining > 0f)
		{
			_attackActiveRemaining -= step;
			if (_attackActiveRemaining <= 0f)
			{
				_attackActiveRemaining = 0f;
				_hitbox?.DeactivateWindow();
				_animator?.Stop();
				if (_health == null || !_health.IsDead)
				{
					if (_aggro != null && _aggro.HasTarget)
					{
						_fsm?.TransitionNow("Chase");
					}
					else
					{
						_fsm?.TransitionNow("Idle");
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

	private class EnemySimpleState : State
	{
		public EnemySimpleState(string name) : base(name)
		{
		}
	}
}
