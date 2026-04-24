using Godot;
using Sequence.Autoloads;
using Sequence.Components.Ability;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;
using Sequence.Components.Status;

namespace Sequence.Entities.Player;

public partial class PlayerController : CharacterBody2D
{
	[Export(PropertyHint.Range, "0,2000,1")] public float MoveSpeed { get; set; } = 350f;
	[Export(PropertyHint.Range, "0,3000,1")] public float JumpVelocity { get; set; } = 1000f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackWindowSeconds { get; set; } = 0.12f;
	[Export(PropertyHint.Range, "0,1000,0.1")] public float BasicAttackSanityCost { get; set; } = 5f;
	// Multiplier applied to gravity when the player is falling (>1 = faster fall)
	[Export(PropertyHint.Range, "1,5,0.1")] public float FallGravityMultiplier { get; set; } = 2.0f;
	// Cut jump height when jump key is released early
	[Export(PropertyHint.Range, "0,1,0.05")] public float JumpCutFraction { get; set; } = 0.5f;
	[Export] public NodePath? HealthPath { get; set; }
	[Export] public NodePath? HitboxPath { get; set; }
	[Export] public NodePath? AbilityPath { get; set; }

	private HealthComponent? _health;
	private HitboxComponent? _hitbox;
	private AbilityComponent? _ability;
	private StatusEffectComponent? _status;
	private float _attackWindowRemaining;
	private bool _attackWasPressed;
	private bool _jumpHeld;

	public override void _Ready()
	{
		_health = ResolveNode<HealthComponent>(HealthPath, "HealthComponent");
		_hitbox = ResolveNode<HitboxComponent>(HitboxPath, "HitboxComponent");
		_ability = ResolveNode<AbilityComponent>(AbilityPath, "AbilityComponent");
		_status = GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");

		if (_health != null)
			_health.Died += OnDied;
	}

	public override void _ExitTree()
	{
		if (_health != null)
			_health.Died -= OnDied;
	}

	public override void _PhysicsProcess(double delta)
	{
		var step = (float)delta;

		if (_attackWindowRemaining > 0f)
		{
			_attackWindowRemaining -= step;
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

		var gravity = GetGravity();

		// Apply gravity — use a higher multiplier when falling or when jump key released early
		if (!IsOnFloor())
		{
			var gravScale = (Velocity.Y > 0f || !_jumpHeld) ? FallGravityMultiplier : 1f;
			Velocity += gravity * gravScale * step;
		}

		// Jump — only from floor
		if (IsOnFloor() && Input.IsActionJustPressed("jump"))
		{
			Velocity = new Vector2(Velocity.X, -JumpVelocity);
			_jumpHeld = true;
		}

		// Variable jump height: cut velocity when key released mid-air
		if (_jumpHeld && Input.IsActionJustReleased("jump") && Velocity.Y < 0f)
		{
			Velocity = new Vector2(Velocity.X, Velocity.Y * JumpCutFraction);
			_jumpHeld = false;
		}

		if (IsOnFloor())
			_jumpHeld = false;

		// Horizontal movement only
		var moveX = Input.GetAxis("move_left", "move_right");
		if (moveX == 0f)
			moveX = GetKeyboardMoveX();

		var effectiveMoveSpeed = _status != null
			? _status.GetStat("move_speed", MoveSpeed)
			: MoveSpeed;
		Velocity = new Vector2(moveX * effectiveMoveSpeed, Velocity.Y);
		MoveAndSlide();

		// Attack
		var attackPressed = Input.IsActionPressed("attack");
		var attackJustPressed = attackPressed && !_attackWasPressed;
		_attackWasPressed = attackPressed;

		if (attackJustPressed)
		{
			if (_ability == null || _ability.TryActivate("basic_attack", sanityCost: BasicAttackSanityCost))
			{
				_hitbox?.ActivateWindow();
				var atkSpeedMult = _status != null ? _status.GetStat("attack_speed_multiplier", 1f) : 1f;
				_attackWindowRemaining = AttackWindowSeconds / Mathf.Max(0.1f, atkSpeedMult);
			}
		}
	}

	public void OnAttackWindowStart()
	{
		_hitbox?.ActivateWindow();
		_attackWindowRemaining = AttackWindowSeconds;
	}

	public void OnAttackWindowEnd()
	{
		_attackWindowRemaining = 0f;
		_hitbox?.DeactivateWindow();
	}

	private void OnDied(Node? source)
	{
		_hitbox?.DeactivateWindow();
		RunManager.Instance?.EndRun(isVictory: false);
	}

	private T? ResolveNode<T>(NodePath? path, string fallbackName) where T : Node
	{
		if (path != null && !path.IsEmpty)
			return GetNodeOrNull<T>(path);
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
