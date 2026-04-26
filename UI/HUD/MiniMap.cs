using Godot;
using Sequence.Autoloads;
using Sequence.World;

namespace Sequence.UI.HUD;

public partial class MiniMap : Control
{
    private const int CellSize = 14;
    private const int CellGap = 2;
    private const int Step = CellSize + CellGap;

    private bool _isSubscribed;

    public override void _Ready()
    {
        Subscribe();
    }

    public override void _ExitTree()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_isSubscribed) return;

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.SequenceAdvanced += OnRefreshSequence;
            SignalBus.Instance.ShrineConsumed += OnRefreshRoom;
            SignalBus.Instance.RoomEntered += OnRefreshRoom;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.CurrentRoomChanged += OnRefreshRoom;
            RunManager.Instance.RunStarted += OnRefreshRoom;
        }

        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.SequenceAdvanced -= OnRefreshSequence;
            SignalBus.Instance.ShrineConsumed -= OnRefreshRoom;
            SignalBus.Instance.RoomEntered -= OnRefreshRoom;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.CurrentRoomChanged -= OnRefreshRoom;
            RunManager.Instance.RunStarted -= OnRefreshRoom;
        }

        _isSubscribed = false;
    }

    private void OnRefreshSequence(int _) => QueueRedraw();
    private void OnRefreshRoom(int _) => QueueRedraw();

    public override void _Draw()
    {
        var graph = RoomGraph.Instance;
        if (graph == null) return;

        int currentRoomId = RunManager.Instance?.CurrentRoomId ?? -1;
        var currentRoom = graph.GetRoom(currentRoomId);
        if (currentRoom == null) return;

        var panelCenter = Size / 2f;

        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0f, 0f, 0f, 0.55f), filled: true);

        var visited = new System.Collections.Generic.List<RoomNode>();
        foreach (var room in graph.AllRooms)
        {
            if (room.IsVisited)
                visited.Add(room);
        }

        // Draw door bars beneath room squares.
        // A bar appears as soon as either connected room is visited.
        var drawnPairs = new System.Collections.Generic.HashSet<(int, int)>();
        foreach (var room in visited)
        {
            foreach (var (neighbor, door) in room.Neighbours)
            {
                int lo = System.Math.Min(room.RoomId, neighbor.RoomId);
                int hi = System.Math.Max(room.RoomId, neighbor.RoomId);
                if (!drawnPairs.Add((lo, hi))) continue;

                var ca = RoomCenter(room, currentRoom, panelCenter);
                var cb = RoomCenter(neighbor, currentRoom, panelCenter);
                var delta = neighbor.GridPosition - room.GridPosition;

                Rect2 barRect;
                if (delta.X != 0)
                {
                    float barX = (ca.X + cb.X) / 2f - CellGap / 2f;
                    float barY = ca.Y - CellSize / 2f;
                    barRect = new Rect2(barX, barY, CellGap, CellSize);
                }
                else
                {
                    float barX = ca.X - CellSize / 2f;
                    float barY = (ca.Y + cb.Y) / 2f - CellGap / 2f;
                    barRect = new Rect2(barX, barY, CellSize, CellGap);
                }

                var barColor = door != null && door.IsLocked
                    ? new Color(0.9f, 0.2f, 0.2f)
                    : new Color(0.65f, 0.65f, 0.7f);
                DrawRect(barRect, barColor, filled: true);
            }
        }

        // Draw room squares on top
        foreach (var room in visited)
        {
            var center = RoomCenter(room, currentRoom, panelCenter);
            var topLeft = center - new Vector2(CellSize / 2f, CellSize / 2f);
            var rect = new Rect2(topLeft, new Vector2(CellSize, CellSize));

            Color fillColor;
            if (room.RoomId == currentRoomId)
                fillColor = Colors.White;
            else if (room.Archetype == RoomArchetype.SequenceShrine && !room.IsShrineUsed)
                fillColor = new Color(1f, 0.82f, 0.2f);
            else
                fillColor = new Color(0.65f, 0.65f, 0.7f);

            DrawRect(rect, fillColor, filled: true);
            DrawRect(rect, new Color(0f, 0f, 0f, 0.5f), filled: false);
        }
    }

    private Vector2 RoomCenter(RoomNode room, RoomNode currentRoom, Vector2 panelCenter)
    {
        var delta = room.GridPosition - currentRoom.GridPosition;
        return panelCenter + new Vector2(delta.X, delta.Y) * Step;
    }
}
