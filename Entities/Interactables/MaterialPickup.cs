using Godot;
using Sequence.Components.Inventory;

namespace Sequence.Entities.Interactables;

/// <summary>
/// Collectible world pickup that grants materials to the player inventory
/// when the player presses the interact key while standing in range.
/// </summary>
public partial class MaterialPickup : Area2D
{
    [Signal] public delegate void CollectedEventHandler(Node collector, string materialId, int amount);

    [Export] public string MaterialId { get; set; } = "basic_essence";
    [Export(PropertyHint.Range, "1,999,1")] public int Amount { get; set; } = 1;
    [Export] public NodePath? InventoryPath { get; set; }

    private Node2D? _currentUser;
    private bool _interactWasPressed;
    private bool _isCollected;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;
    }

    public override void _Process(double delta)
    {
        if (_isCollected)
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

        TryCollectFromNode(_currentUser);
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

    private bool TryCollectFromNode(Node? collector)
    {
        if (collector == null || string.IsNullOrWhiteSpace(MaterialId) || Amount <= 0)
        {
            return false;
        }

        var inventory = ResolveInventory(collector);
        if (inventory == null)
        {
            return false;
        }

        inventory.AddMaterial(MaterialId, Amount);
        EmitSignal(SignalName.Collected, collector, MaterialId, Amount);
        _isCollected = true;
        QueueFree();
        return true;
    }

    private InventoryComponent? ResolveInventory(Node collector)
    {
        if (InventoryPath != null && !InventoryPath.IsEmpty)
        {
            return GetNodeOrNull<InventoryComponent>(InventoryPath);
        }

        return collector.GetNodeOrNull<InventoryComponent>("InventoryComponent");
    }
}
