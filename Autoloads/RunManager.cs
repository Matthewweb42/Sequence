using Godot;

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
			return;
		}

		CurrentWorld = root;
		Player = root.FindChild("Player", recursive: true, owned: false);
	}
}

