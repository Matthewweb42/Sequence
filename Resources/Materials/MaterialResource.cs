using Godot;

namespace Sequence.Resources;

public enum MaterialRarity { Common, Uncommon, Rare }

[GlobalClass]
public partial class MaterialResource : Resource
{
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public MaterialRarity Rarity { get; set; } = MaterialRarity.Common;
}
