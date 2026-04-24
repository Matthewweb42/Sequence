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
		ResolveSceneReferences();
		StartRun(CurrentSeed == 0 ? (int)Time.GetUnixTimeFromSystem() : CurrentSeed);
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
		EmitSignal(SignalName.RunStarted, CurrentSeed);
		EmitSignal(SignalName.RunStateChanged, (int)CurrentRunState);
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
		RoomGraph = root.FindChild("RoomGraph", recursive: true, owned: false) as RoomGraph;
	}

	public void TransitionToRoom(int roomId, Direction? entryDirection = null)
	{
		if (RoomGraph == null || Player == null || CurrentWorld == null)
		{
			GD.PrintErr("[RunManager] TransitionToRoom: Missing RoomGraph, Player, or CurrentWorld!");
			return;
		}

		RoomNode? roomNode = RoomGraph.GetRoom(roomId);
		if (roomNode == null)
		{
			GD.PrintErr($"[RunManager] TransitionToRoom: Room {roomId} not found in RoomGraph!");
			return;
		}

		var roomContainer = CurrentWorld.FindChild("RoomContainer", recursive: false, owned: false) as Node;
		if (roomContainer != null)
		{
			foreach (Node child in roomContainer.GetChildren())
			{
				child.QueueFree();
			}
		}

		PackedScene? roomScene = LoadRoomTemplate(roomNode.Archetype);
		if (roomScene == null)
		{
			GD.PrintErr($"[RunManager] TransitionToRoom: Could not load template for archetype {roomNode.Archetype}");
			return;
		}

		RoomInstance? newRoom = roomScene.Instantiate<RoomInstance>();
		if (newRoom == null)
		{
			GD.PrintErr("[RunManager] TransitionToRoom: Failed to instantiate room scene");
			return;
		}

		newRoom.RoomId = roomId;
		newRoom.BranchId = roomNode.BranchId;
		newRoom.Archetype = roomNode.Archetype;

		if (roomContainer != null)
		{
			roomContainer.AddChild(newRoom);
		}

		if (Player is Node2D player2D)
		{
			if (entryDirection.HasValue)
			{
				player2D.GlobalPosition = newRoom.GetConnectionPointPosition(entryDirection.Value);
			}
			else
			{
				player2D.GlobalPosition = newRoom.GlobalPosition;
			}
		}

		newRoom.IsVisited = true;

		if (!newRoom.IsCleared && newRoom.Archetype != RoomArchetype.Material && newRoom.Archetype != RoomArchetype.Lore &&
		    newRoom.Archetype != RoomArchetype.SequenceShrine && newRoom.Archetype != RoomArchetype.BossAntechamber)
		{
			newRoom.SpawnEnemies();
		}

		SetCurrentRoom(roomId);
		SignalBus.Instance?.PublishRoomEntered(roomId);

		GD.Print($"[RunManager] Transitioned to Room {roomId} ({roomNode.Archetype}) on Branch {roomNode.BranchId}");
	}

	private PackedScene? LoadRoomTemplate(RoomArchetype archetype)
	{
		string? templatePath = archetype switch
		{
			RoomArchetype.Combat => "res://World/Rooms/CombatRoom.tscn",
			RoomArchetype.SequenceShrine => "res://World/Rooms/SequenceShrineRoom.tscn",
			RoomArchetype.Material => "res://World/Rooms/MaterialRoom.tscn",
			RoomArchetype.Lore => "res://World/Rooms/MaterialRoom.tscn",
			RoomArchetype.BossAntechamber => "res://World/Rooms/CombatRoom.tscn",
			RoomArchetype.BossRoom => "res://World/Rooms/BossRoom.tscn",
			RoomArchetype.Hidden => "res://World/Rooms/MaterialRoom.tscn",
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
