using Godot;
using Sequence.Components.Hurtbox;
using Sequence.Components.Sanity;

namespace Sequence.Components.Ability;

/// <summary>
/// Manages hold-button channeling abilities. Drains sanity over time and breaks
/// the channel if the entity takes damage or runs out of sanity.
/// </summary>
public partial class ChannelComponent : Node
{
    [Signal] public delegate void ChannelStartedEventHandler(string channelId);
    [Signal] public delegate void ChannelPulseEventHandler(string channelId, float elapsed);
    [Signal] public delegate void ChannelEndedEventHandler(string channelId, string reason);

    [Export(PropertyHint.Range, "0,1000,0.1")] public float SanityDrainPerSecond { get; set; } = 4f;
    [Export(PropertyHint.Range, "0.05,5,0.05")] public float PulseIntervalSeconds { get; set; } = 0.25f;
    [Export] public NodePath? SanityPath { get; set; }
    [Export] public NodePath? HurtboxPath { get; set; }

    public bool IsChanneling { get; private set; }
    public string ActiveChannelId { get; private set; } = string.Empty;

    private SanityComponent? _sanity;
    private float _elapsed;
    private float _nextPulse;

    public override void _Ready()
    {
        _sanity = SanityPath != null && !SanityPath.IsEmpty
            ? GetNodeOrNull<SanityComponent>(SanityPath)
            : GetParent()?.GetNodeOrNull<SanityComponent>("SanityComponent");

        var hurtbox = HurtboxPath != null && !HurtboxPath.IsEmpty
            ? GetNodeOrNull<HurtboxComponent>(HurtboxPath)
            : GetParent()?.GetNodeOrNull<HurtboxComponent>("HurtboxComponent");

        if (hurtbox != null)
        {
            hurtbox.HitAccepted += OnHitAccepted;
        }
    }

    public override void _Process(double delta)
    {
        if (!IsChanneling) return;

        var dt = (float)delta;
        _elapsed += dt;

        if (SanityDrainPerSecond > 0f && _sanity != null)
        {
            if (!_sanity.TrySpend(SanityDrainPerSecond * dt))
            {
                EndChannel("sanity_depleted");
                return;
            }
        }

        _nextPulse -= dt;
        if (_nextPulse <= 0f)
        {
            _nextPulse += PulseIntervalSeconds;
            EmitSignal(SignalName.ChannelPulse, ActiveChannelId, _elapsed);
        }
    }

    public bool TryBeginChannel(string channelId)
    {
        if (IsChanneling) return false;
        if (string.IsNullOrWhiteSpace(channelId)) return false;

        IsChanneling = true;
        ActiveChannelId = channelId;
        _elapsed = 0f;
        _nextPulse = PulseIntervalSeconds;
        EmitSignal(SignalName.ChannelStarted, channelId);
        return true;
    }

    public void EndChannel(string reason = "released")
    {
        if (!IsChanneling) return;

        var id = ActiveChannelId;
        IsChanneling = false;
        ActiveChannelId = string.Empty;
        EmitSignal(SignalName.ChannelEnded, id, reason);
    }

    private void OnHitAccepted(Node attacker, float damage)
    {
        if (IsChanneling)
        {
            EndChannel("damaged");
        }
    }
}
