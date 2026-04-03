using Godot;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sequence.Autoloads;
using Sequence.World;

namespace Sequence.Tests;

/// <summary>
/// Unit tests for the RoomGraph public API and signal handler.
/// Uses reflection to inject graph state into RoomGraph's private fields,
/// allowing us to test the implemented query logic without running the
/// (unimplemented) generation pipeline.
/// </summary>
[TestClass]
[TestCategory("GodotRuntime")]
public class RoomGraphTests
{
    // ─────────────────────────────────────────────
    //  Test Helpers
    // ─────────────────────────────────────────────

    /// <summary>
    /// Creates a RoomGraph instance and injects the given rooms and start room ID
    /// into its private backing fields via reflection.
    /// </summary>
    private static RoomGraph BuildGraph(Dictionary<int, RoomNode> rooms, int startRoomId)
    {
        var graph = new RoomGraph();

        typeof(RoomGraph)
            .GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(graph, rooms);

        typeof(RoomGraph)
            .GetField("_startRoomId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(graph, startRoomId);

        return graph;
    }

    /// <summary>
    /// Invokes the private OnSequenceAdvanced method via reflection.
    /// </summary>
    private static void InvokeOnSequenceAdvanced(RoomGraph graph, int newSequence)
    {
        typeof(RoomGraph)
            .GetMethod("OnSequenceAdvanced", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(graph, new object[] { newSequence });
    }

    /// <summary>
    /// Shorthand: creates a RoomNode with the given id and branch.
    /// </summary>
    private static RoomNode MakeRoom(int id, int branchId = 9, RoomArchetype archetype = RoomArchetype.Combat)
    {
        return new RoomNode
        {
            RoomId = id,
            BranchId = branchId,
            Archetype = archetype,
            GridPosition = new Vector2I(id, 0) // arbitrary unique position
        };
    }

    /// <summary>
    /// Links two rooms with an open (ungated) connection in both directions.
    /// </summary>
    private static void LinkOpen(RoomNode a, RoomNode b)
    {
        a.Neighbours[b] = null;
        b.Neighbours[a] = null;
    }

    /// <summary>
    /// Links two rooms with a locked door in both directions.
    /// </summary>
    private static DoorInfo LinkLocked(RoomNode a, RoomNode b, int requiredSequence)
    {
        var door = new DoorInfo
        {
            RequiredSequence = requiredSequence,
            ConnectedBranches = (a.BranchId, b.BranchId),
            IsLocked = true
        };
        a.Neighbours[b] = door;
        b.Neighbours[a] = door;
        return door;
    }

    /// <summary>
    /// Builds the rooms dictionary from a params array of RoomNodes.
    /// </summary>
    private static Dictionary<int, RoomNode> RoomsDict(params RoomNode[] nodes)
    {
        var dict = new Dictionary<int, RoomNode>();
        foreach (var n in nodes) dict[n.RoomId] = n;
        return dict;
    }

    // ─────────────────────────────────────────────
    //  Shared test graph topology
    // ─────────────────────────────────────────────
    //
    //  Branch 9:  [0] ── [1] ── [2](Shrine)
    //                      │
    //              (locked door, req 8)
    //                      │
    //  Branch 8:          [3] ── [4](Shrine)
    //                             │
    //                     (locked door, req 7)
    //                             │
    //  Branch 7:                 [5](BossRoom)
    //
    //  Cross-connection:  [2] ── [5]  (locked, req 7)
    // ─────────────────────────────────────────────

    private RoomNode _r0, _r1, _r2, _r3, _r4, _r5;
    private DoorInfo _door8, _door7, _crossDoor;
    private RoomGraph _graph;

    private void SetUpStandardGraph()
    {
        _r0 = MakeRoom(0, branchId: 9);
        _r1 = MakeRoom(1, branchId: 9);
        _r2 = MakeRoom(2, branchId: 9, archetype: RoomArchetype.SequenceShrine);
        _r3 = MakeRoom(3, branchId: 8);
        _r4 = MakeRoom(4, branchId: 8, archetype: RoomArchetype.SequenceShrine);
        _r5 = MakeRoom(5, branchId: 7, archetype: RoomArchetype.BossRoom);

        // Branch 9: open chain
        LinkOpen(_r0, _r1);
        LinkOpen(_r1, _r2);

        // Branch 8 forks from room 1 with a locked door (requires Sequence 8)
        _door8 = LinkLocked(_r1, _r3, requiredSequence: 8);

        // Branch 8 internal open connection
        LinkOpen(_r3, _r4);

        // Branch 7 forks from room 4 with a locked door (requires Sequence 7)
        _door7 = LinkLocked(_r4, _r5, requiredSequence: 7);

        // Cross-connection: room 2 (branch 9) ↔ room 5 (branch 7), locked at min(9,7)=7
        _crossDoor = LinkLocked(_r2, _r5, requiredSequence: 7);

        _graph = BuildGraph(RoomsDict(_r0, _r1, _r2, _r3, _r4, _r5), startRoomId: 0);
    }

    // ═══════════════════════════════════════════
    //  GetRoom
    // ═══════════════════════════════════════════

    [TestMethod]
    public void GetRoom_ExistingId_ReturnsCorrectNode()
    {
        SetUpStandardGraph();

        RoomNode result = _graph.GetRoom(0);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.RoomId);
        Assert.AreEqual(9, result.BranchId);
    }

    [TestMethod]
    public void GetRoom_NonExistentId_ReturnsNull()
    {
        SetUpStandardGraph();

        RoomNode result = _graph.GetRoom(999);

        Assert.IsNull(result);
    }

    // ═══════════════════════════════════════════
    //  GetAdjacentRooms
    // ═══════════════════════════════════════════

    [TestMethod]
    public void GetAdjacentRooms_MiddleNode_ReturnsAllNeighbours()
    {
        SetUpStandardGraph();

        // Room 1 connects to: room 0 (open), room 2 (open), room 3 (locked door)
        List<RoomNode> neighbours = _graph.GetAdjacentRooms(1);

        Assert.AreEqual(3, neighbours.Count);
        CollectionAssert.Contains(neighbours, _r0);
        CollectionAssert.Contains(neighbours, _r2);
        CollectionAssert.Contains(neighbours, _r3);
    }

    [TestMethod]
    public void GetAdjacentRooms_LeafNode_ReturnsSingleNeighbour()
    {
        SetUpStandardGraph();

        // Room 0 only connects to room 1
        List<RoomNode> neighbours = _graph.GetAdjacentRooms(0);

        Assert.AreEqual(1, neighbours.Count);
        Assert.AreEqual(_r1, neighbours[0]);
    }

    [TestMethod]
    public void GetAdjacentRooms_InvalidId_ReturnsEmptyList()
    {
        SetUpStandardGraph();

        List<RoomNode> neighbours = _graph.GetAdjacentRooms(999);

        Assert.IsNotNull(neighbours);
        Assert.AreEqual(0, neighbours.Count);
    }

    [TestMethod]
    public void GetAdjacentRooms_IncludesLockedDoorNeighbours()
    {
        SetUpStandardGraph();

        // Room 4 connects to room 3 (open) and room 5 (locked)
        List<RoomNode> neighbours = _graph.GetAdjacentRooms(4);

        Assert.AreEqual(2, neighbours.Count);
        CollectionAssert.Contains(neighbours, _r3);
        CollectionAssert.Contains(neighbours, _r5);
    }

    // ═══════════════════════════════════════════
    //  GetDoorBetween
    // ═══════════════════════════════════════════

    [TestMethod]
    public void GetDoorBetween_LockedDoor_ReturnsDoorInfo()
    {
        SetUpStandardGraph();

        DoorInfo door = _graph.GetDoorBetween(1, 3);

        Assert.IsNotNull(door);
        Assert.AreEqual(8, door.RequiredSequence);
        Assert.IsTrue(door.IsLocked);
    }

    [TestMethod]
    public void GetDoorBetween_OpenConnection_ReturnsNull()
    {
        SetUpStandardGraph();

        // Rooms 0 and 1 are openly connected (no door)
        DoorInfo door = _graph.GetDoorBetween(0, 1);

        Assert.IsNull(door);
    }

    [TestMethod]
    public void GetDoorBetween_NonAdjacentRooms_ReturnsNull()
    {
        SetUpStandardGraph();

        // Rooms 0 and 5 are not adjacent at all
        DoorInfo door = _graph.GetDoorBetween(0, 5);

        Assert.IsNull(door);
    }

    [TestMethod]
    public void GetDoorBetween_InvalidRoomA_ReturnsNull()
    {
        SetUpStandardGraph();

        DoorInfo door = _graph.GetDoorBetween(999, 0);

        Assert.IsNull(door);
    }

    [TestMethod]
    public void GetDoorBetween_InvalidRoomB_ReturnsNull()
    {
        SetUpStandardGraph();

        DoorInfo door = _graph.GetDoorBetween(0, 999);

        Assert.IsNull(door);
    }

    [TestMethod]
    public void GetDoorBetween_IsSymmetric()
    {
        SetUpStandardGraph();

        DoorInfo doorAB = _graph.GetDoorBetween(1, 3);
        DoorInfo doorBA = _graph.GetDoorBetween(3, 1);

        // Both directions should return the same DoorInfo instance
        Assert.AreSame(doorAB, doorBA);
    }

    // ═══════════════════════════════════════════
    //  GetRoomsByBranch
    // ═══════════════════════════════════════════

    [TestMethod]
    public void GetRoomsByBranch_Branch9_ReturnsThreeRooms()
    {
        SetUpStandardGraph();

        List<RoomNode> branch9 = _graph.GetRoomsByBranch(9);

        Assert.AreEqual(3, branch9.Count);
        CollectionAssert.Contains(branch9, _r0);
        CollectionAssert.Contains(branch9, _r1);
        CollectionAssert.Contains(branch9, _r2);
    }

    [TestMethod]
    public void GetRoomsByBranch_Branch8_ReturnsTwoRooms()
    {
        SetUpStandardGraph();

        List<RoomNode> branch8 = _graph.GetRoomsByBranch(8);

        Assert.AreEqual(2, branch8.Count);
        CollectionAssert.Contains(branch8, _r3);
        CollectionAssert.Contains(branch8, _r4);
    }

    [TestMethod]
    public void GetRoomsByBranch_Branch7_ReturnsBossRoom()
    {
        SetUpStandardGraph();

        List<RoomNode> branch7 = _graph.GetRoomsByBranch(7);

        Assert.AreEqual(1, branch7.Count);
        Assert.AreEqual(RoomArchetype.BossRoom, branch7[0].Archetype);
    }

    [TestMethod]
    public void GetRoomsByBranch_NonExistentBranch_ReturnsEmptyList()
    {
        SetUpStandardGraph();

        List<RoomNode> branch6 = _graph.GetRoomsByBranch(6);

        Assert.IsNotNull(branch6);
        Assert.AreEqual(0, branch6.Count);
    }

    // ═══════════════════════════════════════════
    //  IsRoomReachable
    // ═══════════════════════════════════════════

    [TestMethod]
    public void IsRoomReachable_StartRoom_AlwaysReachable()
    {
        SetUpStandardGraph();

        Assert.IsTrue(_graph.IsRoomReachable(0, playerSequence: 9));
    }

    [TestMethod]
    public void IsRoomReachable_OpenBranch_ReachableAtStartingSequence()
    {
        SetUpStandardGraph();

        // Rooms 0, 1, 2 are all on branch 9 with open connections
        Assert.IsTrue(_graph.IsRoomReachable(1, playerSequence: 9));
        Assert.IsTrue(_graph.IsRoomReachable(2, playerSequence: 9));
    }

    [TestMethod]
    public void IsRoomReachable_LockedBranch_NotReachableAtHigherSequence()
    {
        SetUpStandardGraph();

        // Room 3 is behind a door requiring Sequence 8; player is at 9
        Assert.IsFalse(_graph.IsRoomReachable(3, playerSequence: 9));
    }

    [TestMethod]
    public void IsRoomReachable_LockedBranch_ReachableAfterAdvancement()
    {
        SetUpStandardGraph();

        // At Sequence 8, the door (req 8) should be passable
        Assert.IsTrue(_graph.IsRoomReachable(3, playerSequence: 8));
        Assert.IsTrue(_graph.IsRoomReachable(4, playerSequence: 8));
    }

    [TestMethod]
    public void IsRoomReachable_DeepBranch_RequiresMultipleAdvancements()
    {
        SetUpStandardGraph();

        // Room 5 (branch 7) requires passing through door8 (req 8) and door7 (req 7)
        Assert.IsFalse(_graph.IsRoomReachable(5, playerSequence: 9));
        Assert.IsFalse(_graph.IsRoomReachable(5, playerSequence: 8));
        Assert.IsTrue(_graph.IsRoomReachable(5, playerSequence: 7));
    }

    [TestMethod]
    public void IsRoomReachable_CrossConnection_UsableWhenUnlocked()
    {
        SetUpStandardGraph();

        // At Sequence 7, the cross-connection door (req 7) between room 2 and 5
        // provides a shortcut. Room 5 should be reachable.
        Assert.IsTrue(_graph.IsRoomReachable(5, playerSequence: 7));
    }

    [TestMethod]
    public void IsRoomReachable_InvalidRoomId_ReturnsFalse()
    {
        SetUpStandardGraph();

        Assert.IsFalse(_graph.IsRoomReachable(999, playerSequence: 9));
    }

    [TestMethod]
    public void IsRoomReachable_DisconnectedRoom_ReturnsFalse()
    {
        // Create a room that exists in the dict but has no connections
        var isolated = MakeRoom(10, branchId: 9);
        var start = MakeRoom(0, branchId: 9);
        var graph = BuildGraph(RoomsDict(start, isolated), startRoomId: 0);

        Assert.IsFalse(graph.IsRoomReachable(10, playerSequence: 1));
    }

    // ═══════════════════════════════════════════
    //  OnSequenceAdvanced (via reflection)
    // ═══════════════════════════════════════════

    [TestMethod]
    public void OnSequenceAdvanced_UnlocksMatchingDoors()
    {
        SetUpStandardGraph();

        // All doors start locked
        Assert.IsTrue(_door8.IsLocked);
        Assert.IsTrue(_door7.IsLocked);
        Assert.IsTrue(_crossDoor.IsLocked);

        // Advance to Sequence 8 → should unlock door8 (req 8)
        InvokeOnSequenceAdvanced(_graph, 8);

        Assert.IsFalse(_door8.IsLocked);
        Assert.IsTrue(_door7.IsLocked);     // req 7, should still be locked
        Assert.IsTrue(_crossDoor.IsLocked); // req 7, should still be locked
    }

    [TestMethod]
    public void OnSequenceAdvanced_UnlocksMultipleDoorsAtSameTier()
    {
        SetUpStandardGraph();

        // Advance to Sequence 7 → should unlock door7 (req 7) AND crossDoor (req 7),
        // and also door8 (req 8, since 7 <= 8)
        InvokeOnSequenceAdvanced(_graph, 7);

        Assert.IsFalse(_door8.IsLocked);
        Assert.IsFalse(_door7.IsLocked);
        Assert.IsFalse(_crossDoor.IsLocked);
    }

    [TestMethod]
    public void OnSequenceAdvanced_AlreadyUnlockedDoors_RemainUnlocked()
    {
        SetUpStandardGraph();

        // Unlock door8 first
        InvokeOnSequenceAdvanced(_graph, 8);
        Assert.IsFalse(_door8.IsLocked);

        // Advance again to 7 — door8 should remain unlocked
        InvokeOnSequenceAdvanced(_graph, 7);
        Assert.IsFalse(_door8.IsLocked);
    }

    [TestMethod]
    public void OnSequenceAdvanced_HigherSequence_UnlocksNothing()
    {
        SetUpStandardGraph();

        // "Advancing" to Sequence 9 (the starting level) should unlock nothing
        // since all doors require 8 or 7
        InvokeOnSequenceAdvanced(_graph, 9);

        Assert.IsTrue(_door8.IsLocked);
        Assert.IsTrue(_door7.IsLocked);
        Assert.IsTrue(_crossDoor.IsLocked);
    }

    [TestMethod]
    public void OnSequenceAdvanced_PublishedOnSignalBus_UnlocksDoors()
    {
        SetUpStandardGraph();

        var signalBus = new SignalBus();
        signalBus._Ready();

        try
        {
            _graph._Ready();

            Assert.IsTrue(_door8.IsLocked);
            Assert.IsTrue(_door7.IsLocked);

            signalBus.PublishSequenceAdvanced(8);

            Assert.IsFalse(_door8.IsLocked);
            Assert.IsTrue(_door7.IsLocked);
        }
        finally
        {
            _graph._ExitTree();
            signalBus._ExitTree();
        }
    }

    // ═══════════════════════════════════════════
    //  Integration: OnSequenceAdvanced + IsRoomReachable
    // ═══════════════════════════════════════════

    [TestMethod]
    public void SequenceAdvancement_ProgressivelyOpensMap()
    {
        SetUpStandardGraph();

        // At Sequence 9: only branch 9 rooms reachable
        Assert.IsTrue(_graph.IsRoomReachable(0, 9));
        Assert.IsTrue(_graph.IsRoomReachable(2, 9));
        Assert.IsFalse(_graph.IsRoomReachable(3, 9));
        Assert.IsFalse(_graph.IsRoomReachable(5, 9));

        // Advance to 8: branch 8 rooms now reachable
        InvokeOnSequenceAdvanced(_graph, 8);
        Assert.IsTrue(_graph.IsRoomReachable(3, 8));
        Assert.IsTrue(_graph.IsRoomReachable(4, 8));
        Assert.IsFalse(_graph.IsRoomReachable(5, 8));

        // Advance to 7: everything reachable including boss
        InvokeOnSequenceAdvanced(_graph, 7);
        Assert.IsTrue(_graph.IsRoomReachable(5, 7));
    }

    // ═══════════════════════════════════════════
    //  Edge Cases
    // ═══════════════════════════════════════════

    [TestMethod]
    public void EmptyGraph_AllQueriesHandleGracefully()
    {
        var graph = BuildGraph(new Dictionary<int, RoomNode>(), startRoomId: 0);

        Assert.IsNull(graph.GetRoom(0));
        Assert.AreEqual(0, graph.GetAdjacentRooms(0).Count);
        Assert.IsNull(graph.GetDoorBetween(0, 1));
        Assert.IsFalse(graph.IsRoomReachable(0, 9));
        Assert.AreEqual(0, graph.GetRoomsByBranch(9).Count);
    }

    [TestMethod]
    public void SingleRoom_IsReachable()
    {
        var room = MakeRoom(0);
        var graph = BuildGraph(RoomsDict(room), startRoomId: 0);

        Assert.IsTrue(graph.IsRoomReachable(0, playerSequence: 9));
        Assert.AreEqual(0, graph.GetAdjacentRooms(0).Count);
    }
}
