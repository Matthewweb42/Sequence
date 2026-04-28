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
    private InteractPrompt? _prompt;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite?.Play("new_animation");

        _prompt = new InteractPrompt();
        _prompt.PulseColor = new Color(0.2f, 0.9f, 0.3f, 1f); // green for heal
        AddChild(_prompt);
    }

    public override void _Process(double delta)
    {
        if (_isUsed) return;

        if (_currentUser == null || !GodotObject.IsInstanceValid(_currentUser))
        {
            _currentUser = null;
            return;
        }

        var interactPressed = Input.IsActionPressed("interact") || Input.IsKeyPressed(Key.W);
        var interactJustPressed = interactPressed && !_interactWasPressed;
        _interactWasPressed = interactPressed;

        if (!interactJustPressed) return;

        var health = _currentUser.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (health == null) return;

        health.Heal(health.MaxHp);
        EmitSignal(SignalName.HealUsed, _currentUser);
        _isUsed = true;
        _prompt?.Hide();
        Monitoring = false;
        QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!body.IsInGroup("player")) return;
        if (_currentUser == null)
        {
            _currentUser = body;
            _prompt?.Show();
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (_currentUser == body)
        {
            _currentUser = null;
            _prompt?.Hide();
        }
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area.GetParent() is not Node2D owner || !owner.IsInGroup("player")) return;
        if (_currentUser != null) return;
        _currentUser = owner;
        _prompt?.Show();
    }

    private void OnAreaExited(Area2D area)
    {
        if (area.GetParent() is Node2D owner && _currentUser == owner)
        {
            _currentUser = null;
            _prompt?.Hide();
        }
    }
}
