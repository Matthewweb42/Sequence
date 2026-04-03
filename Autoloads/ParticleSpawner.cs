using Godot;

namespace Sequence.Autoloads;

/// <summary>
/// Global utility for one-shot particle and visual effect spawning.
/// </summary>
public partial class ParticleSpawner : Node
{
	public static ParticleSpawner? Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public Node? SpawnPackedScene(PackedScene? scene, Vector2 worldPosition, Node? parent = null)
	{
		if (scene == null)
		{
			return null;
		}

		var node = scene.Instantiate<Node>();
		if (node is Node2D node2D)
		{
			node2D.GlobalPosition = worldPosition;
		}

		var targetParent = parent ?? GetTree()?.CurrentScene;
		targetParent?.AddChild(node);
		return node;
	}
}

