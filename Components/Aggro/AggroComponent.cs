using Godot;
using System.Collections.Generic;
using Sequence.Autoloads;

namespace Sequence.Components.Aggro;

/// <summary>
/// Detects and tracks a target for AI systems with optional line-of-sight gating.
/// </summary>
public partial class AggroComponent : Area2D
{
	[Signal] public delegate void AggroAcquiredEventHandler(Node2D target);
	[Signal] public delegate void AggroLostEventHandler(Node2D? target);

	[Export] public bool RequireLineOfSight { get; set; } = true;
	[Export(PropertyHint.Range, "0,5,0.05")] public float LoseAggroDelaySeconds { get; set; } = 0.75f;
	[Export(PropertyHint.Layers2DPhysics)] public uint OcclusionMask { get; set; } = uint.MaxValue;
	[Export] public NodePath? ExplicitTargetPath { get; set; }

	private readonly HashSet<Node2D> _candidates = new();
	private Node2D? _currentTarget;
	private float _timeSinceLastSeen;

	public bool HasTarget => _currentTarget != null;
	public Node2D? CurrentTarget => _currentTarget;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		AreaEntered += OnAreaEntered;
		AreaExited += OnAreaExited;
		CallDeferred(nameof(SeedInitialCandidates));

		if (ExplicitTargetPath != null && !ExplicitTargetPath.IsEmpty)
		{
			_currentTarget = GetNodeOrNull<Node2D>(ExplicitTargetPath);
			if (_currentTarget != null)
			{
				AcquireTarget(_currentTarget);
			}
		}
	}

	private void SeedInitialCandidates()
	{
		foreach (var body in GetOverlappingBodies())
		{
			if (body is Node2D node)
			{
				_candidates.Add(node);
			}
		}

		foreach (var area in GetOverlappingAreas())
		{
			if (area is Node2D node)
			{
				_candidates.Add(node);
			}
		}
	}

	public override void _Process(double delta)
	{
		var step = (float)delta;
		var visibleTarget = ResolveVisibleTarget();

		if (visibleTarget != null)
		{
			_timeSinceLastSeen = 0f;
			if (_currentTarget != visibleTarget)
			{
				AcquireTarget(visibleTarget);
			}

			return;
		}

		if (_currentTarget == null)
		{
			return;
		}

		_timeSinceLastSeen += step;
		if (_timeSinceLastSeen >= LoseAggroDelaySeconds)
		{
			LoseTarget();
		}
	}

	private Node2D? ResolveVisibleTarget()
	{
		foreach (var candidate in _candidates)
		{
			if (!GodotObject.IsInstanceValid(candidate))
			{
				continue;
			}

			if (!RequireLineOfSight || HasLineOfSight(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	private bool HasLineOfSight(Node2D target)
	{
		if (target == null)
		{
			return false;
		}

		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, target.GlobalPosition);
		query.CollisionMask = OcclusionMask;
		query.Exclude = new Godot.Collections.Array<Rid>
		{
			GetRid()
		};

		if (target is CollisionObject2D collisionTarget)
		{
			query.Exclude.Add(collisionTarget.GetRid());
		}

		var result = spaceState.IntersectRay(query);
		return result.Count == 0;
	}

	private void AcquireTarget(Node2D target)
	{
		_currentTarget = target;
		_timeSinceLastSeen = 0f;
		EmitSignal(SignalName.AggroAcquired, target);
		SignalBus.Instance?.PublishAggroAcquired(GetParent() ?? this, target);
	}

	private void LoseTarget()
	{
		var lost = _currentTarget;
		if (lost == null)
		{
			return;
		}

		_currentTarget = null;
		_timeSinceLastSeen = 0f;
		EmitSignal(SignalName.AggroLost, lost);
		SignalBus.Instance?.PublishAggroLost(GetParent() ?? this, lost);
	}

	private void OnBodyEntered(Node2D body)
	{
		_candidates.Add(body);
	}

	private void OnBodyExited(Node2D body)
	{
		_candidates.Remove(body);
		if (_currentTarget == body)
		{
			_timeSinceLastSeen = LoseAggroDelaySeconds;
		}
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is Node2D node)
		{
			_candidates.Add(node);
		}
	}

	private void OnAreaExited(Area2D area)
	{
		if (area is Node2D node)
		{
			_candidates.Remove(node);
			if (_currentTarget == node)
			{
				_timeSinceLastSeen = LoseAggroDelaySeconds;
			}
		}
	}
}

