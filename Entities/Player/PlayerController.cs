using Godot;
using Sequence.Autoloads;
using Sequence.Components.Ability;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;
using Sequence.Components.Status;

namespace Sequence.Entities.Player;

/// <summary>
/// Player orchestration script for movement and combat input.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	[Export(PropertyHint.Range, "0,2000,1")] public float MoveSpeed { get; set; } = 220f;
	[Export(PropertyHint.Range, "0,2000,1")] public float JumpVelocity { get; set; } = 480f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackWindowSeconds { get; set; } = 0.12f;
	[Export(PropertyHint.Range, "0,1000,0.1")] public float BasicAttackSanityCost { get; set; } = 5f;
	[Export] public NodePath? HealthPath { get; set; }
	[Export] public NodePath? HitboxPath { get; set; }
	[Export] public NodePath? AbilityPath { get; set; }

	private HealthComponent? _health;
	private HitboxComponent? _hitbox;
	private AbilityComponent? _ability;
	private StatusEffectComponent? _status;
	private float _attackWindowRemaining;
	private bool _attackWasPressed;
	private bool _facingRight = true;

	public override void _Ready()
	{
		_health = ResolveNode<HealthComponent>(HealthPath, "HealthComponent");
		_hitbox = ResolveNode<HitboxComponent>(HitboxPath, "HitboxComponent");
		_ability = ResolveNode<AbilityComponent>(AbilityPath, "AbilityComponent");
		_status = GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");

		if (_health != null)
		{
			_health.Died += OnDied;
		}

		if (_hitbox != null)
			_hitbox.Damage = 15f;
	}

	public override void _ExitTree()
	{
		if (_health != null)
		{
			_health.Died -= OnDied;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_attackWindowRemaining > 0f)
		{
			_attackWindowRemaining -= (float)delta;
			if (_attackWindowRemaining <= 0f)
			{
				_attackWindowRemaining = 0f;
				_hitbox?.DeactivateWindow();
			}
		}

		if (_health != null && _health.IsDead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// Gravity
		if (!IsOnFloor())
		{
			Velocity += GetGravity() * (float)delta;
		}

		// Jump
		if (IsOnFloor() && Input.IsActionJustPressed("jump"))
		{
			Velocity = new Vector2(Velocity.X, -JumpVelocity);
		}

		// Horizontal movement only — vertical controlled by gravity/jump
		var moveX = Input.GetAxis("move_left", "move_right");
		if (moveX == 0f)
		{
			moveX = GetKeyboardMoveX();
		}

		if (moveX > 0.1f) _facingRight = true;
		else if (moveX < -0.1f) _facingRight = false;

		var effectiveMoveSpeed = _status != null
			? _status.GetStat("move_speed", MoveSpeed)
			: MoveSpeed;
		Velocity = new Vector2(moveX * effectiveMoveSpeed, Velocity.Y);
		MoveAndSlide();

		var attackPressed = Input.IsActionPressed("attack");
		var attackJustPressed = attackPressed && !_attackWasPressed;
		_attackWasPressed = attackPressed;

		if (attackJustPressed)
		{
			if (_ability == null || _ability.TryActivate("basic_attack", sanityCost: BasicAttackSanityCost))
			{
				FlipHitboxForFacing();
				_hitbox?.ActivateWindow();
				var atkSpeedMult = _status != null ? _status.GetStat("attack_speed_multiplier", 1f) : 1f;
				_attackWindowRemaining = AttackWindowSeconds / Mathf.Max(0.1f, atkSpeedMult);
			}
		}
	}

	public bool FacingRight => _facingRight;

	public void OnAttackWindowStart()
	{
		FlipHitboxForFacing();
		_hitbox?.ActivateWindow();
		_attackWindowRemaining = AttackWindowSeconds;
	}

	public void OnAttackWindowEnd()
	{
		_attackWindowRemaining = 0f;
		_hitbox?.DeactivateWindow();
	}

	private void FlipHitboxForFacing()
	{
		if (_hitbox == null) return;
		var shape = _hitbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape == null) return;
		var scale = shape.Scale;
		scale.X = _facingRight ? 1f : -1f;
		shape.Scale = scale;
	}

	private void OnDied(Node? source)
	{
		_hitbox?.DeactivateWindow();
		RunManager.Instance?.EndRun(isVictory: false);
	}

	private T? ResolveNode<T>(NodePath? path, string fallbackName) where T : Node
	{
		if (path != null && !path.IsEmpty)
		{
			return GetNodeOrNull<T>(path);
		}

		return GetNodeOrNull<T>(fallbackName);
	}

	private float GetKeyboardMoveX()
	{
		var x = 0f;
		if (Input.IsKeyPressed(Key.A)) x -= 1f;
		if (Input.IsKeyPressed(Key.D)) x += 1f;
		return x;
	}
}
