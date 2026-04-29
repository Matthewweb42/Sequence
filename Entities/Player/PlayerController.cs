using Godot;
using Sequence.Autoloads;
using Sequence.Components.Ability;
using Sequence.Components.Animation;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;
using Sequence.Components.Hurtbox;
using Sequence.Components.Sequence;
using Sequence.Components.Status;

namespace Sequence.Entities.Player;

public partial class PlayerController : CharacterBody2D
{
	[Export(PropertyHint.Range, "0,2000,1")] public float MoveSpeed { get; set; } = 350f;
	[Export(PropertyHint.Range, "0,3000,1")] public float JumpVelocity { get; set; } = 1000f;
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float AttackWindowSeconds { get; set; } = 0.12f;
	[Export(PropertyHint.Range, "0,2,0.01")] public float AttackCooldownSeconds { get; set; } = 0.25f;
	[Export(PropertyHint.Range, "0,1000,0.1")] public float BasicAttackSanityCost { get; set; } = 5f;
	[Export(PropertyHint.Range, "0,500,1")] public float HitboxForwardOffset { get; set; } = 20f;
	[Export(PropertyHint.Range, "0,2000,1")] public float HitKnockbackSpeed { get; set; } = 260f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float HitStopSeconds { get; set; } = 0.08f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ScreenShakeSeconds { get; set; } = 0.18f;
	[Export(PropertyHint.Range, "0,64,0.1")] public float ScreenShakeAmplitude { get; set; } = 6f;
	// Multiplier applied to gravity when the player is falling (>1 = faster fall)
	[Export(PropertyHint.Range, "1,5,0.1")] public float FallGravityMultiplier { get; set; } = 2.0f;
	// Cut jump height when jump key is released early
	[Export(PropertyHint.Range, "0,1,0.05")] public float JumpCutFraction { get; set; } = 0.5f;
	[Export] public bool DebugCombat { get; set; }
	[Export] public NodePath? HealthPath { get; set; }
	[Export] public NodePath? HitboxPath { get; set; }
	[Export] public NodePath? AbilityPath { get; set; }
	[Export] public NodePath? HurtboxPath { get; set; }
	[Export] public Texture2D? IdleSpriteSheet { get; set; }
	[Export] public Texture2D? RunSpriteSheet { get; set; }
	[Export] public Texture2D? AttackSpriteSheet { get; set; }
	[Export(PropertyHint.Range, "16,512,1")] public int SpriteFrameSize { get; set; } = 192;
	[Export(PropertyHint.Range, "1,30,1")] public float IdleFps { get; set; } = 10f;
	[Export(PropertyHint.Range, "1,30,1")] public float RunFps { get; set; } = 12f;

	private HealthComponent? _health;
	private HitboxComponent? _hitbox;
	private HurtboxComponent? _hurtbox;
	private AbilityComponent? _ability;
	private StatusEffectComponent? _status;
	private Sprite2D? _sprite;
	private Camera2D? _camera;
	private SpriteAnimator? _animator;
	private SequenceComponent? _sequence;
	private float _attackWindowRemaining;
	private float _attackCooldownRemaining;
	private bool _jumpHeld;
	private int _facing = 1;
	private float _hitStopRemaining;
	private float _shakeRemaining;
	private bool _f11Pressed;

	public override void _Ready()
	{
		_health = ResolveNode<HealthComponent>(HealthPath, "HealthComponent");
		_hitbox = ResolveNode<HitboxComponent>(HitboxPath, "HitboxComponent");
		_hurtbox = ResolveNode<HurtboxComponent>(HurtboxPath, "HurtboxComponent");
		_ability = ResolveNode<AbilityComponent>(AbilityPath, "AbilityComponent");
		_status = GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		_camera = GetNodeOrNull<Camera2D>("Camera2D");
		_sequence = GetNodeOrNull<SequenceComponent>("SequenceComponent");

		SetupAnimator();

		if (_health != null)
			_health.Died += OnDied;

		if (_hurtbox != null)
			_hurtbox.HitAccepted += OnHurtboxHit;

		ApplyFacingToHitbox();
	}

	private void SetupAnimator()
	{
		if (_sprite == null) return;

		_animator = new SpriteAnimator(_sprite, SpriteFrameSize, SpriteFrameSize);

		if (IdleSpriteSheet != null)
			_animator.RegisterClip("idle", IdleSpriteSheet, 0, 0, 8, IdleFps, loop: true);
		if (RunSpriteSheet != null)
			_animator.RegisterClip("run", RunSpriteSheet, 0, 0, 6, RunFps, loop: true);
		if (AttackSpriteSheet != null)
			_animator.RegisterClip("attack", AttackSpriteSheet, 0, 0, 4, defaultFps: 24f, loop: false);

		// Default to idle so the sprite has something on screen even before input.
		if (IdleSpriteSheet != null)
			_animator.Play("idle");
	}

	public override void _ExitTree()
	{
		if (_health != null)
			_health.Died -= OnDied;

		if (_hurtbox != null)
			_hurtbox.HitAccepted -= OnHurtboxHit;
	}

