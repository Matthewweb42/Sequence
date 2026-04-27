using Godot;
using Sequence.Components.Health;

namespace Sequence.Entities.Interactables;

public partial class HealInteractable : Area2D
{
    [Signal] public delegate void HealUsedEventHandler(Node user);

    private Node2D? _currentUser;
    private bool _interactWasPressed;
    private bool _isUsed;
    private AnimatedSprite2D? _sprite;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite?.Play("new_animation");
    }

    public override void _Process(double delta)
    {
        if (_isUsed) return;

        if (_currentUser == null || !GodotObject.IsInstanceValid(_currentUser))
        {
            _currentUser = null;
            return;
        }

        var interactPressed = Input.IsActionPressed("interact") || Input.IsKeyPressed(Key.E);
        var interactJustPressed = interactPressed && !_interactWasPressed;
        _interactWasPressed = interactPressed;

        if (!interactJustPressed) return;

        var health = _currentUser.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (health == null) return;

        health.Heal(health.MaxHp);
        EmitSignal(SignalName.HealUsed, _currentUser);
        _isUsed = true;
        Monitoring = false;
        QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_currentUser == null)
            _currentUser = body;
    }

    private void OnBodyExited(Node2D body)
    {
        if (_currentUser == body)
            _currentUser = null;
    }

    private void OnAreaEntered(Area2D area)
    {
        if (_currentUser != null) return;
        if (area.GetParent() is Node2D owner)
            _currentUser = owner;
    }

    private void OnAreaExited(Area2D area)
    {
        if (area.GetParent() is Node2D owner && _currentUser == owner)
            _currentUser = null;
    }
}
