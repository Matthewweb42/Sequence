using Godot;
using System.Collections.Generic;

namespace Sequence.Utilities;

public static class LootTable
{
    public static T? Roll<T>(Dictionary<T, float> weightTable, RandomNumberGenerator rng) where T : notnull
    {
        if (weightTable == null || weightTable.Count == 0)
            return default;

        var total = 0f;
        foreach (var weight in weightTable.Values)
            total += weight;

        var roll = rng.Randf() * total;
        var cumulative = 0f;

        foreach (var (item, weight) in weightTable)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return item;
        }

        // Fallback: return last entry (handles floating-point edge cases)
        T? last = default;
        foreach (var key in weightTable.Keys)
            last = key;
        return last;
    }
}
