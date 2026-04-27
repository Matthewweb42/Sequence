using Godot;
using Sequence.World;

namespace Sequence.Autoloads;

public enum RunState
{
	Inactive,
	Active,
	Victory,
	Defeat
}

/// <summary>
/// Runtime coordinator for the active run and scene-level references.
/// </summary>
public partial class RunManager : Node
{
	public static RunManager? Instance { get; private set; }

	[Signal] public delegate void RunStartedEventHandler(int seed);
	[Signal] public delegate void RunEndedEventHandler(bool isVictory);
	[Signal] public delegate void RunStateChangedEventHandler(int runState);
	[Signal] public delegate void CurrentRoomChangedEventHandler(int roomId);

	[Export] public int CurrentSeed { get; set; } = 0;

	public Node? CurrentWorld { get; private set; }
	public Node? Player { get; private set; }
	public RoomGraph? RoomGraph { get; private set; }
	public int CurrentRoomId { get; private set; } = -1;
	public bool IsRunActive { get; private set; }
	public RunState CurrentRunState { get; private set; } = RunState.Inactive;

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

	public void StartRun(int seed)
	{
		CurrentSeed = seed;
		IsRunActive = true;
		CurrentRunState = RunState.Active;
		ResolveSceneReferences();

		GD.Print($"[RunManager] StartRun: Player={Player?.Name ?? "NULL"}, RoomGraph={RoomGraph?.Name ?? "NULL"}, CurrentWorld={CurrentWorld?.Name ?? "NULL"}");

		RoomGraph?.GenerateWorld(seed, sequenceStart: 9, sequenceFinal: 5);
		GD.Print($"[RunManager] GenerateWorld complete. StartRoomId={RoomGraph?.StartRoomId}");

		EmitSignal(SignalName.RunStarted, CurrentSeed);
		EmitSignal(SignalName.RunStateChanged, (int)CurrentRunState);

		if (RoomGraph != null)
			TransitionToRoom(RoomGraph.StartRoomId);
		else
			GD.PrintErr("[RunManager] StartRun: RoomGraph is null, cannot transition to starting room");
	}

	public void EndRun(bool isVictory)
	{
		if (!IsRunActive)
		{
			return;
		}

		IsRunActive = false;
		CurrentRunState = isVictory ? RunState.Victory : RunState.Defeat;
		EmitSignal(SignalName.RunEnded, isVictory);
		EmitSignal(SignalName.RunStateChanged, (int)CurrentRunState);
	}

	public bool SetCurrentRoom(int roomId)
	{
		if (CurrentRoomId == roomId)
		{
			return false;
		}

		CurrentRoomId = roomId;
		EmitSignal(SignalName.CurrentRoomChanged, CurrentRoomId);
		return true;
	}

	public void RefreshReferences()
	{
		ResolveSceneReferences();
	}

	private void ResolveSceneReferences()
	{
		var root = GetTree()?.CurrentScene;
		if (root == null)
		{
			CurrentWorld = null;
			Player = null;
			RoomGraph = null;
			return;
		}

		CurrentWorld = root;
		Player = root.FindChild("Player", recursive: true, owned: false);
		RoomGraph = RoomGraph.Instance;
	}

