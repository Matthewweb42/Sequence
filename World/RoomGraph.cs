using Godot;
using System.Collections.Generic;
using Sequence.Autoloads;

namespace Sequence.World;

// ─────────────────────────────────────────────
//  Enums
// ─────────────────────────────────────────────

/// <summary>
/// Cardinal directions used during the random walk to place rooms on the grid.
/// </summary>
public enum Direction
{
    North,
    South,
    East,
    West
}

/// <summary>
/// Room content archetypes as defined in the HLD.
/// </summary>
public enum RoomArchetype
{
    Combat,
    SequenceShrine,
    Material,
    Lore,
    BossAntechamber,
    BossRoom,
    Hidden
}

// ─────────────────────────────────────────────
//  Supporting Data Structures
// ─────────────────────────────────────────────

/// <summary>
/// Metadata for a single door connecting two rooms.
/// Branch-entry doors gate new branches; cross-connection doors
/// link rooms on different branches. Both types lock based on
/// <see cref="RequiredSequence"/>.
/// </summary>
public class DoorInfo
{
    /// <summary>The Sequence level the player must have reached to unlock this door.</summary>
    public int RequiredSequence { get; set; }

    /// <summary>The branch IDs that this door bridges (tuple of two).</summary>
    public (int BranchA, int BranchB) ConnectedBranches { get; set; }

    /// <summary>Current lock state; updated when SequenceAdvanced fires.</summary>
    public bool IsLocked { get; set; } = true;
}

/// <summary>
/// Graph node representing a single room in the generated map.
/// Holds placement data, archetype, branch ownership, and adjacency info.
/// </summary>
public class RoomNode
{
    /// <summary>Unique identifier for this room within the run.</summary>
    public int RoomId { get; set; }

    /// <summary>Grid position assigned during the random walk.</summary>
    public Vector2I GridPosition { get; set; }

    /// <summary>The Sequence branch this room belongs to.</summary>
    public int BranchId { get; set; }

    /// <summary>Content archetype (Combat, Shrine, Material, etc.).</summary>
    public RoomArchetype Archetype { get; set; }

    /// <summary>Whether all enemies in this room have been defeated.</summary>
    public bool IsCleared { get; set; }

    /// <summary>Whether the player has entered this room at least once.</summary>
    public bool IsVisited { get; set; }

    /// <summary>
    /// Adjacency list: maps a neighbouring RoomNode to the DoorInfo
    /// that separates them (null if the connection is always open).
    /// </summary>
    public Dictionary<RoomNode, DoorInfo?> Neighbours { get; set; } = new();
}

// ─────────────────────────────────────────────
//  RoomGraph — world generation & runtime map
// ─────────────────────────────────────────────

/// <summary>
/// Owns the generated room map for the duration of a run.
/// Implements the random-walk-with-sequence-branches algorithm
/// described in HLD Section 6.
/// </summary>
public partial class RoomGraph : Node
{
    // ── Configuration ────────────────────────

    /// <summary>Configurable number of rooms per branch (set in inspector or resource).</summary>
    [Export] public int RoomsPerBranch { get; set; } = 8;

    /// <summary>Probability (0-1) of adding a cross-connection between spatially adjacent rooms on different branches.</summary>
    [Export] public float CrossConnectionProbability { get; set; } = 0.3f;

    /// <summary>Maximum generation retries before falling back to a known-good layout.</summary>
    [Export] public int MaxRetries { get; set; } = 10;

    // ── Runtime State ────────────────────────

    /// <summary>Seeded RNG for deterministic generation.</summary>
    private RandomNumberGenerator _rng = new();

    /// <summary>Master list of all rooms keyed by RoomId.</summary>
    private Dictionary<int, RoomNode> _rooms = new();

    /// <summary>Spatial lookup: grid position → RoomNode occupying that cell.</summary>
    private Dictionary<Vector2I, RoomNode> _grid = new();

    /// <summary>Ordered list of branch IDs generated (highest Sequence first).</summary>
    private List<int> _branchOrder = new();

    /// <summary>Next auto-incremented room ID.</summary>
    private int _nextRoomId;

    /// <summary>Room ID of the starting room (root of reachability queries).</summary>
    private int _startRoomId;

    // ═══════════════════════════════════════════
    //  Godot Lifecycle
    // ═══════════════════════════════════════════

