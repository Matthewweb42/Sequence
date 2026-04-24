using Godot;

namespace Sequence.Autoloads;

/// <summary>
/// Lightweight persistent game-level state holder.
/// </summary>
public partial class GameManager : Node
{
	public static GameManager? Instance { get; private set; }

	[Signal] public delegate void GamePausedEventHandler(bool isPaused);

	public bool IsPaused { get; private set; }

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

	public void SetPaused(bool pause)
	{
		if (IsPaused == pause)
		{
			return;
		}

		IsPaused = pause;
		GetTree().Paused = IsPaused;
		EmitSignal(SignalName.GamePaused, IsPaused);
	}

	public void TogglePause()
	{
		SetPaused(!IsPaused);
	}
}

