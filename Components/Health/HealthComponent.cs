using Godot;
using Sequence.Autoloads;

namespace Sequence.Components.Health;

/// <summary>
/// Runtime health model for any entity.
/// </summary>
public partial class HealthComponent : Node
{
	[Signal] public delegate void DamagedEventHandler(float amount, float currentHp, float maxHp, Node? source);
	[Signal] public delegate void HealedEventHandler(float amount, float currentHp, float maxHp);
	[Signal] public delegate void DiedEventHandler(Node? source);

	[Export(PropertyHint.Range, "1,10000,1")] public float MaxHp { get; set; } = 100f;
	[Export] public bool StartAtMaxHp { get; set; } = true;
	[Export(PropertyHint.Range, "0,10000,0.1")] public float StartingHp { get; set; } = 100f;

	public float CurrentHp { get; private set; }
	public bool IsDead { get; private set; }

	public override void _Ready()
	{
		MaxHp = Mathf.Max(1f, MaxHp);
		CurrentHp = StartAtMaxHp ? MaxHp : Mathf.Clamp(StartingHp, 0f, MaxHp);
		IsDead = CurrentHp <= 0f;
		SignalBus.Instance?.PublishHealthChanged(GetOwnerEntity(), CurrentHp, MaxHp);
	}

	public bool TakeDamage(float amount, Node? source = null)
	{
		if (IsDead || amount <= 0f)
		{
			return false;
		}

		var sourceNode = source ?? GetOwnerEntity();

		CurrentHp = Mathf.Clamp(CurrentHp - amount, 0f, MaxHp);

		EmitSignal(SignalName.Damaged, amount, CurrentHp, MaxHp, sourceNode);
		SignalBus.Instance?.PublishHealthChanged(GetOwnerEntity(), CurrentHp, MaxHp);

		if (CurrentHp <= 0f && !IsDead)
		{
			IsDead = true;
			EmitSignal(SignalName.Died, sourceNode);
			SignalBus.Instance?.PublishEntityDied(GetOwnerEntity(), sourceNode);
		}

		return true;
	}

	public bool Heal(float amount)
	{
		if (IsDead || amount <= 0f)
		{
			return false;
		}

		var previous = CurrentHp;
		CurrentHp = Mathf.Clamp(CurrentHp + amount, 0f, MaxHp);

		if (!Mathf.IsEqualApprox(previous, CurrentHp))
		{
			EmitSignal(SignalName.Healed, amount, CurrentHp, MaxHp);
			SignalBus.Instance?.PublishHealthChanged(GetOwnerEntity(), CurrentHp, MaxHp);
			return true;
		}

		return false;
	}

	public void SetMaxHp(float newMaxHp, bool preserveRatio = true)
	{
		newMaxHp = Mathf.Max(1f, newMaxHp);
		var ratio = MaxHp > 0f ? CurrentHp / MaxHp : 1f;
		MaxHp = newMaxHp;
		CurrentHp = preserveRatio ? Mathf.Clamp(MaxHp * ratio, 0f, MaxHp) : Mathf.Clamp(CurrentHp, 0f, MaxHp);
		IsDead = CurrentHp <= 0f;
		SignalBus.Instance?.PublishHealthChanged(GetOwnerEntity(), CurrentHp, MaxHp);
	}

	private Node GetOwnerEntity()
	{
		return GetParent() ?? this;
	}
}

