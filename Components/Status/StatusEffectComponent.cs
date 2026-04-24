using Godot;
using System.Collections.Generic;

namespace Sequence.Components.Status;

public enum ModifierOp { Additive, Multiplicative }

public readonly record struct StatModifier(string StatKey, float Value, ModifierOp Op);

/// <summary>
/// Tracks active timed and permanent status effects (buffs and debuffs) on an entity.
/// Consumers call GetStat() to read modified values rather than having this component
/// mutate sibling components directly.
/// </summary>
public partial class StatusEffectComponent : Node
{
    [Signal] public delegate void EffectAppliedEventHandler(string effectId);
    [Signal] public delegate void EffectRemovedEventHandler(string effectId);

    private sealed class ActiveEffect
    {
        public string Id { get; }
        public float RemainingSeconds { get; set; }
        public bool IsPermanent { get; }
        public StatModifier[] Modifiers { get; }
        public string[] Tags { get; }

        public ActiveEffect(string id, float durationSeconds, StatModifier[] modifiers, string[] tags)
        {
            Id = id;
            IsPermanent = durationSeconds <= 0f;
            RemainingSeconds = durationSeconds;
            Modifiers = modifiers;
            Tags = tags;
        }
    }

    private readonly Dictionary<string, ActiveEffect> _active = new(StringComparer.Ordinal);

    public override void _Process(double delta)
    {
        var toRemove = new List<string>();

        foreach (var kvp in _active)
        {
            if (kvp.Value.IsPermanent) continue;

            kvp.Value.RemainingSeconds -= (float)delta;
            if (kvp.Value.RemainingSeconds <= 0f)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var id in toRemove)
        {
            _active.Remove(id);
            EmitSignal(SignalName.EffectRemoved, id);
        }
    }

    /// <param name="durationSeconds">Pass 0 or negative for a permanent effect.</param>
    public void ApplyEffect(string effectId, float durationSeconds, StatModifier[]? modifiers = null, string[]? tags = null)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return;

        var effect = new ActiveEffect(effectId, durationSeconds, modifiers ?? System.Array.Empty<StatModifier>(), tags ?? System.Array.Empty<string>());
        _active[effectId] = effect;
        EmitSignal(SignalName.EffectApplied, effectId);
    }

    public void RemoveEffect(string effectId)
    {
        if (_active.Remove(effectId))
        {
            EmitSignal(SignalName.EffectRemoved, effectId);
        }
    }

    public void RemoveAllEffects()
    {
        var ids = new List<string>(_active.Keys);
        _active.Clear();
        foreach (var id in ids)
        {
            EmitSignal(SignalName.EffectRemoved, id);
        }
    }

    public void RemoveByTag(string tag)
    {
        var toRemove = new List<string>();
        foreach (var kvp in _active)
        {
            foreach (var t in kvp.Value.Tags)
            {
                if (t == tag) { toRemove.Add(kvp.Key); break; }
            }
        }
        foreach (var id in toRemove) RemoveEffect(id);
    }

    public bool HasEffect(string effectId) => _active.ContainsKey(effectId);

    public bool HasTag(string tag)
    {
        foreach (var effect in _active.Values)
        {
            foreach (var t in effect.Tags)
            {
                if (t == tag) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns baseValue with all active modifiers for the given stat key applied.
    /// Multiplicative modifiers are applied first, then additive.
    /// </summary>
    public float GetStat(string statKey, float baseValue)
    {
        var multiplier = 1f;
        var additive = 0f;

        foreach (var effect in _active.Values)
        {
            foreach (var mod in effect.Modifiers)
            {
                if (mod.StatKey != statKey) continue;
                if (mod.Op == ModifierOp.Multiplicative) multiplier *= mod.Value;
                else additive += mod.Value;
            }
        }

        return baseValue * multiplier + additive;
    }
}
