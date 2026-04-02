using Godot;
using System.Collections.Generic;

namespace Sequence.Components.Inventory;

/// <summary>
/// Stores player-owned materials, formulas, and artifacts.
/// </summary>
public partial class InventoryComponent : Node
{
	[Signal] public delegate void MaterialChangedEventHandler(string materialId, int newAmount);
	[Signal] public delegate void FormulaDiscoveredEventHandler(string formulaId);
	[Signal] public delegate void ArtifactAddedEventHandler(string artifactId);

	private readonly Dictionary<string, int> _materials = new();
	private readonly HashSet<string> _knownFormulas = new();
	private readonly HashSet<string> _artifacts = new();

	public int GetMaterialCount(string materialId)
	{
		return _materials.TryGetValue(materialId, out var amount) ? amount : 0;
	}

	public int AddMaterial(string materialId, int amount)
	{
		if (string.IsNullOrWhiteSpace(materialId) || amount <= 0)
		{
			return GetMaterialCount(materialId);
		}

		var next = GetMaterialCount(materialId) + amount;
		_materials[materialId] = next;
		EmitSignal(SignalName.MaterialChanged, materialId, next);
		return next;
	}

	public bool TryConsumeMaterial(string materialId, int amount)
	{
		if (string.IsNullOrWhiteSpace(materialId) || amount <= 0)
		{
			return false;
		}

		var current = GetMaterialCount(materialId);
		if (current < amount)
		{
			return false;
		}

		var next = current - amount;
		_materials[materialId] = next;
		EmitSignal(SignalName.MaterialChanged, materialId, next);
		return true;
	}

	public bool DiscoverFormula(string formulaId)
	{
		if (string.IsNullOrWhiteSpace(formulaId))
		{
			return false;
		}

		if (_knownFormulas.Add(formulaId))
		{
			EmitSignal(SignalName.FormulaDiscovered, formulaId);
			return true;
		}

		return false;
	}

	public bool AddArtifact(string artifactId)
	{
		if (string.IsNullOrWhiteSpace(artifactId))
		{
			return false;
		}

		if (_artifacts.Add(artifactId))
		{
			EmitSignal(SignalName.ArtifactAdded, artifactId);
			return true;
		}

		return false;
	}
}

