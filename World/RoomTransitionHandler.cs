using Godot;

namespace Sequence.World;

/// <summary>
/// Manages room transitions triggered by ConnectionPoint entry.
/// Attached to ConnectionPoint Area2D markers to detect when the player
/// enters them and transition to adjacent rooms.
/// </summary>
public partial class RoomTransitionHandler : Area2D
{
	// ─────────────────────────────────────────────
	//  Properties
	// ─────────────────────────────────────────────

	/// <summary>The direction this connection point leads to.</summary>
	public Direction Direction { get; set; }

	/// <summary>The room instance this connection belongs to.</summary>
	private RoomInstance? _parentRoom;

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Godot Lifecycle
	// ═══════════════════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		// Extract direction from node name (North, South, East, West)
		string nodeName = Name.ToString();
		if (!Direction.TryParse(nodeName, ignoreCase: true, out Direction dir))
		{
			dir = Direction.North; // Default
			GD.PrintErr($"[RoomTransitionHandler] Cannot parse direction from node name '{nodeName}'. Defaulting to North.");
		}
		Direction = dir;

		// Cache parent room
		_parentRoom = GetParent<Node2D>()?.GetParent<RoomInstance>();

		// Connect to area entry signal
		AreaEntered += OnAreaEntered;
	}

	public override void _ExitTree()
	{
		AreaEntered -= OnAreaEntered;
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Trigger Handlers
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Called when an object enters this connection point.
	/// If it's the player, attempt to transition to the adjacent room.
	/// </summary>
	private void OnAreaEntered(Area2D area)
	{
		var runManager = RunManager.Instance;
		if (runManager?.Player == null || _parentRoom == null)
		{
			return;
		}

		// Check if the entering object is the player's hurtbox/collision area
		if (area == runManager.Player || area.IsAncestorOf(runManager.Player) || runManager.Player.IsAncestorOf(area))
		{
			AttemptTransition();
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Transition Logic
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Attempt to transition to the adjacent room in this direction.
	/// Validates that:
	/// 1. An adjacent room exists
	/// 2. It's reachable given the player's current Sequence level
	/// 3. No doors are locked along the path
	/// </summary>
	private void AttemptTransition()
	{
		var runManager = RunManager.Instance;
		var roomGraph = runManager?.RoomGraph;

		if (runManager == null || roomGraph == null || _parentRoom == null)
		{
			return;
		}

		// Find adjacent room in this direction
		RoomNode? adjacentRoom = roomGraph.GetAdjacentRoomInDirection(_parentRoom.RoomId, Direction);
		if (adjacentRoom == null)
		{
			GD.Print($"[RoomTransitionHandler] No adjacent room in direction {Direction}");
			return;
		}

		// Get player's current Sequence level
		var player = runManager.Player;
		int playerSequence = 9; // Default; should query from SequenceComponent if available
		
		// TODO: Get actual Sequence from player's SequenceComponent
		// For now, assume sequence 5 for testing purposes
		playerSequence = 5;

		// Check if the adjacent room is reachable
		if (!roomGraph.IsRoomReachable(adjacentRoom.RoomId, playerSequence))
		{
			GD.Print($"[RoomTransitionHandler] Room {adjacentRoom.RoomId} is not reachable from {_parentRoom.RoomId} (locked doors)");
			// TODO: Show lock indicator to player
			return;
		}

		// Transition to the adjacent room
		// The entry direction is the opposite of the exit direction
		Direction entryDirection = GetOppositeDirection(Direction);
		runManager.TransitionToRoom(adjacentRoom.RoomId, entryDirection);
	}

	/// <summary>
	/// Get the opposite cardinal direction.
	/// </summary>
	private Direction GetOppositeDirection(Direction dir)
	{
		return dir switch
		{
			Direction.North => Direction.South,
			Direction.South => Direction.North,
			Direction.East => Direction.West,
			Direction.West => Direction.East,
			_ => Direction.North,
		};
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Utility
	// ═══════════════════════════════════════════════════════════════════════════════════

	public override string ToString()
	{
		return $"RoomTransitionHandler({Direction})";
	}
}
