using Godot;
using Sequence.Autoloads;
using Sequence.World;

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

	/// <summary>Visual representation node.</summary>
	private Node2D? _visual;

	/// <summary>Collision shape for the Area2D detection region.</summary>
	private CollisionShape2D? _collisionShape;

	/// <summary>StaticBody2D child that physically blocks the player when locked.</summary>
	private StaticBody2D? _blocker;

	/// <summary>Collision shape on the Blocker StaticBody2D.</summary>
	private CollisionShape2D? _blockerShape;

	/// <summary>Label showing the required sequence number.</summary>
	private Label? _label;

	// ═══════════════════════════════════════════════════════════════════════════════════
	//  Properties
	// ═══════════════════════════════════════════════════════════════════════════════════

	public bool IsLocked
	{
		get => _isLocked;
		set
		{
			if (_isLocked == value)
			{
				return;
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
		// Try to find children authored in the .tscn (editor use)
		_visual = GetNodeOrNull<Node2D>("Visual");
		_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		_blocker = GetNodeOrNull<StaticBody2D>("Blocker");
		_blockerShape = _blocker?.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		_label = GetNodeOrNull<Label>("Visual/Label");

		// When created via new SequenceDoor() at runtime the .tscn children don't exist — build them
		if (_visual == null)
		{
			BuildChildrenProgrammatically();
		}

		if (SignalBus.Instance != null)
		{
			SignalBus.Instance.SequenceAdvanced += OnSequenceAdvanced;
		}

		IsLocked = true;
		UpdateVisualState();
	}

	/// <summary>
	/// Creates the door's visual and physics children in code.
	/// Called when the door is instantiated via <c>new SequenceDoor()</c> rather than from a .tscn.
	/// </summary>
	private void BuildChildrenProgrammatically()
	{
		// ── Visual ─────────────────────────────────────────────────────────
		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);

		// Door body
		var body = new Polygon2D
		{
			Color = new Color(0.35f, 0.2f, 0.1f, 0.9f),
			Polygon = new Vector2[] { new(-30, -70), new(-30, 70), new(30, 70), new(30, -70) }
		};
		_visual.AddChild(body);

		// Horizontal cross-planks
		var plankTop = new Polygon2D
		{
			Color = new Color(0.5f, 0.3f, 0.15f, 0.9f),
			Polygon = new Vector2[] { new(-30, -25), new(-30, -17), new(30, -17), new(30, -25) }
		};
		_visual.AddChild(plankTop);

		var plankBottom = new Polygon2D
		{
			Color = new Color(0.5f, 0.3f, 0.15f, 0.9f),
			Polygon = new Vector2[] { new(-30, 17), new(-30, 25), new(30, 25), new(30, 17) }
		};
		_visual.AddChild(plankBottom);

		// Sequence label
		_label = new Label
		{
			Name = "Label",
			OffsetLeft = -30,
			OffsetTop = -10,
			OffsetRight = 30,
			OffsetBottom = 10,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		_visual.AddChild(_label);

		// ── Area2D collision (detection) ────────────────────────────────────
		_collisionShape = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(40, 120) }
		};
		AddChild(_collisionShape);

		// ── StaticBody2D (physical blocker) ────────────────────────────────
		_blocker = new StaticBody2D
		{
			Name = "Blocker",
			CollisionLayer = 1,
			CollisionMask = 0
		};
		_blockerShape = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(60, 140) }
		};
		_blocker.AddChild(_blockerShape);
		AddChild(_blocker);
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
		// Area2D detection region
		if (_collisionShape != null)
			_collisionShape.Disabled = !IsLocked;

		// Physical blocker — enabled when locked so the player cannot walk through
		if (_blockerShape != null)
			_blockerShape.Disabled = !IsLocked;

		// Update label text with required sequence
		if (_label != null)
			_label.Text = $"Seq {RequiredSequence}";

		// Show door visual when locked; hide it when unlocked so the corridor is clear
		if (_visual != null)
		{
			_visual.Visible = IsLocked;
			_visual.Modulate = new Color(1f, 0.4f, 0.4f, 1f);
		}

		GD.Print($"[SequenceDoor] Door @ Sequence {RequiredSequence} is now {(IsLocked ? "LOCKED" : "UNLOCKED")} (Branches {ConnectedBranches.X} <-> {ConnectedBranches.Y})");
	}

	/// <summary>
	/// Initialize this door from graph edge data at runtime.
	/// Called by RoomInstance.ConfigureConnectionPoints when a gated connection is found.
	/// </summary>
	public void ConfigureFromDoorInfo(DoorInfo doorInfo, Direction facing)
	{
		RequiredSequence = doorInfo.RequiredSequence;
		ConnectedBranches = new Vector2I(doorInfo.ConnectedBranches.BranchA, doorInfo.ConnectedBranches.BranchB);
		_isLocked = doorInfo.IsLocked;
		if (facing == Direction.North || facing == Direction.South)
		{
			Rotation = Mathf.Pi / 2f;
		}
		UpdateVisualState();
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
