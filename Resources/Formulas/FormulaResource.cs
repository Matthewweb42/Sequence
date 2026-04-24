using Godot;

namespace Sequence.Resources;

[GlobalClass]
public partial class FormulaResource : Resource
{
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public int TargetSequence { get; set; } = 8;

    /// <summary>Keys are material IDs, values are required quantities.</summary>
    [Export] public Godot.Collections.Dictionary<string, int> RequiredMaterials { get; set; } = new();
}
