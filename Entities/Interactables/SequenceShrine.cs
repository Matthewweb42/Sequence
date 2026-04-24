using Godot;
using Sequence.Autoloads;
using Sequence.Components.Inventory;
using Sequence.Components.Sequence;

namespace Sequence.Entities.Interactables;

/// <summary>
/// Interactable shrine placed in Sequence Shrine rooms.
/// When the player presses interact while nearby, opens PotionSynthesisUI for the
/// current sequence advancement formula. Advancement happens through the UI's hold-to-craft flow.
/// </summary>
public partial class SequenceShrine : Area2D
{
    [Signal] public delegate void ShrineUsedEventHandler(Node user, int newSequence);

    [Export] public bool SingleUse { get; set; } = true;
    [Export] public NodePath? SequencePath { get; set; }

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
        if (_isConsumed || _currentUser == null || !GodotObject.IsInstanceValid(_currentUser))
        {
            _currentUser = null;
            return;
        }

        var interactPressed = Input.IsActionPressed("interact") || Input.IsKeyPressed(Key.E);
        var interactJustPressed = interactPressed && !_interactWasPressed;
        _interactWasPressed = interactPressed;

        if (!interactJustPressed) return;

        OpenSynthesisUI(_currentUser);
    }

    public void OnSynthesisCompleted(Node user, int newSequence)
    {
        EmitSignal(SignalName.ShrineUsed, user, newSequence);

        var sequence = ResolveSequence(user);
        if (sequence != null && sequence.CurrentSequence <= sequence.FinalSequence)
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

    private void OpenSynthesisUI(Node2D user)
    {
        var sequence = ResolveSequence(user);
        if (sequence == null) return;

        var inventory = user.GetNodeOrNull<InventoryComponent>("InventoryComponent");
        if (inventory == null) return;

        var targetSequence = sequence.CurrentSequence - 1;
        if (targetSequence < sequence.FinalSequence) return;

        var formulaId = $"formula_s{targetSequence}";

        var ui = GetTree()?.CurrentScene?.FindChild("PotionSynthesisUI", recursive: true, owned: false)
                 as UI.PotionSynthesisController;

        ui?.Open(formulaId, inventory, sequence, this, user);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_currentUser == null) _currentUser = body;
    }

    private void OnBodyExited(Node2D body)
    {
        if (_currentUser == body) _currentUser = null;
    }

    private void OnAreaEntered(Area2D area)
    {
        if (_currentUser != null) return;
        if (area.GetParent() is Node2D owner) _currentUser = owner;
    }

    private void OnAreaExited(Area2D area)
    {
        if (area.GetParent() is Node2D owner && _currentUser == owner) _currentUser = null;
    }

    private SequenceComponent? ResolveSequence(Node user)
    {
        if (SequencePath != null && !SequencePath.IsEmpty)
            return GetNodeOrNull<SequenceComponent>(SequencePath);

        return user.GetNodeOrNull<SequenceComponent>("SequenceComponent");
    }
}
