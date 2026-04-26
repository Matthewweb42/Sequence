using Godot;
using Sequence.Autoloads;
using Sequence.World;

namespace Sequence.Entities.Interactables;

/// <summary>
/// Handles room transitions triggered by a door when the player presses the up key.
/// Attached to Door entities (or as a component) to detect proximity and input,
/// then transition to adjacent rooms.
/// </summary>
public partial class DoorTransitionHandler : Area2D
{
	// ─────────────────────────────────────────────
	//  Properties
	// ─────────────────────────────────────────────

	/// <summary>The direction this door leads to. Derived from node name at runtime.</summary>
	public Direction Direction { get; private set; } = Direction.North;

	/// <summary>The room instance this door belongs to.</summary>
	private RoomInstance? _parentRoom;

	/// <summary>Whether the player is currently in proximity to this door.</summary>
	private bool _playerInProximity = false;

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Godot Lifecycle
	// ═══════════════════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		// Derive direction from node name (North/South/East/West)
		string nodeName = Name.ToString();
		if (Direction.TryParse(nodeName, ignoreCase: true, out Direction dir))
		{
			Direction = dir;
		}
		else
		{
			GD.PrintErr($"[DoorTransitionHandler] Cannot parse direction from node name '{nodeName}'. Defaulting to North.");
		}

		// ConnectionPoints is a plain Node (not Node2D), so use untyped GetParent for that step
		_parentRoom = GetParent()?.GetParent<RoomInstance>();

		// Connect to body entry/exit signals (player is CharacterBody2D, not Area2D)
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _ExitTree()
	{
		BodyEntered -= OnBodyEntered;
		BodyExited -= OnBodyExited;
	}

	public override void _Input(InputEvent @event)
	{
		// Check if player pressed W while in proximity
		if (_playerInProximity && @event is InputEventKey key && key.PhysicalKeycode == Key.W && key.Pressed && !key.Echo)
		{
			AttemptTransition();
			GetViewport().SetInputAsHandled();
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Proximity Detection
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Called when a body enters this door's proximity area.
	/// </summary>
	private void OnBodyEntered(Node2D body)
	{
		var runManager = RunManager.Instance;
		if (runManager?.Player == null)
		{
			return;
		}

		if (body == runManager.Player)
		{
			_playerInProximity = true;
			GD.Print($"[DoorTransitionHandler] Player in proximity to door {Direction}");
		}
	}

	/// <summary>
	/// Called when a body exits this door's proximity area.
	/// </summary>
	private void OnBodyExited(Node2D body)
	{
		var runManager = RunManager.Instance;
		if (runManager?.Player == null)
		{
			return;
		}

		if (body == runManager.Player)
		{
			_playerInProximity = false;
			GD.Print($"[DoorTransitionHandler] Player left door {Direction}");
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Transition Logic
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Attempt to transition to the adjacent room in this door's direction.
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

		RoomNode? adjacentRoom = roomGraph.GetAdjacentRoomInDirection(_parentRoom.RoomId, Direction);
		if (adjacentRoom == null)
		{
			GD.Print($"[DoorTransitionHandler] No adjacent room in direction {Direction}");
			return;
		}

		// Reject if rooms are only spatially adjacent but have no actual graph edge
		if (!roomGraph.AreRoomsConnected(_parentRoom.RoomId, adjacentRoom.RoomId))
		{
			GD.Print($"[DoorTransitionHandler] Room {adjacentRoom.RoomId} is not connected to {_parentRoom.RoomId}");
			return;
		}

		// Check the immediate edge directly — refuse if this specific door is locked
		DoorInfo? door = roomGraph.GetDoorBetween(_parentRoom.RoomId, adjacentRoom.RoomId);
		if (door != null && door.IsLocked)
		{
			GD.Print($"[DoorTransitionHandler] Door to room {adjacentRoom.RoomId} is locked (requires Sequence ≤ {door.RequiredSequence})");
			return;
		}

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
		return $"DoorTransitionHandler({Direction})";
	}
}
