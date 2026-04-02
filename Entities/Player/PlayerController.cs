using Godot;
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
	[Export] public NodePath HealthPath { get; set; }
	[Export] public NodePath HitboxPath { get; set; }
	[Export] public NodePath AbilityPath { get; set; }

	private HealthComponent _health;
	private HitboxComponent _hitbox;
	private AbilityComponent _ability;

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
		if (_health != null && _health.IsDead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		var moveInput = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = moveInput * MoveSpeed;
		MoveAndSlide();

		if (Input.IsActionJustPressed("attack"))
		{
			if (_ability == null || _ability.TryActivate("basic_attack"))
			{
				_hitbox?.ActivateWindow();
			}
		}
	}

	public void OnAttackWindowStart()
	{
		_hitbox?.ActivateWindow();
	}

	public void OnAttackWindowEnd()
	{
		_hitbox?.DeactivateWindow();
	}

	private void OnDied(Node source)
	{
		_hitbox?.DeactivateWindow();
	}

	private T ResolveNode<T>(NodePath path, string fallbackName) where T : Node
	{
		if (path != null && !path.IsEmpty)
		{
			return GetNodeOrNull<T>(path);
		}

		return GetNodeOrNull<T>(fallbackName);
	}
}

