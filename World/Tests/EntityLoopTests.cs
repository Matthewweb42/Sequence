using Godot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sequence.Components.Drop;
using Sequence.Components.Health;
using Sequence.Components.Inventory;
using Sequence.Entities.Interactables;

namespace Sequence.Tests;

[TestClass]
[TestCategory("GodotRuntime")]
public class EntityLoopTests
{
    [TestMethod]
    public void Inventory_AddMaterial_IncrementsCount()
    {
        var inventory = new InventoryComponent();

        var afterFirst = inventory.AddMaterial("basic_essence", 2);
        var afterSecond = inventory.AddMaterial("basic_essence", 3);

        Assert.AreEqual(2, afterFirst);
        Assert.AreEqual(5, afterSecond);
        Assert.AreEqual(5, inventory.GetMaterialCount("basic_essence"));
    }

    [TestMethod]
    public void MaterialPickup_WithExplicitInventoryPath_AddsMaterialAndEmitsCollected()
    {
        var root = new Node2D();
        var collector = new Node2D { Name = "Collector" };
        var inventory = new InventoryComponent { Name = "InventoryComponent" };
        var pickup = new MaterialPickup
        {
            MaterialId = "basic_essence",
            Amount = 4,
            InventoryPath = new NodePath("../Collector/InventoryComponent")
        };

        root.AddChild(collector);
        collector.AddChild(inventory);
        root.AddChild(pickup);

        pickup._Ready();

        var collectedCount = 0;
        pickup.Collected += (_, _, _) => collectedCount++;

        // Use reflection to invoke private collection path directly for deterministic testing.
        var collectMethod = typeof(MaterialPickup).GetMethod(
            "TryCollectFromNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(collectMethod);

        var result = (bool)collectMethod.Invoke(pickup, new object[] { collector });

        Assert.IsTrue(result);
        Assert.AreEqual(1, collectedCount);
        Assert.AreEqual(4, inventory.GetMaterialCount("basic_essence"));
    }

    [TestMethod]
    public void DropComponent_OnOwnerDeath_EmitsDropRequested()
    {
        var enemyRoot = new Node2D();
        var health = new HealthComponent { Name = "HealthComponent", MaxHp = 10f, StartAtMaxHp = true };
        var drop = new DropComponent { Name = "DropComponent" };

        enemyRoot.AddChild(health);
        enemyRoot.AddChild(drop);

        health._Ready();
        drop._Ready();

        var dropSignalCount = 0;
        Node emittedOwner = null;
        Vector2 emittedPosition = Vector2.Zero;

        drop.DropRequested += (owner, position) =>
        {
            dropSignalCount++;
            emittedOwner = owner;
            emittedPosition = position;
        };

        health.TakeDamage(999f);

        Assert.AreEqual(1, dropSignalCount);
        Assert.AreEqual(enemyRoot, emittedOwner);
        Assert.AreEqual(enemyRoot.GlobalPosition, emittedPosition);
    }
}
