using Godot;
using Sequence.Autoloads;
using Sequence.Components.Inventory;
using Sequence.Components.Sequence;

namespace Sequence.Entities.Interactables;

/// <summary>
/// Simple interactable shrine that advances the nearby player's sequence.
/// </summary>
public partial class SequenceShrine : Area2D
{
    [Signal] public delegate void ShrineUsedEventHandler(Node user, int newSequence);

    [Export] public bool SingleUse { get; set; } = true;
    [Export] public NodePath? SequencePath { get; set; }
    [Export] public string MaterialId { get; set; } = "basic_essence";
    [Export(PropertyHint.Range, "0,99,1")] public int RequiredMaterialAmount { get; set; } = 1;
    [Export] public NodePath? InventoryPath { get; set; }

    private Node2D? _currentUser;
    private bool _interactWasPressed;
    private bool _isConsumed;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;
    }

    public override void _Process(double delta)
    {
        if (_isConsumed)
        {
            return;
        }

        if (_currentUser == null || !GodotObject.IsInstanceValid(_currentUser))
        {
            _currentUser = null;
            return;
        }

        var interactPressed = Input.IsActionPressed("interact") || Input.IsKeyPressed(Key.E);
        var interactJustPressed = interactPressed && !_interactWasPressed;
        _interactWasPressed = interactPressed;

        if (!interactJustPressed)
        {
            return;
        }

        if (RequiredMaterialAmount > 0 && !string.IsNullOrWhiteSpace(MaterialId))
        {
            var inventory = ResolveInventory(_currentUser);
            if (inventory == null || !inventory.TryConsumeMaterial(MaterialId, RequiredMaterialAmount))
            {
                return;
            }
        }

        var sequence = ResolveSequence(_currentUser);
        if (sequence == null)
        {
            return;
        }

        if (!sequence.TryAdvance())
        {
            return;
        }

        EmitSignal(SignalName.ShrineUsed, _currentUser, sequence.CurrentSequence);

        if (sequence.CurrentSequence <= sequence.FinalSequence)
        {
            RunManager.Instance?.EndRun(isVictory: true);
        }

        if (SingleUse)
        {
            _isConsumed = true;
            Monitoring = false;
            Monitorable = false;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_currentUser == null)
        {
            _currentUser = body;
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (_currentUser == body)
        {
            _currentUser = null;
        }
    }

    private void OnAreaEntered(Area2D area)
    {
        if (_currentUser != null)
        {
            return;
        }

        if (area.GetParent() is Node2D owner)
        {
            _currentUser = owner;
        }
    }

    private void OnAreaExited(Area2D area)
    {
        if (area.GetParent() is Node2D owner && _currentUser == owner)
        {
            _currentUser = null;
        }
    }

    private InventoryComponent? ResolveInventory(Node user)
    {
        if (InventoryPath != null && !InventoryPath.IsEmpty)
        {
            return GetNodeOrNull<InventoryComponent>(InventoryPath);
        }

        return user.GetNodeOrNull<InventoryComponent>("InventoryComponent");
    }

    private SequenceComponent? ResolveSequence(Node user)
    {
        if (SequencePath != null && !SequencePath.IsEmpty)
        {
            return GetNodeOrNull<SequenceComponent>(SequencePath);
        }

        return user.GetNodeOrNull<SequenceComponent>("SequenceComponent");
    }
}
