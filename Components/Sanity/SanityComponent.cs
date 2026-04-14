using Godot;

namespace Sequence.Components.Sanity;

/// <summary>
/// Runtime resource pool for ability costs and regeneration.
/// </summary>
public partial class SanityComponent : Node
{
	[Signal] public delegate void SanityChangedEventHandler(float current, float max);
	[Signal] public delegate void SanityDepletedEventHandler();

	[Export(PropertyHint.Range, "1,10000,1")] public float MaxSanity { get; set; } = 100f;
	[Export] public bool StartAtMaxSanity { get; set; } = true;
	[Export(PropertyHint.Range, "0,10000,0.1")] public float StartingSanity { get; set; } = 100f;
	[Export(PropertyHint.Range, "0,1000,0.1")] public float RegenPerSecond { get; set; } = 2f;

	public float CurrentSanity { get; private set; }

	public override void _Ready()
	{
		MaxSanity = Mathf.Max(1f, MaxSanity);
		CurrentSanity = StartAtMaxSanity ? MaxSanity : Mathf.Clamp(StartingSanity, 0f, MaxSanity);
		EmitSignal(SignalName.SanityChanged, CurrentSanity, MaxSanity);
	}

	public override void _Process(double delta)
	{
		if (RegenPerSecond <= 0f || CurrentSanity >= MaxSanity)
		{
			return;
		}

		CurrentSanity = Mathf.Clamp(CurrentSanity + RegenPerSecond * (float)delta, 0f, MaxSanity);
		EmitSignal(SignalName.SanityChanged, CurrentSanity, MaxSanity);
	}

	public bool TrySpend(float amount)
	{
		if (amount <= 0f)
		{
			return true;
		}

		if (CurrentSanity < amount)
		{
			EmitSignal(SignalName.SanityDepleted);
			return false;
		}

		CurrentSanity -= amount;
		EmitSignal(SignalName.SanityChanged, CurrentSanity, MaxSanity);
		if (CurrentSanity <= 0f)
		{
			EmitSignal(SignalName.SanityDepleted);
		}

		return true;
	}

	public void Restore(float amount)
	{
		if (amount <= 0f) return;
		CurrentSanity = Mathf.Clamp(CurrentSanity + amount, 0f, MaxSanity);
		EmitSignal(SignalName.SanityChanged, CurrentSanity, MaxSanity);
	}
}