	public override void _PhysicsProcess(double delta)
	{
		var step = (float)delta;

		// Debug: F12 advances sequence (bypasses shrine + materials).
		if (Input.IsActionJustPressed("debug_advance_sequence"))
		{
			if (_sequence != null && _sequence.TryAdvance())
				GD.Print($"[Debug] Sequence advanced to {_sequence.CurrentSequence}");
			else
				GD.Print("[Debug] Sequence advance blocked (already at final).");
		}

		// Debug: F11 teleports directly to the boss room.
		var f11Down = Input.IsKeyPressed(Key.F11);
		if (f11Down && !_f11Pressed)
		{
			var graph = World.RoomGraph.Instance;
			if (graph != null)
			{
				foreach (var room in graph.AllRooms)
				{
					if (room.Archetype == World.RoomArchetype.BossRoom)
					{
						GD.Print($"[Debug] Teleporting to BossRoom {room.RoomId}");
						RunManager.Instance?.TransitionToRoom(room.RoomId);
						break;
					}
				}
			}
		}
		_f11Pressed = f11Down;

		if (_attackWindowRemaining > 0f)
		{
			_attackWindowRemaining -= step;
			if (_attackWindowRemaining <= 0f)
			{
				_attackWindowRemaining = 0f;
				_hitbox?.DeactivateWindow();
			}
		}

		if (_attackCooldownRemaining > 0f)
		{
			_attackCooldownRemaining = Mathf.Max(0f, _attackCooldownRemaining - step);
		}

		TickScreenShake(step);

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

		// Hit-stop: suppress input-driven movement briefly, let knockback ride out
		if (_hitStopRemaining > 0f)
		{
			_hitStopRemaining -= step;
			MoveAndSlide();
			return;
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

		if (moveX > 0f && _facing != 1)
		{
			_facing = 1;
			ApplyFacingToHitbox();
		}
		else if (moveX < 0f && _facing != -1)
		{
			_facing = -1;
			ApplyFacingToHitbox();
		}

		var effectiveMoveSpeed = _status != null
			? _status.GetStat("move_speed", MoveSpeed)
			: MoveSpeed;
		Velocity = new Vector2(moveX * effectiveMoveSpeed, Velocity.Y);
		MoveAndSlide();

		// Attack — held button auto-repeats; cooldown gate paces the swings.
		var attackHeld = Input.IsActionPressed("attack");

		if (attackHeld && _attackCooldownRemaining <= 0f && _attackWindowRemaining <= 0f)
		{
			// Basic attack is always free; don't gate it on AbilityComponent unlocks.
			_hitbox?.ActivateWindow();
			var atkSpeedMult = _status != null ? _status.GetStat("attack_speed_multiplier", 1f) : 1f;
			_attackWindowRemaining = AttackWindowSeconds / Mathf.Max(0.1f, atkSpeedMult);
			_attackCooldownRemaining = AttackCooldownSeconds / Mathf.Max(0.1f, atkSpeedMult);

			_animator?.Play("attack", _attackWindowRemaining);

			if (DebugCombat)
				GD.Print($"[Player] Swing facing={_facing}");
		}

		UpdateAnimationState(moveX);
		_animator?.Tick(step);
	}

	private void UpdateAnimationState(float moveX)
	{
		if (_animator == null) return;

		// Don't override the attack swing while it's still in its window.
		if (_attackWindowRemaining > 0f && _animator.CurrentClip == "attack")
			return;

		var moving = Mathf.Abs(moveX) > 0.01f;
		var desired = moving ? "run" : "idle";
		if (_animator.CurrentClip != desired)
			_animator.Play(desired);
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

	private void ApplyFacingToHitbox()
	{
		if (_hitbox != null)
			_hitbox.Position = new Vector2(_facing * HitboxForwardOffset, 0f);

		if (_sprite != null)
			_sprite.FlipH = _facing < 0;
	}

	private void OnHurtboxHit(Node attacker, float damage)
	{
		if (DebugCombat)
		{
			var hp = _health?.CurrentHp ?? -1f;
			var max = _health?.MaxHp ?? -1f;
			GD.Print($"[Player] Hit by {attacker?.Name} for {damage}. HP {hp}/{max}");
		}

		// Apply knockback away from attacker
		if (attacker is Node2D attackerNode)
		{
			var dir = Mathf.Sign(GlobalPosition.X - attackerNode.GlobalPosition.X);
			if (dir == 0f) dir = -_facing;
			Velocity = new Vector2(dir * HitKnockbackSpeed, -HitKnockbackSpeed * 0.5f);
		}

		_hitStopRemaining = HitStopSeconds;
		_shakeRemaining = ScreenShakeSeconds;
	}

	private void TickScreenShake(float step)
	{
		if (_camera == null) return;

		if (_shakeRemaining > 0f)
		{
			_shakeRemaining -= step;
			var amp = ScreenShakeAmplitude * Mathf.Max(0f, _shakeRemaining / Mathf.Max(0.001f, ScreenShakeSeconds));
			_camera.Offset = new Vector2(
				(GD.Randf() * 2f - 1f) * amp,
				(GD.Randf() * 2f - 1f) * amp);
			if (_shakeRemaining <= 0f)
			{
				_shakeRemaining = 0f;
				_camera.Offset = Vector2.Zero;
			}
		}
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
