using Godot;
using Sequence.Autoloads;

namespace Sequence.World;

public partial class WorldController : Node2D
{
	[Export] public int DebugSeed { get; set; } = 0;

	public override void _Ready()
	{
		GD.Print("[WorldController] _Ready fired");

		if (RunManager.Instance?.IsRunActive == true)
		{
			GD.Print("[WorldController] Run already active, skipping StartRun");
			return;
		}

		int seed = DebugSeed != 0 ? DebugSeed : (int)Time.GetUnixTimeFromSystem();
		GD.Print($"[WorldController] Calling StartRun with seed {seed}");
		RunManager.Instance?.StartRun(seed);
	}
}
