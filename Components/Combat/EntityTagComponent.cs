using Godot;
using System.Collections.Generic;

namespace Sequence.Components.Combat;

/// <summary>
/// Stores logical type tags on an entity (e.g. "undead", "evil_spirit", "boss").
/// Used by abilities that deal bonus damage or have special effects against tagged targets.
/// </summary>
public partial class EntityTagComponent : Node
{
    [Export] public string[] Tags { get; set; } = System.Array.Empty<string>();

    private readonly HashSet<string> _tagSet = new(StringComparer.OrdinalIgnoreCase);

    public override void _Ready()
    {
        foreach (var tag in Tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                _tagSet.Add(tag);
        }
    }

    public bool HasTag(string tag) => _tagSet.Contains(tag);
    public void AddTag(string tag) => _tagSet.Add(tag);
    public void RemoveTag(string tag) => _tagSet.Remove(tag);
}