    public override void _Ready()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.SequenceAdvanced += OnSequenceAdvanced;
        }
    }

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.SequenceAdvanced -= OnSequenceAdvanced;
        }
    }

    // ═══════════════════════════════════════════
    //  Public API — Generation Entry Point
    // ═══════════════════════════════════════════

    /// <summary>
    /// Top-level entry point. Generates the entire map for a run.
    /// Corresponds to HLD steps 1-6.
    /// </summary>
    /// <param name="seed">RNG seed from RunManager.CurrentSeed.</param>
    /// <param name="sequenceStart">The player's starting Sequence (e.g. 9).</param>
    /// <param name="sequenceFinal">The final Sequence to reach before the boss (e.g. 5).</param>
    public void GenerateWorld(int seed, int sequenceStart, int sequenceFinal)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            // Step 1: Seed & configure
            SeedAndConfigure(seed + attempt);

            // Step 2: Walk primary branch
            _branchOrder.Add(sequenceStart);
            WalkPrimaryBranch(sequenceStart);

            // Step 3: Walk subsequent branches (one per advancement + one boss branch)
            bool generationFailed = false;
            for (int seq = sequenceStart - 1; seq >= sequenceFinal; seq--)
            {
                _branchOrder.Add(seq);
                bool isBossBranch = (seq == sequenceFinal);
                List<RoomNode>? branchRooms = WalkSubsequentBranch(seq, isBossBranch);

                // I might make changes to the WalkSubsequentBranch algo bc it'll make exploration more interesting if the branches cross more and therefore should handle crossed pathes better
                if (branchRooms == null || branchRooms.Count == 0)
                {
                    // Walk failed (no valid fork point or fully blocked grid)
                    generationFailed = true;
                    break;
                }
            }

            if (generationFailed)
            {
                continue;
            }

            // Step 4: Create inter-branch connections
            CreateInterBranchConnections();

            // Step 5: Populate rooms
            PopulateRooms();

            // Step 6: Validation pass
            if (ValidateGraph())
            {
                GD.Print($"[RoomGraph] World generated successfully on attempt {attempt + 1} (seed {seed + attempt}).");
                return;
            }
        }

        // All retries exhausted — use fallback
        GD.PrintErr($"[RoomGraph] Generation failed after {MaxRetries} attempts. Loading fallback layout.");
        LoadFallbackLayout();
    }

    // ═══════════════════════════════════════════
    //  Generation Steps (private)
    // ═══════════════════════════════════════════

    /// <summary>
    /// HLD Step 1 — Initialise the RNG and clear any previous generation state.
    /// </summary>
    /// <param name="seed">RNG seed.</param>
    private void SeedAndConfigure(int seed)
    {
        _rng.Seed = (ulong)seed;
        _rooms.Clear();
        _grid.Clear();
        _branchOrder.Clear();
        _nextRoomId = 0;
        _startRoomId = 0;
    }

    /// <summary>
    /// HLD Step 2 — Walk the primary (starting) branch from the Start Room.
    /// Places rooms via random walk for a configurable length and terminates
    /// with a Sequence Shrine.
    /// </summary>
    /// <param name="branchId">Sequence tier for this branch (e.g. 9).</param>
    /// <returns>The list of RoomNodes placed on this branch.</returns>
    private List<RoomNode> WalkPrimaryBranch(int branchId)
    {
        Vector2I origin = Vector2I.Zero;
        List<RoomNode> rooms = PerformRandomWalk(origin, branchId, RoomsPerBranch);

        if (rooms.Count > 0)
        {
            _startRoomId = rooms[0].RoomId;
        }

        return rooms;
    }

    /// <summary>
    /// HLD Step 3 — Walk a subsequent branch that forks from a random room
    /// on any previously placed branch. The fork point gets a branch-entry
    /// door tagged with <paramref name="branchId"/>.
    /// </summary>
    /// <param name="branchId">Sequence tier for this branch.</param>
    /// <param name="isBossBranch">If true, terminates with Boss Antechamber + Boss Room instead of a Shrine.</param>
    /// <returns>The list of RoomNodes placed on this branch, or null if no valid fork point was found.</returns>
    private List<RoomNode>? WalkSubsequentBranch(int branchId, bool isBossBranch)
    {
        // Pick a fork point on an existing branch that has at least one free adjacent cell
        RoomNode? forkRoom = SelectForkPoint();
        if (forkRoom == null)
        {
            return null;
        }

        // Find a free adjacent cell next to the fork point to start the new branch
        Direction? dir = PickRandomDirection(forkRoom.GridPosition);
        if (dir == null)
        {
            return null;
        }

        Vector2I branchStart = forkRoom.GridPosition + DirectionToOffset(dir.Value);

        // Walk the new branch
        List<RoomNode> rooms = PerformRandomWalk(branchStart, branchId, RoomsPerBranch, isBossBranch);
        if (rooms.Count == 0)
        {
            return null;
        }

        // Gate the entry to this branch with a sequence-locked door
        CreateBranchEntryDoor(forkRoom, rooms[0], branchId);        // TODO: If branches are changed to interconnect differently, we need to handle sequence gates so that they apply to every cross connection

        return rooms;
    }

    /// <summary>
    /// Core random walk logic shared by primary and subsequent branches.
    /// Picks a random cardinal direction, checks the grid for collisions,
    /// and places the next room from the archetype schedule.
    /// </summary>
    /// <param name="startPosition">Grid cell where this walk begins.</param>
    /// <param name="branchId">Branch ID to stamp on every placed room.</param>
    /// <param name="roomCount">Number of rooms to place on this walk.</param>
    /// <param name="isBossBranch">Whether this branch terminates with the boss.</param>
    /// <returns>Ordered list of placed RoomNodes.</returns>
    private List<RoomNode> PerformRandomWalk(Vector2I startPosition, int branchId, int roomCount, bool isBossBranch = false)
    {
        var placed = new List<RoomNode>();
        Vector2I currentPos = startPosition;

        for (int i = 0; i < roomCount; i++)
        {
            // If the current position is already occupied (shouldn't happen for the
            // first room unless the caller made an error), bail out
            if (_grid.ContainsKey(currentPos))
            {
                break;
            }

            bool isLastRoom = (i == roomCount - 1);
            RoomArchetype archetype = DetermineArchetype(i, isLastRoom, isBossBranch);

            // For the boss branch, the second-to-last room is the antechamber
            if (isBossBranch && i == roomCount - 2)
            {
                archetype = RoomArchetype.BossAntechamber;
            }

            var room = new RoomNode
            {
                RoomId = _nextRoomId++,
                GridPosition = currentPos,
                BranchId = branchId,
                Archetype = archetype
            };

            // Register in data structures
            _rooms[room.RoomId] = room;
            _grid[currentPos] = room;
            placed.Add(room);

            // Link to the previous room in this walk with an open connection
            if (placed.Count > 1)
            {
                RoomNode prev = placed[placed.Count - 2];
                prev.Neighbours[room] = null;  // open connection
                room.Neighbours[prev] = null;
            }

            // Advance the walker to the next cell (unless this is the last room)
            if (!isLastRoom)
            {
                Direction? nextDir = PickRandomDirection(currentPos);
                if (nextDir == null)
                {
                    // Stuck — no free adjacent cells; end the walk early.
                    // Mark the current (final) room with the terminal archetype.
                    room.Archetype = isBossBranch ? RoomArchetype.BossRoom : RoomArchetype.SequenceShrine;
                    break;
                }
                currentPos = currentPos + DirectionToOffset(nextDir.Value);
            }
        }

        return placed;
    }

    /// <summary>
    /// Selects a random valid cardinal direction from the given position
    /// that does not collide with an already-occupied grid cell.
    /// </summary>
    /// <param name="currentPosition">The walker's current grid cell.</param>
    /// <returns>The chosen direction, or null if all neighbours are occupied.</returns>
    private Direction? PickRandomDirection(Vector2I currentPosition)
    {
        var candidates = new List<Direction>();

        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            Vector2I neighbour = currentPosition + DirectionToOffset(dir);
            if (!_grid.ContainsKey(neighbour))
            {
                candidates.Add(dir);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int index = _rng.RandiRange(0, candidates.Count - 1);
        return candidates[index];
    }

    /// <summary>
    /// Returns the grid offset for a given cardinal direction.
    /// </summary>
    private Vector2I DirectionToOffset(Direction direction)
    {
        return direction switch
        {
            Direction.North => new Vector2I(0, -1),
            Direction.South => new Vector2I(0, 1),
            Direction.East  => new Vector2I(1, 0),
            Direction.West  => new Vector2I(-1, 0),
            _ => Vector2I.Zero
        };
    }

    /// <summary>
    /// Determines the room archetype for a given index within a branch,
    /// following the archetype schedule (Combat → Material → Combat → Shrine, etc.).
    /// </summary>
    /// <param name="indexInBranch">Zero-based position along the branch.</param>
    /// <param name="isLastRoom">True if this is the terminal room of the branch.</param>
    /// <param name="isBossBranch">True if this branch ends with the boss.</param>
    /// <returns>The archetype to assign.</returns>
    private RoomArchetype DetermineArchetype(int indexInBranch, bool isLastRoom, bool isBossBranch)
    {// TODO: This is Super up for changes
        // Terminal room: Shrine (normal branch) or BossRoom (boss branch)
        if (isLastRoom)
        {
            return isBossBranch ? RoomArchetype.BossRoom : RoomArchetype.SequenceShrine;
        }

        // First room is always Combat (entrance encounter)
        if (indexInBranch == 0)
        {
            return RoomArchetype.Combat;
        }

        // Repeating schedule for middle rooms:
        //   Combat → Material → Combat → Lore → Combat → ...
        int phase = (indexInBranch - 1) % 4;
        return phase switch
        {
            0 => RoomArchetype.Combat,
            1 => RoomArchetype.Material,
            2 => RoomArchetype.Combat,
            3 => RoomArchetype.Lore,
            _ => RoomArchetype.Combat
        };
    }

    /// <summary>
    /// Selects a random fork point from all rooms placed so far.
    /// Excludes rooms that have no free adjacent grid cells, Shrine rooms,
    /// and Boss rooms to keep fork points in traversal-friendly locations.
    /// </summary>
    /// <returns>The RoomNode chosen as the fork origin, or null if none available.</returns>
    private RoomNode? SelectForkPoint()
    {
        var candidates = new List<RoomNode>();

        foreach (RoomNode room in _rooms.Values)
        {
            // Skip terminal rooms (Shrines, Boss rooms) — forks should come from mid-branch
            if (room.Archetype == RoomArchetype.SequenceShrine ||
                room.Archetype == RoomArchetype.BossRoom ||
                room.Archetype == RoomArchetype.BossAntechamber)
            {
                continue;
            }
            // Must have at least one free adjacent cell to start a new branch from
            if (PickRandomDirection(room.GridPosition) != null)
            {
                candidates.Add(room);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int index = _rng.RandiRange(0, candidates.Count - 1);
        return candidates[index];
    }

    /// <summary>
    /// Creates a branch-entry door between the fork-point room and the
    /// first room of the new branch.
    /// </summary>
    /// <param name="forkRoom">Room on the existing branch.</param>
    /// <param name="branchEntryRoom">First room of the new branch.</param>
    /// <param name="requiredSequence">Sequence level needed to unlock.</param>
    private void CreateBranchEntryDoor(RoomNode forkRoom, RoomNode branchEntryRoom, int requiredSequence)
    {
        var door = new DoorInfo
        {
            RequiredSequence = requiredSequence,
            ConnectedBranches = (forkRoom.BranchId, branchEntryRoom.BranchId),
            IsLocked = true
        };

        forkRoom.Neighbours[branchEntryRoom] = door;
        branchEntryRoom.Neighbours[forkRoom] = door;
    }

    /// <summary>
    /// HLD Step 4 — Scans the grid for rooms on different branches that are
    /// spatially adjacent and probabilistically adds cross-connection doors
    /// gated at RequiredSequence = min(branchA, branchB).
    /// </summary>
    private void CreateInterBranchConnections()
    {
        // Check each room against all four cardinal neighbours
        foreach (RoomNode room in _rooms.Values)
        {
            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                Vector2I adjacentPos = room.GridPosition + DirectionToOffset(dir);

                if (!_grid.TryGetValue(adjacentPos, out RoomNode? adjacentRoom) || adjacentRoom == null)
                {
                    continue;
                }

                // Only interested in rooms on different branches
                if (adjacentRoom.BranchId == room.BranchId)
                {
                    continue;
                }

                // Skip if already connected (branch-entry door or previous cross-connection)
                if (room.Neighbours.ContainsKey(adjacentRoom))
                {
                    continue;
                }

                // Probabilistic: roll against CrossConnectionProbability
                if (_rng.Randf() > CrossConnectionProbability)
                {
                    continue;
                }

                // Gate at the more advanced (lower number) of the two branches
                int requiredSeq = System.Math.Min(room.BranchId, adjacentRoom.BranchId);

                var door = new DoorInfo
                {
                    RequiredSequence = requiredSeq,
                    ConnectedBranches = (room.BranchId, adjacentRoom.BranchId),
                    IsLocked = true
                };

                room.Neighbours[adjacentRoom] = door;
                adjacentRoom.Neighbours[room] = door;
            }
        }
    }

    /// <summary>
    /// HLD Step 5 — Populates each room with content matching its archetype.
    /// This sets up the data-level room content. Actual scene instantiation
    /// (enemy spawn points, material nodes, etc.) happens when a RoomInstance
    /// is loaded by RunManager.TransitionToRoom.
    /// </summary>
    private void PopulateRooms()
    {
        // Population is currently a no-op at the graph level.
        // Room content (enemies, pickups) is driven by the RoomInstance scene
        // based on the Archetype tag set during generation.
        // This hook exists for future logic such as difficulty scaling,
        // loot budget distribution, or hidden-room placement.
    }

    /// <summary>
    /// HLD Step 6 — Validates that all Shrines and the Boss Room
    /// are reachable from the Start Room assuming the player advances
    /// at every Shrine encountered in order.
    /// Uses a simulated BFS that unlocks doors as Shrines are reached.
    /// </summary>
    /// <returns>True if the generated layout is valid.</returns>
    private bool ValidateGraph()
    {
        // Collect mandatory rooms: all Shrines + BossRoom
        var mandatoryRooms = new List<RoomNode>();
        RoomNode? bossRoom = null;

        foreach (RoomNode room in _rooms.Values)
        {
            if (room.Archetype == RoomArchetype.SequenceShrine)
            {
                mandatoryRooms.Add(room);
            }
            else if (room.Archetype == RoomArchetype.BossRoom)
            {
                bossRoom = room;
            }
        }

        if (bossRoom == null)
        {
            return false;
        }

        // Sort shrines by branch ID descending (highest Sequence first = earliest to reach)
        mandatoryRooms.Sort((a, b) => b.BranchId.CompareTo(a.BranchId));
        mandatoryRooms.Add(bossRoom);

        // Simulate progression: start at Sequence = highest branch,
        // advance each time we confirm a Shrine is reachable
        int simulatedSequence = _branchOrder.Count > 0 ? _branchOrder[0] : 9;

        foreach (RoomNode target in mandatoryRooms)
        {
            if (!IsRoomReachable(target.RoomId, simulatedSequence))
            {
                return false;
            }

            // Simulate advancement after reaching a Shrine
            if (target.Archetype == RoomArchetype.SequenceShrine)
            {
                simulatedSequence--;
                // Unlock doors at this new level so subsequent checks work correctly
                OnSequenceAdvanced(simulatedSequence);
            }
        }

        // Re-lock all doors (validation shouldn't leave permanent side effects)
        foreach (RoomNode room in _rooms.Values)
        {
            foreach (DoorInfo? door in room.Neighbours.Values)
            {
                if (door != null)
                {
                    door.IsLocked = true;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Fallback when validation fails after <see cref="MaxRetries"/> attempts.
    /// Loads a known-good, pre-authored layout.
    /// </summary>
    private void LoadFallbackLayout()
    {
        // TODO: Load a pre-authored room graph from a saved resource.
        // For now, generate a minimal valid linear layout.
        SeedAndConfigure(0);

        GD.PrintErr("[RoomGraph] Fallback layout not yet implemented. Generating minimal linear map.");
    }

    // ═══════════════════════════════════════════
    //  Public API — Runtime Queries
    // ═══════════════════════════════════════════

    /// <summary>
    /// Returns the list of rooms adjacent to the given room,
    /// regardless of door lock state.
    /// </summary>
    /// <param name="roomId">ID of the room to query.</param>
    /// <returns>List of adjacent RoomNodes.</returns>
    public List<RoomNode> GetAdjacentRooms(int roomId)
    {
        if (!_rooms.TryGetValue(roomId, out RoomNode? room) || room == null)
        {
            return new List<RoomNode>();
        }

        return new List<RoomNode>(room.Neighbours.Keys);
    }

    /// <summary>
    /// Returns the door metadata between two rooms, or null if
    /// they are openly connected (no door) or not adjacent.
    /// </summary>
    /// <param name="roomIdA">First room ID.</param>
    /// <param name="roomIdB">Second room ID.</param>
    /// <returns>DoorInfo if a gated door exists; otherwise null.</returns>
    public DoorInfo? GetDoorBetween(int roomIdA, int roomIdB)
    {
        if (!_rooms.TryGetValue(roomIdA, out RoomNode? roomA) || roomA == null)
        {
            return null;
        }

        if (!_rooms.TryGetValue(roomIdB, out RoomNode? roomB) || roomB == null)
        {
            return null;
        }

        // Check if roomB is a neighbour of roomA and return the door (may be null for open connections)
        roomA.Neighbours.TryGetValue(roomB, out DoorInfo? door);
        return door;
    }

    /// <summary>
    /// Determines whether the given room is reachable from the Start Room
    /// given the player's current Sequence level (i.e., all doors along the
    /// path have RequiredSequence >= playerSequence).
    /// </summary>
    /// <param name="roomId">Target room ID.</param>
    /// <param name="playerSequence">Player's current Sequence number.</param>
    /// <returns>True if a valid path exists.</returns>
    public bool IsRoomReachable(int roomId, int playerSequence)
    {
        if (!_rooms.ContainsKey(roomId) || !_rooms.ContainsKey(_startRoomId))
        {
            return false;
        }

        // BFS from the start room, only traversing edges whose doors are
        // either absent (open connection) or unlocked at the player's current Sequence.
        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        visited.Add(_startRoomId);
        queue.Enqueue(_startRoomId);

        while (queue.Count > 0)
        {
            int currentId = queue.Dequeue();
            if (currentId == roomId)
            {
                return true;
            }

            RoomNode current = _rooms[currentId];
            foreach (var (neighbour, door) in current.Neighbours)
            {
                if (visited.Contains(neighbour.RoomId))
                {
                    continue;
                }

                // A null door means an open (ungated) connection.
                // A locked door is passable only if the player's Sequence
                // is at or beyond the required level (lower number = more advanced).
                if (door != null && door.IsLocked && playerSequence > door.RequiredSequence)
                {
                    continue;
                }

                visited.Add(neighbour.RoomId);
                queue.Enqueue(neighbour.RoomId);
            }
        }

        return false;
    }

    /// <summary>
    /// Retrieves a RoomNode by its ID.
    /// </summary>
    /// <param name="roomId">The room's unique identifier.</param>
    /// <returns>The matching RoomNode, or null if not found.</returns>
    public RoomNode? GetRoom(int roomId)
    {
        _rooms.TryGetValue(roomId, out RoomNode? room);
        return room;
    }

    /// <summary>
    /// Returns all rooms belonging to the specified branch.
    /// </summary>
    /// <param name="branchId">The Sequence branch ID.</param>
    /// <returns>List of RoomNodes on that branch.</returns>
    public List<RoomNode> GetRoomsByBranch(int branchId)
    {
        var result = new List<RoomNode>();
        foreach (RoomNode room in _rooms.Values)
        {
            if (room.BranchId == branchId)
            {
                result.Add(room);
            }
        }
        return result;
    }

    /// <summary>
    /// Finds the adjacent room in a given direction from the specified room.
    /// Uses the grid layout to locate neighbors.
    /// </summary>
    /// <param name="roomId">The starting room ID.</param>
    /// <param name="direction">The cardinal direction to search.</param>
    /// <returns>The adjacent RoomNode, or null if none exists in that direction.</returns>
    public RoomNode? GetAdjacentRoomInDirection(int roomId, Direction direction)
    {
        if (!_rooms.TryGetValue(roomId, out RoomNode? room) || room == null)
        {
            return null;
        }

        Vector2I adjacentPos = room.GridPosition + DirectionToOffset(direction);
        _grid.TryGetValue(adjacentPos, out RoomNode? adjacentRoom);
        return adjacentRoom;
    }

    // ═══════════════════════════════════════════
    //  Signal Handlers
    // ═══════════════════════════════════════════

    /// <summary>
    /// Called when the player advances Sequence. Batch-unlocks every door
    /// whose RequiredSequence now matches or exceeds the player's new level.
    /// </summary>
    /// <param name="newSequence">The player's new (lower) Sequence number.</param>
    private void OnSequenceAdvanced(int newSequence)
    {
        // Iterate every room and every door; unlock any door whose
        // RequiredSequence the player now satisfies.
        // Lower Sequence number = more advanced, so unlock when
        // newSequence <= door.RequiredSequence.
        foreach (RoomNode room in _rooms.Values)
        {
            foreach (DoorInfo? door in room.Neighbours.Values)
            {
                if (door == null || !door.IsLocked)
                {
                    continue;
                }

                if (newSequence <= door.RequiredSequence)
                {
                    door.IsLocked = false;
                }
            }
        }
    }
}
