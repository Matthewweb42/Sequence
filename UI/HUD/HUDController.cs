using Godot;
using Sequence.Autoloads;
using Sequence.Components.Health;
using Sequence.Components.Inventory;
using Sequence.Components.Sanity;
using Sequence.Components.Sequence;

namespace Sequence.UI.HUD;

/// <summary>
/// Lightweight debug HUD showing key runtime stats for gameplay loop verification.
/// </summary>
public partial class HUDController : CanvasLayer
{
	[Export] public NodePath? PlayerPath { get; set; }
	[Export] public NodePath HpLabelPath { get; set; } = new("DebugPanel/VBox/HpLabel");
	[Export] public NodePath SanityLabelPath { get; set; } = new("DebugPanel/VBox/SanityLabel");
	[Export] public NodePath SequenceLabelPath { get; set; } = new("DebugPanel/VBox/SequenceLabel");
	[Export] public NodePath EssenceLabelPath { get; set; } = new("DebugPanel/VBox/EssenceLabel");
	[Export] public NodePath RunStatusLabelPath { get; set; } = new("DebugPanel/VBox/RunStatusLabel");

	private Label? _hpLabel;
	private Label? _sanityLabel;
	private Label? _sequenceLabel;
	private Label? _essenceLabel;
	private Label? _runStatusLabel;

	private HealthComponent? _health;
	private SanityComponent? _sanity;
	private SequenceComponent? _sequence;
	private InventoryComponent? _inventory;
	private bool _isSubscribed;
	private bool _isRunManagerSubscribed;
	private string _runStateText = "Run: Active";

	public override void _Ready()
	{
		_hpLabel = GetNodeOrNull<Label>(HpLabelPath);
		_sanityLabel = GetNodeOrNull<Label>(SanityLabelPath);
		_sequenceLabel = GetNodeOrNull<Label>(SequenceLabelPath);
		_essenceLabel = GetNodeOrNull<Label>(EssenceLabelPath);
		_runStatusLabel = GetNodeOrNull<Label>(RunStatusLabelPath);

		SubscribeRunManager();

		TryResolveComponents();
		UpdateDisplay();
	}

	public override void _ExitTree()
	{
		Unsubscribe();
		UnsubscribeRunManager();
	}

	public override void _Process(double delta)
	{
		if (!_isRunManagerSubscribed)
		{
			SubscribeRunManager();
		}

		if (_health == null || _sanity == null || _sequence == null || _inventory == null)
		{
			TryResolveComponents();
		}

		UpdateDisplay();
	}

	private void TryResolveComponents()
	{
		if (_health != null && _sanity != null && _sequence != null && _inventory != null)
		{
			return;
		}

		Node? player = null;
		if (PlayerPath != null && !PlayerPath.IsEmpty)
		{
			player = GetNodeOrNull<Node>(PlayerPath);
		}

		player ??= RunManager.Instance?.Player;
		player ??= GetTree()?.CurrentScene?.FindChild("Player", recursive: true, owned: false);

		if (player == null)
		{
			return;
		}

		_health = player.GetNodeOrNull<HealthComponent>("HealthComponent");
		_sanity = player.GetNodeOrNull<SanityComponent>("SanityComponent");
		_sequence = player.GetNodeOrNull<SequenceComponent>("SequenceComponent");
		_inventory = player.GetNodeOrNull<InventoryComponent>("InventoryComponent");

		Subscribe();
	}

	private void Subscribe()
	{
		if (_isSubscribed)
		{
			return;
		}

		if (_health == null || _sanity == null || _sequence == null || _inventory == null)
		{
			return;
		}

		_health.Damaged += OnHealthDamaged;
		_health.Healed += OnHealthHealed;
		_health.Died += OnHealthDied;
		_sanity.SanityChanged += OnSanityChanged;
		_sanity.SanityDepleted += OnSanityDepleted;

		_sequence.SequenceAdvanced += OnSequenceAdvanced;
		_inventory.MaterialChanged += OnMaterialChanged;

		_isSubscribed = true;
	}

