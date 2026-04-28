using Godot;
using Sequence.Autoloads;
using Sequence.Components.Ability;
using System.Collections.Generic;

namespace Sequence.UI.HUD;

/// <summary>
/// Displays a row of ability slots in the bottom-left corner.
/// Each slot shows the key label, lights up when pressed, and dims during cooldown.
/// Hovering a slot shows a tooltip with the ability description and effects.
/// </summary>
public partial class AbilityBarController : Control
{
    private record TooltipData(string Title, string Description, string[] Bullets);

    // Ordered list of (abilityId, inputAction, displayKey).
    // Add or reorder entries here to change what appears in the bar.
    private static readonly (string AbilityId, string Action, string Key)[] SlotDefs = new[]
    {
        ("bardic_song",              "ability_1", "Q"),
        ("sunshine_burst",           "ability_3", "T"),
        ("theurgical_purification",  "ability_5", "F"),
        ("truth_seal",               "ability_8", "X"),
        ("pure_white_ray",           "ability_7", "Z"),
    };

    private static readonly Dictionary<string, TooltipData> Tooltips = new()
    {
        ["bardic_song"] = new TooltipData(
            "Bardic Song [Q]",
            "Hold Q to charge a musical aura; release to unleash it as a buff lasting as long as you charged.",
            new[]
            {
                "+15% movement speed (or +22.5% with Notary)",
                "+10% attack speed (or +15% with Notary)",
                "Duration scales 1:1 with charge time (max 8s)",
                "Drains sanity while charging",
            }),
        ["sunshine_burst"] = new TooltipData(
            "Sunshine Burst [T]",
            "Fire a radiant solar projectile in the direction you're facing, damaging enemies it hits.",
            new[]
            {
                "Deals light damage to the first enemy hit",
                "Activates Sun Eyes aura for 30 seconds",
                "8s cooldown / 15 sanity cost",
            }),
        ["theurgical_purification"] = new TooltipData(
            "Theurgical Purification [F]",
            "Unleash a burst of holy energy: cleanse your debuffs and damage nearby enemies with a sacred ring.",
            new[]
            {
                "Removes all debuffs from yourself",
                "Deals 30 holy damage in a ring around you",
                "Instantly destroys undead enemies below 30% HP",
                "With Notary: 5s debuff immunity after cast",
                "20s cooldown / 25 sanity cost",
            }),
        ["truth_seal"] = new TooltipData(
            "Truth Seal [X]",
            "Brand the nearest enemy with an unbreakable seal, holding them in place.",
            new[]
            {
                "Stuns the nearest enemy for 15 seconds",
                "Does not affect bosses",
                "30s cooldown / 10 sanity cost",
            }),
        ["pure_white_ray"] = new TooltipData(
            "Pure White Ray [Z]",
            "Channel pure divine light into a blazing beam that pierces through enemies.",
            new[]
            {
                "Fires a large high-damage projectile",
                "Pierces through multiple enemies",
                "10s cooldown / 12 sanity cost",
            }),
    };

    private static readonly Color ColorIdle      = new(0.15f, 0.15f, 0.20f, 0.85f);
    private static readonly Color ColorActive    = new(1.0f,  0.85f, 0.25f, 1.00f);
    private static readonly Color ColorCooldown  = new(0.08f, 0.08f, 0.12f, 0.85f);
    private static readonly Color ColorLocked    = new(0.05f, 0.05f, 0.05f, 0.50f);

    private AbilityComponent? _ability;
    private bool _resolved;
    private readonly Dictionary<PanelContainer, StyleBoxFlat> _styleCache = new();

    // Tooltip popup (shared, shown above hovered slot).
    private PanelContainer? _tooltipPanel;
    private StyleBoxFlat? _tooltipStyle;
    private Label? _tooltipTitle;
    private Label? _tooltipDesc;
    private VBoxContainer? _tooltipBullets;

    // Runtime slot state.
    private readonly List<SlotWidgets> _slots = new();

    private sealed class SlotWidgets
    {
        public required PanelContainer Panel;
        public required Label KeyLabel;
        public required ColorRect CooldownOverlay;
        public required string AbilityId;
        public required string Action;
        public float CooldownTotal;
    }

    public override void _Ready()
    {
        // Anchor bottom-left.
        AnchorLeft   = 0f;
        AnchorTop    = 1f;
        AnchorRight  = 0f;
        AnchorBottom = 1f;

        BuildSlots();
        BuildTooltip();
    }

