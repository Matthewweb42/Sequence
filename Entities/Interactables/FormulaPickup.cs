using Godot;
using Sequence.Components.Inventory;

namespace Sequence.Entities.Interactables;

/// <summary>
/// World pickup that teaches the player a formula when walked over.
/// </summary>
public partial class FormulaPickup : Area2D
{
    [Export] public string FormulaId { get; set; } = string.Empty;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        var inventory = body.GetNodeOrNull<InventoryComponent>("InventoryComponent");
        if (inventory == null) return;

        if (inventory.DiscoverFormula(FormulaId))
        {
            GD.Print($"[FormulaPickup] Discovered formula: {FormulaId}");
        }

        QueueFree();
    }
}