	private void Unsubscribe()
	{
		if (!_isSubscribed)
		{
			return;
		}

		if (_health != null)
		{
			_health.Damaged -= OnHealthDamaged;
			_health.Healed -= OnHealthHealed;
			_health.Died -= OnHealthDied;
		}

		if (_sanity != null)
		{
			_sanity.SanityChanged -= OnSanityChanged;
			_sanity.SanityDepleted -= OnSanityDepleted;
		}

		if (_sequence != null)
		{
			_sequence.SequenceAdvanced -= OnSequenceAdvanced;
		}

		if (_inventory != null)
		{
			_inventory.MaterialChanged -= OnMaterialChanged;
		}

		_isSubscribed = false;
	}

	private void UpdateDisplay()
	{
		if (_hpLabel != null)
		{
			if (_health == null)
			{
				_hpLabel.Text = "HP: -";
			}
			else
			{
				_hpLabel.Text = $"HP: {_health.CurrentHp:0}/{_health.MaxHp:0}";
			}
		}

		if (_sequenceLabel != null)
		{
			_sequenceLabel.Text = _sequence == null ? "Sequence: -" : $"Sequence: {_sequence.CurrentSequence}";
		}

		if (_sanityLabel != null)
		{
			_sanityLabel.Text = _sanity == null ? "Sanity: -" : $"Sanity: {_sanity.CurrentSanity:0}/{_sanity.MaxSanity:0}";
		}

		if (_essenceLabel != null)
		{
			var count = _inventory?.GetMaterialCount("basic_essence") ?? 0;
			_essenceLabel.Text = $"basic_essence: {count}";
		}

		if (_runStatusLabel != null)
		{
			_runStatusLabel.Text = _runStateText;
		}
	}

	private void SubscribeRunManager()
	{
		if (_isRunManagerSubscribed || RunManager.Instance == null)
		{
			return;
		}

		RunManager.Instance.RunStarted += OnRunStarted;
		RunManager.Instance.RunEnded += OnRunEnded;
		RunManager.Instance.RunStateChanged += OnRunStateChanged;

		_runStateText = RunManager.Instance.CurrentRunState switch
		{
			RunState.Victory => "Run: Victory",
			RunState.Defeat => "Run: Defeat",
			RunState.Inactive => "Run: Inactive",
			_ => "Run: Active"
		};

		_isRunManagerSubscribed = true;
	}

	private void UnsubscribeRunManager()
	{
		if (!_isRunManagerSubscribed || RunManager.Instance == null)
		{
			return;
		}

		RunManager.Instance.RunStarted -= OnRunStarted;
		RunManager.Instance.RunEnded -= OnRunEnded;
		RunManager.Instance.RunStateChanged -= OnRunStateChanged;
		_isRunManagerSubscribed = false;
	}

	private void OnRunStarted(int seed)
	{
		_runStateText = "Run: Active";
		UpdateDisplay();
	}

	private void OnRunEnded(bool isVictory)
	{
		_runStateText = isVictory ? "Run: Victory" : "Run: Defeat";
		UpdateDisplay();
	}

	private void OnRunStateChanged(int runState)
	{
		_runStateText = ((RunState)runState) switch
		{
			RunState.Victory => "Run: Victory",
			RunState.Defeat => "Run: Defeat",
			RunState.Inactive => "Run: Inactive",
			_ => "Run: Active"
		};

		UpdateDisplay();
	}

	private void OnHealthDamaged(float amount, float currentHp, float maxHp, Node? source)
	{
		UpdateDisplay();
	}

	private void OnHealthHealed(float amount, float currentHp, float maxHp)
	{
		UpdateDisplay();
	}

	private void OnHealthDied(Node? source)
	{
		UpdateDisplay();
	}

	private void OnSanityChanged(float current, float max)
	{
		UpdateDisplay();
	}

	private void OnSanityDepleted()
	{
		UpdateDisplay();
	}

	private void OnSequenceAdvanced(int newSequence)
	{
		UpdateDisplay();
	}

	private void OnMaterialChanged(string materialId, int newAmount)
	{
		if (materialId == "basic_essence")
		{
			UpdateDisplay();
		}
	}
}

