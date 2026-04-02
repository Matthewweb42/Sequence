using Godot;
using Sequence.Components.Health;

namespace Sequence.Components.Drop;

/// <summary>
/// Emits drop intent when owning entity dies.
/// </summary>
public partial class DropComponent : Node
{
	[Signal] public delegate void DropRequestedEventHandler(Node ownerEntity, Vector2 worldPosition);

	[Export] public NodePath HealthPath { get; set; }

	private HealthComponent _health;

	public override void _Ready()
	{
		_health = ResolveHealth();
		if (_health != null)
		{
			_health.Died += OnOwnerDied;
		}
	}

	public override void _ExitTree()
	{
		if (_health != null)
		{
			_health.Died -= OnOwnerDied;
		}
	}

	private void OnOwnerDied(Node source)
	{
		var ownerEntity = GetParent() ?? this;
		var worldPosition = ownerEntity is Node2D node2D ? node2D.GlobalPosition : Vector2.Zero;
		EmitSignal(SignalName.DropRequested, ownerEntity, worldPosition);
	}

	private HealthComponent ResolveHealth()
	{
		if (HealthPath != null && !HealthPath.IsEmpty)
		{
			return GetNodeOrNull<HealthComponent>(HealthPath);
		}

		return GetParent()?.GetNodeOrNull<HealthComponent>("HealthComponent");
	}
}

