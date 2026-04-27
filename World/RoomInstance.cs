using Godot;
using System.Collections.Generic;
using Sequence.Autoloads;
using Sequence.Entities.Interactables;

namespace Sequence.World;

/// <summary>
/// A loaded room scene instance. Manages room state (cleared, visited), 
/// tracks enemies, emits signals when cleared, and provides connection points
/// for player transitions between rooms.
/// </summary>
public partial class RoomInstance : Node2D
{
	// ─────────────────────────────────────────────
	//  Exported Properties (set in inspector or by creator)
	// ─────────────────────────────────────────────

	/// <summary>The RoomId registered in RoomGraph.</summary>
	public int RoomId { get; set; }

	/// <summary>The Sequence branch this room belongs to.</summary>
	public int BranchId { get; set; }

	/// <summary>Whether all enemies in this room have been defeated.</summary>
	public bool IsCleared { get; private set; }

	/// <summary>Whether the player has entered this room at least once.</summary>
	public bool IsVisited { get; set; }

	/// <summary>The archetype of this room (Combat, Shrine, Material, etc.).</summary>
	public RoomArchetype Archetype { get; set; }

	/// <summary>
	/// In the current demo flow, this keeps newly spawned enemies close enough
	/// to immediately detect and chase the player.
	/// </summary>
	[Export(PropertyHint.Range, "0,2000,1")]
	public float EnemySpawnAggroDistanceCap { get; set; } = 0f;

	// ─────────────────────────────────────────────
	//  Runtime State
	// ─────────────────────────────────────────────

	/// <summary>Current number of alive enemies in this room.</summary>
	private int _aliveEnemyCount = 0;

	/// <summary>
	/// Maps cardinal directions to their connection point markers.
	/// Used for room transitions (player moves North/South/East/West to adjacent rooms).
	/// </summary>
	private Dictionary<Direction, Node2D> _connectionPoints = new();

	/// <summary>
	/// Array of SequenceDoor nodes in this room, if any.
	/// These are gated doors that separate branches or cross-connections.
	/// </summary>
	private Node[] _sequenceDoors = new Node[0];

	/// <summary>The shrine in this room, if archetype is SequenceShrine.</summary>
	private SequenceShrine? _shrine;

	/// <summary>The heal interactable in this room, if archetype is Healing.</summary>
	private HealInteractable? _heal;

	/// <summary>
	/// Node containing all enemy spawn points (for visual debugging or configurability).
	/// </summary>
	private Node? _enemySpawnPoints;

	/// <summary>
	/// Node containing all material pickup nodes (loot, materials, etc.).
	/// </summary>
	private Node? _materialNodes;

	/// <summary>The tilemap visual for this room.</summary>
	private TileMapLayer? _tilemap;

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Godot Lifecycle
	// ═══════════════════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		// Mark as visited on first room load
		IsVisited = true;

		// Propagate to the persistent graph node so the mini-map can see it after this room unloads.
		var node = RoomGraph.Instance?.GetRoom(RoomId);
		if (node != null)
		{
			node.IsVisited = true;
		}

		// Cache child nodes
		CacheChildNodes();

