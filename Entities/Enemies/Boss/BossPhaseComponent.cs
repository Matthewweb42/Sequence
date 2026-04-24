using Godot;
using Sequence.Components.Health;

namespace Sequence.Entities.Enemies.Boss;

/// <summary>
/// Tracks boss HP thresholds and emits one-shot phase transitions.
/// </summary>
public partial class BossPhaseComponent : Node
{
	[Signal] public delegate void BossPhaseChangedEventHandler(int newPhase);

	[Export] public NodePath? HealthPath { get; set; }
	[Export(PropertyHint.Range, "0,1,0.01")] public float PhaseTwoThreshold { get; set; } = 0.66f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float PhaseThreeThreshold { get; set; } = 0.33f;

	private HealthComponent? _health;

	public int CurrentPhase { get; private set; } = 1;

	public override void _Ready()
	{
		_health = ResolveHealth();
		if (_health != null)
		{
			_health.Damaged += OnDamaged;
		}
	}

	public override void _ExitTree()
	{
		if (_health != null)
		{
			_health.Damaged -= OnDamaged;
		}
	}

	private void OnDamaged(float amount, float currentHp, float maxHp, Node? source)
	{
		if (maxHp <= 0f)
		{
			return;
		}

		var ratio = currentHp / maxHp;
		if (CurrentPhase < 2 && ratio <= PhaseTwoThreshold)
		{
			SetPhase(2);
		}

		if (CurrentPhase < 3 && ratio <= PhaseThreeThreshold)
		{
			SetPhase(3);
		}
	}

	private void SetPhase(int phase)
	{
		if (phase <= CurrentPhase)
		{
			return;
		}

		CurrentPhase = phase;
		EmitSignal(SignalName.BossPhaseChanged, CurrentPhase);
	}

	private HealthComponent? ResolveHealth()
	{
		if (HealthPath != null && !HealthPath.IsEmpty)
		{
			return GetNodeOrNull<HealthComponent>(HealthPath);
		}

		return GetParent()?.GetNodeOrNull<HealthComponent>("HealthComponent");
	}
}

