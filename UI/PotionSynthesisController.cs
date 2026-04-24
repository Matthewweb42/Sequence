using Godot;
using System.Collections.Generic;
using Sequence.Components.Inventory;
using Sequence.Components.Sequence;
using Sequence.Entities.Interactables;
using Sequence.Resources;

namespace Sequence.UI;

/// <summary>
/// Hold-to-craft Potion Synthesis UI.
/// Opened by SequenceShrine; shows formula requirements and a 2.5s hold-confirm button.
/// </summary>
public partial class PotionSynthesisController : Control
{
    private const float HoldDuration = 2.5f;

    [Export] public NodePath? FormulaTitlePath { get; set; }
    [Export] public NodePath? IngredientsContainerPath { get; set; }
    [Export] public NodePath? HoldProgressPath { get; set; }
    [Export] public NodePath? StatusLabelPath { get; set; }
    [Export] public NodePath? CancelButtonPath { get; set; }

    private Label? _formulaTitle;
    private VBoxContainer? _ingredientsContainer;
    private ProgressBar? _holdProgress;
    private Label? _statusLabel;
    private Button? _cancelButton;

    private string _formulaId = string.Empty;
    private InventoryComponent? _inventory;
    private SequenceComponent? _sequence;
    private SequenceShrine? _shrine;
    private Node? _user;
    private FormulaResource? _formula;
    private float _holdElapsed;
    private bool _isHolding;
    private bool _allMet;

    public override void _Ready()
    {
        _formulaTitle = GetNodeOrNullByPath<Label>(FormulaTitlePath, "FormulaTitle");
        _ingredientsContainer = GetNodeOrNullByPath<VBoxContainer>(IngredientsContainerPath, "IngredientsContainer");
        _holdProgress = GetNodeOrNullByPath<ProgressBar>(HoldProgressPath, "HoldProgress");
        _statusLabel = GetNodeOrNullByPath<Label>(StatusLabelPath, "StatusLabel");
        _cancelButton = GetNodeOrNullByPath<Button>(CancelButtonPath, "CancelButton");

        if (_cancelButton != null)
            _cancelButton.Pressed += Close;

        Hide();
    }

    public void Open(string formulaId, InventoryComponent inventory, SequenceComponent sequence,
        SequenceShrine shrine, Node user)
    {
        _formulaId = formulaId;
        _inventory = inventory;
        _sequence = sequence;
        _shrine = shrine;
        _user = user;
        _holdElapsed = 0f;
        _isHolding = false;

        _formula = LoadFormula(formulaId);

        RefreshUI();
        Show();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _formula == null || _inventory == null) return;

        RefreshIngredientState();

        if (_allMet)
        {
            var holding = Input.IsActionPressed("interact") || Input.IsKeyPressed(Key.E);

            if (holding)
            {
                _holdElapsed += (float)delta;
                if (_holdProgress != null) _holdProgress.Value = _holdElapsed / HoldDuration * 100.0;

                if (_holdElapsed >= HoldDuration)
                {
                    ExecuteSynthesis();
                }
            }
            else
            {
                _holdElapsed = 0f;
                if (_holdProgress != null) _holdProgress.Value = 0;
            }

            if (_statusLabel != null)
                _statusLabel.Text = holding ? "Hold to craft..." : "Hold [E] to brew";
        }
        else
        {
            _holdElapsed = 0f;
            if (_holdProgress != null) _holdProgress.Value = 0;
        }
    }

    private void ExecuteSynthesis()
    {
        if (_formula == null || _inventory == null || _sequence == null) return;

        // Consume all materials
        foreach (var kv in _formula.RequiredMaterials)
        {
            _inventory.TryConsumeMaterial(kv.Key, kv.Value);
        }

        _sequence.TryAdvance();

        if (_shrine != null && _user != null)
            _shrine.OnSynthesisCompleted(_user, _sequence.CurrentSequence);

        Close();
    }

    private void RefreshUI()
    {
        if (_formula == null)
        {
            if (_formulaTitle != null) _formulaTitle.Text = "Formula not found";
            if (_statusLabel != null) _statusLabel.Text = "You don't know this formula yet.";
            return;
        }

        if (_formulaTitle != null) _formulaTitle.Text = _formula.DisplayName;
        RefreshIngredientState();
    }

    private void RefreshIngredientState()
    {
        if (_formula == null || _inventory == null || _ingredientsContainer == null) return;

        // Clear old rows
        foreach (Node child in _ingredientsContainer.GetChildren())
            child.QueueFree();

        _allMet = true;

        foreach (var kv in _formula.RequiredMaterials)
        {
            var have = _inventory.GetMaterialCount(kv.Key);
            var need = kv.Value;
            var met = have >= need;
            if (!met) _allMet = false;

            var row = new HBoxContainer();
            var label = new Label
            {
                Text = $"{kv.Key}: {have} / {need}",
                Modulate = met ? Colors.Green : Colors.Red
            };
            row.AddChild(label);

            if (!met)
            {
                var hint = new Label
                {
                    Text = $"  (Need {need - have} more)",
                    Modulate = Colors.Red
                };
                row.AddChild(hint);
            }

            _ingredientsContainer.AddChild(row);
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = _allMet ? "Hold [E] to brew" : "Not enough materials";
        }

        if (_holdProgress != null)
        {
            _holdProgress.Modulate = _allMet ? Colors.White : new Color(0.5f, 0.5f, 0.5f);
        }
    }

    private void Close()
    {
        _holdElapsed = 0f;
        Hide();
    }

    private static FormulaResource? LoadFormula(string formulaId)
    {
        var path = $"res://Resources/Formulas/{formulaId}.tres";
        return GD.Load<FormulaResource>(path);
    }

    private T? GetNodeOrNullByPath<T>(NodePath? path, string fallback) where T : Node
    {
        if (path != null && !path.IsEmpty) return GetNodeOrNull<T>(path);
        return GetNodeOrNull<T>(fallback);
    }
}
