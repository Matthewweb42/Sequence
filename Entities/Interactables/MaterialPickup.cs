using Godot;
using Sequence.Components.Inventory;

namespace Sequence.Entities.Interactables;

/// <summary>
/// Collectible world pickup that grants materials to the player inventory.
/// </summary>
public partial class MaterialPickup : Area2D
{
    [Signal] public delegate void CollectedEventHandler(Node collector, string materialId, int amount);

    [Export] public string MaterialId { get; set; } = "basic_essence";
    [Export(PropertyHint.Range, "1,999,1")] public int Amount { get; set; } = 1;
    [Export] public NodePath? InventoryPath { get; set; }

    public override void _Ready()
    {
        AreaEntered += OnAreaEntered;
        BodyEntered += OnBodyEntered;
    }

    private void OnAreaEntered(Area2D area)
    {
        var collector = area.GetParent() ?? area;
        TryCollectFromNode(collector);
    }

    private void OnBodyEntered(Node2D body)
    {
        TryCollectFromNode(body);
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
