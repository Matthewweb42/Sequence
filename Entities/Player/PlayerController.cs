using Godot;
using Sequence.Autoloads;
using Sequence.Components.Ability;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;

namespace Sequence.Entities.Player;

/// <summary>
/// Player orchestration script for movement and combat input.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	[Export(PropertyHint.Range, "0,2000,1")] public float MoveSpeed { get; set; } = 220f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackWindowSeconds { get; set; } = 0.12f;
	[Export(PropertyHint.Range, "0,1000,0.1")] public float BasicAttackSanityCost { get; set; } = 5f;
	[Export] public NodePath? HealthPath { get; set; }
	[Export] public NodePath? HitboxPath { get; set; }
	[Export] public NodePath? AbilityPath { get; set; }

	private HealthComponent? _health;
	private HitboxComponent? _hitbox;
	private AbilityComponent? _ability;
	private float _attackWindowRemaining;
	private bool _attackWasPressed;

	public override void _Ready()
	{
		_health = ResolveNode<HealthComponent>(HealthPath, "HealthComponent");
		_hitbox = ResolveNode<HitboxComponent>(HitboxPath, "HitboxComponent");
		_ability = ResolveNode<AbilityComponent>(AbilityPath, "AbilityComponent");

		if (_health != null)
		{
			_health.Died += OnDied;
		}
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

		var moveInput = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (moveInput == Vector2.Zero)
		{
			moveInput = GetKeyboardMoveFallback();
		}
		Velocity = moveInput * MoveSpeed;
		MoveAndSlide();

		var attackPressed = Input.IsActionPressed("attack") || Input.IsKeyPressed(Key.Space);
		var attackJustPressed = attackPressed && !_attackWasPressed;
		_attackWasPressed = attackPressed;

		if (attackJustPressed)
		{
			if (_ability == null || _ability.TryActivate("basic_attack", sanityCost: BasicAttackSanityCost))
			{
				_hitbox?.ActivateWindow();
				_attackWindowRemaining = AttackWindowSeconds;
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
		{
			return GetNodeOrNull<T>(path);
		}

		return GetNodeOrNull<T>(fallbackName);
	}

	private Vector2 GetKeyboardMoveFallback()
	{
		var x = 0f;
		var y = 0f;

		if (Input.IsKeyPressed(Key.A)) x -= 1f;
		if (Input.IsKeyPressed(Key.D)) x += 1f;
		if (Input.IsKeyPressed(Key.W)) y -= 1f;
		if (Input.IsKeyPressed(Key.S)) y += 1f;

		var v = new Vector2(x, y);
		return v == Vector2.Zero ? Vector2.Zero : v.Normalized();
	}
}