		// Connect to enemy death signal and shrine consumption
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.EntityDied += OnEnemyDied;
		}

		if (_shrine != null)
		{
			_shrine.ShrineUsed += OnShrineUsed;

			var roomNode = RoomGraph.Instance?.GetRoom(RoomId);
			if (roomNode?.IsShrineUsed == true)
			{
				_shrine.QueueFree();
				_shrine = null;
			}
		}

		if (_heal != null)
		{
			_heal.HealUsed += OnHealUsed;

			var roomNode = RoomGraph.Instance?.GetRoom(RoomId);
			if (roomNode?.IsHealingUsed == true)
			{
				_heal.QueueFree();
				_heal = null;
			}
		}

		// Remove already-collected material pickups and track new ones
		ApplyCollectedMaterialState();

		// Count initial enemies in the room
		CountEnemies();

		// NOTE: ConfigureConnectionPoints is called by RunManager.TransitionToRoom
		// after the room is in the tree, since it needs the RoomGraph reference.
	}

	public override void _ExitTree()
	{
		// Disconnect from signals
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.EntityDied -= OnEnemyDied;
		}

		if (_shrine != null)
		{
			_shrine.ShrineUsed -= OnShrineUsed;
		}

		if (_heal != null)
		{
			_heal.HealUsed -= OnHealUsed;
		}
	}

	private void OnShrineUsed(Node user, int newSequence)
	{
		var node = RoomGraph.Instance?.GetRoom(RoomId);
		if (node != null)
		{
			node.IsShrineUsed = true;
		}
		SignalBus.Instance?.PublishShrineConsumed(RoomId);
	}

	private void OnHealUsed(Node _)
	{
		var node = RoomGraph.Instance?.GetRoom(RoomId);
		if (node != null)
		{
			node.IsHealingUsed = true;
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Initialization & Caching
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Cache references to child nodes by standard naming conventions.
	/// Expects children named: ConnectionPoints, SequenceDoors[], EnemySpawnPoints[], MaterialNodes, Tilemap.
	/// </summary>
	private void CacheChildNodes()
	{
		// Cache connection points (N/S/E/W markers)
		var connectionPointsNode = GetNodeOrNull("ConnectionPoints");
		if (connectionPointsNode != null)
		{
			foreach (Node child in connectionPointsNode.GetChildren())
			{
				if (child is Node2D marker && Direction.TryParse(child.Name, ignoreCase: true, out Direction dir))
				{
					_connectionPoints[dir] = marker;
				}
			}
		}

		// Cache sequence doors by finding all nodes with the "SequenceDoor" script
		var sequenceDoors = new List<Node>();
		CollectSequenceDoors(this, sequenceDoors);
		_sequenceDoors = sequenceDoors.ToArray();

		// Cache child containers
		_enemySpawnPoints = GetNodeOrNull("EnemySpawnPoints");
		_materialNodes = GetNodeOrNull("MaterialNodes");
		_tilemap = GetNodeOrNull<TileMapLayer>("Tilemap");

		// Cache shrine if present (recursive type search)
		_shrine = FindShrineRecursive(this);

		// Cache heal interactable if present
		_heal = FindHealInteractableRecursive(this);
	}

	private static SequenceShrine? FindShrineRecursive(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is SequenceShrine shrine)
				return shrine;
			var found = FindShrineRecursive(child);
			if (found != null)
				return found;
		}
		return null;
	}

	private static HealInteractable? FindHealInteractableRecursive(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is HealInteractable heal)
				return heal;
			var found = FindHealInteractableRecursive(child);
			if (found != null)
				return found;
		}
		return null;
	}

	/// <summary>
	/// Recursively collect all nodes with the SequenceDoor script attached.
	/// </summary>
	private void CollectSequenceDoors(Node parent, List<Node> result)
	{
		foreach (Node child in parent.GetChildren())
		{
			// Simple heuristic: if the node name contains "Door" or has specific properties
			// In future, could check for Script("SequenceDoor.cs")
			if (child.Name.ToString().Contains("Door", System.StringComparison.OrdinalIgnoreCase))
			{
				result.Add(child);
			}

			CollectSequenceDoors(child, result);
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Public API — Connection Points
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Get the world position of a connection point in a given direction.
	/// Used to position the player when they transition into this room from an adjacent room.
	/// </summary>
	/// <param name="direction">The cardinal direction (North/South/East/West).</param>
	/// <returns>The global position of the connection point, or Vector2.Zero if not found.</returns>
	public Vector2 GetConnectionPointPosition(Direction direction)
	{
		if (_connectionPoints.TryGetValue(direction, out Node2D marker))
		{
			return marker.GlobalPosition;
		}

		GD.PrintErr($"[RoomInstance] No connection point found for direction {direction}");
		return GlobalPosition; // Fallback to room center
	}

	/// <summary>
	/// Get all defined connection point directions for this room.
	/// </summary>
	public IEnumerable<Direction> GetConnectionPointDirections()
	{
		return _connectionPoints.Keys;
	}

	/// <summary>
	/// Called by RunManager after this room is loaded and its RoomId is assigned.
	/// Queries the RoomGraph to determine which directions have actual neighbours,
	/// disables any ConnectionPoint that leads nowhere, and spawns a SequenceDoor
	/// at every ConnectionPoint whose graph edge is gated (has a DoorInfo).
	/// </summary>
	/// <param name="roomGraph">The active RoomGraph for this run.</param>
	public void ConfigureConnectionPoints(RoomGraph roomGraph)
	{
		var blockedMarkersNode = GetNodeOrNull("BlockedMarkers");

		foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
		{
			RoomNode? adjacent = roomGraph.GetAdjacentRoomInDirection(RoomId, dir);
			bool hasNeighbour = adjacent != null && roomGraph.AreRoomsConnected(RoomId, adjacent.RoomId);

			// ── ConnectionPoint Area2D ──────────────────────────────────────
			if (_connectionPoints.TryGetValue(dir, out Node2D? cpNode) && cpNode is Area2D cpArea)
			{
				if (hasNeighbour)
				{
					cpArea.Visible = true;
					cpArea.Monitoring = true;
					SetCollisionShapeDisabled(cpArea, disabled: false);

					// Spawn a SequenceDoor if this edge is gated.
					// Use new SequenceDoor() directly to avoid PackedScene.Instantiate<T>() cast
					// failures caused by Godot's stale script-type registry.
					DoorInfo? doorInfo = roomGraph.GetDoorBetween(RoomId, adjacent!.RoomId);
					if (doorInfo != null)
					{
						var door = new SequenceDoor();
						door.Position = Vector2.Zero;
						cpArea.AddChild(door);
						door.ConfigureFromDoorInfo(doorInfo, dir);
						GD.Print($"[RoomInstance] Room {RoomId}: spawned SequenceDoor at {dir} (req Seq ≤ {doorInfo.RequiredSequence}, locked={doorInfo.IsLocked})");
					}
				}
				else
				{
					cpArea.Visible = false;
					cpArea.Monitoring = false;
					SetCollisionShapeDisabled(cpArea, disabled: true);
					GD.Print($"[RoomInstance] Room {RoomId}: no neighbour {dir} — door hidden.");
				}
			}

			// ── BlockedMarker (optional visual authored in scene) ───────────
			if (blockedMarkersNode != null)
			{
				var marker = blockedMarkersNode.GetNodeOrNull<Node2D>(dir.ToString());
				if (marker != null)
				{
					marker.Visible = !hasNeighbour;
				}
			}
		}
	}

	/// <summary>
	/// Enables or disables the first CollisionShape2D child of the given Area2D.
	/// Safe to call even if the Area2D has no collision shape yet.
	/// </summary>
	/// <param name="area">The Area2D whose collision shape to toggle.</param>
	/// <param name="disabled">True to disable (no physics); false to enable.</param>
	private static void SetCollisionShapeDisabled(Area2D area, bool disabled)
	{
		var shape = area.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape != null)
		{
			shape.Disabled = disabled;
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Enemy Tracking & Room Completion
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Count the number of alive enemies currently in this room.
	/// Called at initialization and after each enemy dies.
	/// Enemies are identified as children under EnemyContainer or direct children of World
	/// that are not the player.
	/// </summary>
	private void CountEnemies()
	{
		if (Archetype == RoomArchetype.Material || Archetype == RoomArchetype.Healing || 
		    Archetype == RoomArchetype.SequenceShrine || Archetype == RoomArchetype.BossAntechamber)
		{
			// These room types don't have enemies
			_aliveEnemyCount = 0;
			return;
		}

		// Count enemies in EnemyContainer if it exists at a higher scope
		_aliveEnemyCount = 0;
		
		// For now, we'll rely on the enemy death signal to decrement the count.
		// This method will be called once at _Ready() to establish the initial count.
		// In a full implementation, we'd query the actual enemy nodes from RunManager.
	}

	/// <summary>
	/// Called by the signal bus when an entity dies.
	/// Checks if it was an enemy in this room and decrements the counter.
	/// If all enemies are defeated, marks the room as cleared and emits the signal.
	/// </summary>
	private void OnEnemyDied(Node entity, Node? source)
	{
		// Simple heuristic: if the entity is not the player and it's in this room's tree
		if (entity == null || entity is CharacterBody2D)
		{
			// Check if it's not the player
			var runManager = RunManager.Instance;
			if (runManager?.Player == entity)
			{
				// This was the player dying, not an enemy
				return;
			}

			// Check if the entity is a descendant of this room
			if (entity.IsAncestorOf(this) || this.IsAncestorOf(entity))
			{
				_aliveEnemyCount = Mathf.Max(0, _aliveEnemyCount - 1);

				// If all enemies are dead, mark the room as cleared
				if (_aliveEnemyCount <= 0 && !IsCleared)
				{
					MarkRoomAsCleared();
				}
			}
		}
	}

	/// <summary>
	/// Mark this room as cleared and emit the RoomCleared signal via SignalBus.
	/// </summary>
	private void ApplyCollectedMaterialState()
	{
		if (_materialNodes == null) return;

		var roomNode = RoomGraph.Instance?.GetRoom(RoomId);

		foreach (Node child in _materialNodes.GetChildren())
		{
			var nodeName = child.Name.ToString();

			if (roomNode != null && roomNode.CollectedMaterialNodes.Contains(nodeName))
			{
				child.QueueFree();
				continue;
			}

			if (child is MaterialPickup pickup)
			{
				pickup.Collected += (_, _, _) => OnMaterialNodeCollected(nodeName);
			}
		}
	}

	private void OnMaterialNodeCollected(string nodeName)
	{
		var roomNode = RoomGraph.Instance?.GetRoom(RoomId);
		roomNode?.CollectedMaterialNodes.Add(nodeName);
	}

	private void MarkRoomAsCleared()
	{
		if (IsCleared)
		{
			return; // Already cleared
		}

		IsCleared = true;

		// Notify the world that this room is cleared
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.PublishRoomCleared(RoomId);
			GD.Print($"[RoomInstance] Room {RoomId} ({Archetype}) cleared!");
		}

		// TODO: Disable/hide enemy spawn points, trigger visual celebration, unlock doors, etc.
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Enemy Spawning
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Spawn enemies at each spawn point in this room based on archetype.
	/// Rooms that don't support enemies are skipped.
	/// </summary>
	public void SpawnEnemies()
	{
		// Skip non-combat rooms
		if (Archetype == RoomArchetype.Material || Archetype == RoomArchetype.Healing ||
		    Archetype == RoomArchetype.SequenceShrine || Archetype == RoomArchetype.BossAntechamber)
		{
			return;
		}

		if (_enemySpawnPoints == null || _enemySpawnPoints.GetChildCount() == 0)
		{
			GD.Print($"[RoomInstance] No enemy spawn points defined for room {RoomId}");
			return;
		}

		// One of each enemy archetype, in a fixed order.
		var enemyScenePaths = new[]
		{
			"res://Entities/Enemies/GoblinEnemy.tscn",
			"res://Entities/Enemies/SkeletonEnemy.tscn",
			"res://Entities/Enemies/MushroomEnemy.tscn",
			"res://Entities/Enemies/FlyingEyeEnemy.tscn",
		};

		// Determine how many enemies to spawn based on archetype
		int enemyCount = Archetype switch
		{
			RoomArchetype.Combat => enemyScenePaths.Length, // one of each
			RoomArchetype.BossRoom => 1, // Single boss
			_ => 0,
		};

		// Get EnemyContainer from RunManager's CurrentWorld
		var runManager = RunManager.Instance;
		if (runManager?.CurrentWorld == null)
		{
			GD.PrintErr("[RoomInstance] Cannot find EnemyContainer - RunManager or CurrentWorld is null");
			return;
		}

		var enemyContainer = runManager.CurrentWorld.FindChild("EnemyContainer", recursive: false, owned: false) as Node2D;
		if (enemyContainer == null)
		{
			GD.PrintErr("[RoomInstance] Cannot find EnemyContainer in CurrentWorld");
			return;
		}

		var playerNode = runManager.Player as Node2D;

		// Spawn enemies at each designated spawn point
		for (int i = 0; i < Mathf.Min(enemyCount, _enemySpawnPoints.GetChildCount()); i++)
		{
			var spawnPoint = _enemySpawnPoints.GetChild<Node2D>(i);
			if (spawnPoint == null)
			{
				continue;
			}

			var scenePath = enemyScenePaths[i % enemyScenePaths.Length];
			var enemyScene = GD.Load<PackedScene>(scenePath);
			if (enemyScene == null)
			{
				GD.PrintErr($"[RoomInstance] Failed to load enemy scene {scenePath}");
				continue;
			}

			// Instantiate enemy
			var enemy = enemyScene.Instantiate<CharacterBody2D>();
			if (enemy == null)
			{
				GD.PrintErr($"[RoomInstance] Failed to instantiate enemy from {scenePath}");
				continue;
			}

			// Keep spawn points close enough to engage the player in this demo flow.
			var spawnPosition = spawnPoint.GlobalPosition;
			if (playerNode != null && EnemySpawnAggroDistanceCap > 0f)
			{
				var toSpawn = spawnPosition - playerNode.GlobalPosition;
				if (toSpawn.Length() > EnemySpawnAggroDistanceCap)
				{
					spawnPosition = playerNode.GlobalPosition + toSpawn.Normalized() * EnemySpawnAggroDistanceCap;
				}
			}

			enemy.GlobalPosition = spawnPosition;

			// Add to world
			enemyContainer.AddChild(enemy);

			_aliveEnemyCount++;

			GD.Print($"[RoomInstance] Spawned enemy at {spawnPosition}");
		}

		GD.Print($"[RoomInstance] Spawned {_aliveEnemyCount} enemies for room {RoomId}");
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Utility
	// ═══════════════════════════════════════════════════════════════════════════════════

	public override string ToString()
	{
		return $"RoomInstance(ID={RoomId}, Branch={BranchId}, Archetype={Archetype}, Cleared={IsCleared}, Visited={IsVisited})";
	}
}