    private void BuildSlots()
    {
        // HBox holds all slots side by side.
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);
        AddChild(hbox);

        const int slotSize = 48;

        foreach (var (abilityId, action, key) in SlotDefs)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(slotSize, slotSize);
            panel.MouseFilter = Control.MouseFilterEnum.Stop;

            // Key label centred in the slot.
            var label = new Label();
            label.Text = key;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AddThemeColorOverride("font_color", Colors.White);
            label.AddThemeFontSizeOverride("font_size", 16);
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.MouseFilter = Control.MouseFilterEnum.Ignore;

            // Cooldown dim overlay (drawn on top of label via sibling order).
            var overlay = new ColorRect();
            overlay.Color = new Color(0f, 0f, 0f, 0f);
            overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlay.MouseFilter = Control.MouseFilterEnum.Ignore;

            // Stack label + overlay inside the panel via a plain Control container.
            var stack = new Control();
            stack.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            stack.MouseFilter = Control.MouseFilterEnum.Ignore;
            stack.AddChild(label);
            stack.AddChild(overlay);

            panel.AddChild(stack);
            hbox.AddChild(panel);

            var capturedId = abilityId;
            var capturedPanel = panel;
            panel.MouseEntered += () => ShowTooltip(capturedId, capturedPanel);
            panel.MouseExited += HideTooltip;