	/// <summary>
	/// Transition the player to a new room.
	/// Unloads the current room, instantiates the new room from the template pool,
	/// positions the player at the appropriate entry point, and emits signals.
	/// </summary>
	/// <param name="roomId">The target room ID (from RoomGraph).</param>
	/// <param name="entryDirection">The cardinal direction the player is entering from (determines exit point from prev room = entry point to new room).</param>
	public void TransitionToRoom(int roomId, Direction? entryDirection = null)
	{
		if (RoomGraph == null || Player == null || CurrentWorld == null)
		{
			GD.PrintErr("[RunManager] TransitionToRoom: Missing RoomGraph, Player, or CurrentWorld!");
			return;
		}

		// Get room metadata from RoomGraph
		RoomNode? roomNode = RoomGraph.GetRoom(roomId);
		if (roomNode == null)
		{
			GD.PrintErr($"[RunManager] TransitionToRoom: Room {roomId} not found in RoomGraph!");
			return;
		}

		// Unload current room from RoomContainer
		var roomContainer = CurrentWorld.FindChild("RoomContainer", recursive: false, owned: false) as Node;
		if (roomContainer != null)
		{
			foreach (Node child in roomContainer.GetChildren())
			{
				child.QueueFree();
			}
		}

		// EnemyContainer is a sibling of RoomContainer at world scope, so its
		// children survive room teardown. Clear them here so leftover enemies
		// from a combat room don't follow the player into non-combat rooms.
		var enemyContainer = CurrentWorld.FindChild("EnemyContainer", recursive: false, owned: false) as Node;
		if (enemyContainer != null)
		{
			foreach (Node child in enemyContainer.GetChildren())
			{
				child.QueueFree();
			}
		}

		// Load the room template based on archetype
		PackedScene? roomScene = LoadRoomTemplate(roomNode.Archetype);
		if (roomScene == null)
		{
			GD.PrintErr($"[RunManager] TransitionToRoom: Could not load template for archetype {roomNode.Archetype}");
			return;
		}

		// Instantiate the room (use safe cast — throws if script not attached to scene root)
		Node rawRoom = roomScene.Instantiate();
		if (rawRoom is not RoomInstance newRoom)
		{
			GD.PrintErr($"[RunManager] TransitionToRoom: Room scene for {roomNode.Archetype} root is '{rawRoom.GetClass()}', not RoomInstance — missing script attachment.");
			rawRoom.QueueFree();
			return;
		}

		// Configure the room instance
		newRoom.RoomId = roomId;
		newRoom.BranchId = roomNode.BranchId;
		newRoom.Archetype = roomNode.Archetype;

		// Add room to the world
		if (roomContainer != null)
		{
			roomContainer.AddChild(newRoom);
		}

		// Disable connection points that lead nowhere, so the player can't walk
		// into a dead wall. Must happen after RoomId is assigned and the room is
		// in the tree so its child nodes are ready.
		newRoom.ConfigureConnectionPoints(RoomGraph);

		// Position the player at the entry point
		if (Player is Node2D player2D)
		{
			if (entryDirection.HasValue)
			{
				player2D.GlobalPosition = newRoom.GetConnectionPointPosition(entryDirection.Value);
			}
			else
			{
				// Default: position at room center
				player2D.GlobalPosition = newRoom.GlobalPosition;
			}
		}

		// Mark room as visited
		newRoom.IsVisited = true;

		// Spawn enemies if room is not yet cleared
		if (!newRoom.IsCleared && newRoom.Archetype != RoomArchetype.Material && newRoom.Archetype != RoomArchetype.Healing &&
		    newRoom.Archetype != RoomArchetype.SequenceShrine && newRoom.Archetype != RoomArchetype.BossAntechamber)
		{
			newRoom.SpawnEnemies();
		}

		// Update current room
		SetCurrentRoom(roomId);

		// Emit signals
		SignalBus.Instance?.PublishRoomEntered(roomId);

		GD.Print($"[RunManager] Transitioned to Room {roomId} ({roomNode.Archetype}) on Branch {roomNode.BranchId}");
	}

	/// <summary>
	/// Load the appropriate room template scene based on archetype.
	/// </summary>
	private PackedScene? LoadRoomTemplate(RoomArchetype archetype)
	{
		string templatePath = archetype switch
		{
			RoomArchetype.Combat => "res://World/Rooms/CombatRoom.tscn",
			RoomArchetype.SequenceShrine => "res://World/Rooms/SequenceShrineRoom.tscn",
			RoomArchetype.Material => "res://World/Rooms/MaterialRoom.tscn",
			RoomArchetype.Healing => "res://World/Rooms/HealingRoom.tscn",
			RoomArchetype.BossAntechamber => "res://World/Rooms/BossAntechamberRoom.tscn",
			RoomArchetype.BossRoom => "res://World/Rooms/BossRoom.tscn",
			RoomArchetype.Hidden => "res://World/Rooms/MaterialRoom.tscn", // Placeholder
			_ => null,
		};

		if (templatePath == null)
		{
			GD.PrintErr($"[RunManager] No template defined for archetype {archetype}");
			return null;
		}

		var scene = GD.Load<PackedScene>(templatePath);
		if (scene == null)
		{
			GD.PrintErr($"[RunManager] Failed to load room template: {templatePath}");
		}

		return scene;
	}
}
