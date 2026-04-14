using Godot;
using Sequence.Autoloads;

namespace Sequence.Entities.Interactables;

/// <summary>
/// A door that separates branches or forms cross-connections between rooms.
/// Locks based on the player's Sequence level and unlocks when they advance
/// to the required Sequence tier.
/// </summary>
public partial class SequenceDoor : Area2D
{
	// ─────────────────────────────────────────────
	//  Inspector Properties
	// ─────────────────────────────────────────────

	/// <summary>The Sequence level required to unlock this door.</summary>
	[Export] public int RequiredSequence { get; set; } = 9;

	/// <summary>
	/// The two branch IDs this door bridges.
	/// For branch-entry doors: (fork point branch, new branch).
	/// For cross-connections: (branch A, branch B).
	/// </summary>
	[Export] public Vector2I ConnectedBranches { get; set; } = Vector2I.Zero;

	// ─────────────────────────────────────────────
	//  Runtime State
	// ─────────────────────────────────────────────

	/// <summary>Current lock state; updated when player advances Sequence.</summary>
	private bool _isLocked = true;

	/// <summary>Visual representation node (sprite or AnimatedSprite2D).</summary>
	private Node2D? _visual;

	/// <summary>Collision shape that disables when door unlocks.</summary>
	private CollisionShape2D? _collisionShape;

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Properties
	// ═══════════════════════════════════════════════════════════════════════════════════

	public bool IsLocked
	{
		get => _isLocked;
		private set
		{
			if (_isLocked == value)
			{
				return; // No change
			}

			_isLocked = value;
			UpdateVisualState();
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Godot Lifecycle
	// ═══════════════════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		// Cache child nodes
		_visual = GetNodeOrNull<Node2D>("Visual");
		_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

		// Connect to sequence advancement signal
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.SequenceAdvanced += OnSequenceAdvanced;
		}

		// Set initial locked state (assume locked at start)
		IsLocked = true;
		UpdateVisualState();
	}

	public override void _ExitTree()
	{
		// Disconnect from signals
		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.SequenceAdvanced -= OnSequenceAdvanced;
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Signal Handlers
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Called when the player advances Sequence.
	/// Unlocks this door if the player has reached the required Sequence level.
	/// </summary>
	/// <param name="newSequence">The player's new (lower) Sequence number.</param>
	private void OnSequenceAdvanced(int newSequence)
	{
		// Door unlocks when player's Sequence >= RequiredSequence
		// (Note: Sequence numbers decrease; lower numbers = higher power)
		if (newSequence <= RequiredSequence)
		{
			IsLocked = false;
			GD.Print($"[SequenceDoor] Door unlocked! Sequence {newSequence} reached required {RequiredSequence}");
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Visual State Management
	// ═══════════════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Update the visual appearance and collision based on lock state.
	/// </summary>
	private void UpdateVisualState()
	{
		// Update collision (disable collision when unlocked)
		if (_collisionShape != null)
		{
			_collisionShape.Disabled = !IsLocked;
		}

		// Update visual appearance
		if (_visual != null)
		{
			if (IsLocked)
			{
				// Locked state: visible, solid
				_visual.Show();
				_visual.Modulate = new Color(1, 0.2f, 0.2f, 0.8f); // Red tint
			}
			else
			{
				// Unlocked state: dimmed or faded out
				_visual.Show();
				_visual.Modulate = new Color(0.3f, 1, 0.3f, 0.3f); // Green tint, semi-transparent
			}
		}

		// Log state change
		string lockState = IsLocked ? "LOCKED" : "UNLOCKED";
		GD.Print($"[SequenceDoor] Door @ Sequence {RequiredSequence} is now {lockState} (Branches {ConnectedBranches.X} <-> {ConnectedBranches.Y})");
	}

	/// <summary>
	/// Manually unlock this door (for testing or special cases).
	/// </summary>
	public void UnlockManually()
	{
		IsLocked = false;
	}

	/// <summary>
	/// Manually lock this door (for testing or special cases).
	/// </summary>
	public void LockManually()
	{
		IsLocked = true;
	}

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Utility
	// ═══════════════════════════════════════════════════════════════════════════════════

	public override string ToString()
	{
		return $"SequenceDoor(Seq{RequiredSequence}, Branches={ConnectedBranches.X}<->{ConnectedBranches.Y}, Locked={IsLocked})";
	}
}