            _slots.Add(new SlotWidgets
            {
                Panel          = panel,
                KeyLabel       = label,
                CooldownOverlay = overlay,
                AbilityId      = abilityId,
                Action         = action,
                CooldownTotal  = 0f,
            });
        }

        // Position the HBox so its bottom edge sits 12px above the screen bottom.
        hbox.Position = new Vector2(12f, -(SlotDefs.Length * 0 + slotSize + 12f));
        // Simpler: just use offset from the bottom anchor.
        OffsetLeft   = 12f;
        OffsetBottom = -12f;
        OffsetTop    = -(slotSize + 12f);
        OffsetRight  = 12f + SlotDefs.Length * (slotSize + 6);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = new PanelContainer();
        _tooltipPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

        _tooltipStyle = new StyleBoxFlat();
        _tooltipStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.93f);
        _tooltipStyle.CornerRadiusTopLeft = 6;
        _tooltipStyle.CornerRadiusTopRight = 6;
        _tooltipStyle.CornerRadiusBottomLeft = 6;
        _tooltipStyle.CornerRadiusBottomRight = 6;
        _tooltipStyle.ContentMarginLeft = 12f;
        _tooltipStyle.ContentMarginRight = 12f;
        _tooltipStyle.ContentMarginTop = 10f;
        _tooltipStyle.ContentMarginBottom = 10f;
        _tooltipPanel.AddThemeStyleboxOverride("panel", _tooltipStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        _tooltipTitle = new Label();
        _tooltipTitle.AddThemeFontSizeOverride("font_size", 15);
        _tooltipTitle.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.35f, 1f));
        _tooltipTitle.MouseFilter = Control.MouseFilterEnum.Ignore;

        _tooltipDesc = new Label();
        _tooltipDesc.AddThemeFontSizeOverride("font_size", 13);
        _tooltipDesc.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 1f));
        _tooltipDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tooltipDesc.CustomMinimumSize = new Vector2(280f, 0f);
        _tooltipDesc.MouseFilter = Control.MouseFilterEnum.Ignore;

        var separator = new HSeparator();
        separator.AddThemeColorOverride("color", new Color(0.4f, 0.4f, 0.5f, 0.6f));
        separator.MouseFilter = Control.MouseFilterEnum.Ignore;

        _tooltipBullets = new VBoxContainer();
        _tooltipBullets.AddThemeConstantOverride("separation", 3);
        _tooltipBullets.MouseFilter = Control.MouseFilterEnum.Ignore;

        vbox.AddChild(_tooltipTitle);
        vbox.AddChild(_tooltipDesc);
        vbox.AddChild(separator);
        vbox.AddChild(_tooltipBullets);

        _tooltipPanel.AddChild(vbox);
        AddChild(_tooltipPanel);

        _tooltipPanel.Hide();
    }

    private void ShowTooltip(string abilityId, Control anchorPanel)
    {
        if (_tooltipPanel == null || _tooltipTitle == null || _tooltipDesc == null || _tooltipBullets == null)
            return;
        if (!Tooltips.TryGetValue(abilityId, out var data))
            return;

        _tooltipTitle.Text = data.Title;
        _tooltipDesc.Text = data.Description;

        // Rebuild bullet list.
        foreach (Node child in _tooltipBullets.GetChildren())
            child.QueueFree();

        foreach (var bullet in data.Bullets)
        {
            var bl = new Label();
            bl.Text = "• " + bullet;
            bl.AddThemeFontSizeOverride("font_size", 12);
            bl.AddThemeColorOverride("font_color", new Color(0.75f, 0.9f, 0.75f, 1f));
            bl.MouseFilter = Control.MouseFilterEnum.Ignore;
            _tooltipBullets.AddChild(bl);
        }

        // Position tooltip above the hovered slot.
        // Force a size recalc before placing.
        _tooltipPanel.Show();
        _tooltipPanel.ResetSize();

        var slotGlobal = anchorPanel.GlobalPosition;
        var tooltipSize = _tooltipPanel.Size;
        var tooltipX = slotGlobal.X;
        var tooltipY = slotGlobal.Y - tooltipSize.Y - 8f;

        // Clamp to screen so it doesn't clip above viewport.
        var viewport = GetViewportRect();
        tooltipX = Mathf.Clamp(tooltipX, 4f, viewport.Size.X - tooltipSize.X - 4f);
        tooltipY = Mathf.Max(tooltipY, 4f);

        _tooltipPanel.GlobalPosition = new Vector2(tooltipX, tooltipY);
    }

    private void HideTooltip()
    {
        _tooltipPanel?.Hide();
    }

    private void TryResolve()
    {
        if (_resolved) return;

        Node? player = RunManager.Instance?.Player
            ?? GetTree()?.CurrentScene?.FindChild("Player", recursive: true, owned: false);

        if (player == null) return;

        _ability = player.GetNodeOrNull<AbilityComponent>("AbilityComponent");
        if (_ability == null) return;

        // Listen for cooldown starts so we know the total duration for the overlay.
        _ability.AbilityCooldownStarted += OnCooldownStarted;
        _resolved = true;
    }

    public override void _ExitTree()
    {
        if (_ability != null)
            _ability.AbilityCooldownStarted -= OnCooldownStarted;
    }

    private void OnCooldownStarted(string abilityId, float cooldownSeconds)
    {
        foreach (var slot in _slots)
        {
            if (slot.AbilityId == abilityId)
            {
                slot.CooldownTotal = cooldownSeconds;
                break;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!_resolved) TryResolve();
        if (_ability == null) return;

        foreach (var slot in _slots)
        {
            var unlocked = _ability.IsUnlocked(slot.AbilityId);
            var pressed  = Input.IsActionPressed(slot.Action);
            var remaining = _ability.GetCooldownRemaining(slot.AbilityId);
            var onCooldown = remaining > 0f;

            // Panel background colour.
            Color bg;
            if (!unlocked)
                bg = ColorLocked;
            else if (pressed && !onCooldown)
                bg = ColorActive;
            else if (onCooldown)
                bg = ColorCooldown;
            else
                bg = ColorIdle;

            ApplyPanelColor(slot.Panel, bg);

            // Cooldown overlay: semi-transparent black that shrinks from top.
            if (onCooldown && slot.CooldownTotal > 0f)
            {
                var frac = remaining / slot.CooldownTotal;
                slot.CooldownOverlay.Color = new Color(0f, 0f, 0f, 0.55f * frac);
            }
            else
            {
                slot.CooldownOverlay.Color = new Color(0f, 0f, 0f, 0f);
            }

            // Dim the key label text when locked.
            slot.KeyLabel.AddThemeColorOverride("font_color",
                unlocked ? Colors.White : new Color(0.4f, 0.4f, 0.4f, 0.5f));
        }
    }

    private void ApplyPanelColor(PanelContainer panel, Color color)
    {
        if (!_styleCache.TryGetValue(panel, out var style))
        {
            style = new StyleBoxFlat();
            style.CornerRadiusTopLeft     = 4;
            style.CornerRadiusTopRight    = 4;
            style.CornerRadiusBottomLeft  = 4;
            style.CornerRadiusBottomRight = 4;
            _styleCache[panel] = style;
            panel.AddThemeStyleboxOverride("panel", style);
        }
        style.BgColor = color;
    }
}
