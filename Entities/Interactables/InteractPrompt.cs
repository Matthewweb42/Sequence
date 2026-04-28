using Godot;

namespace Sequence.Entities.Interactables;

/// <summary>
/// Floating "E" prompt that bobs above an interactable and pulses with a tint color.
/// Add as a child of the interactable; call Show/Hide to control visibility.
/// </summary>
public partial class InteractPrompt : Node2D
{
    // How far above the parent origin the label floats.
    [Export] public float OffsetY { get; set; } = -48f;
    // Peak-to-peak bob distance in pixels.
    [Export] public float BobAmplitude { get; set; } = 5f;
    [Export] public float BobSpeed { get; set; } = 2.5f;
    // Pulse color overlaid on the panel while visible (alpha drives intensity).
    [Export] public Color PulseColor { get; set; } = new Color(1f, 0.9f, 0.2f, 1f);

    private Label? _label;
    private PanelContainer? _panel;
    private StyleBoxFlat? _style;
    private float _time;
    private bool _visible;

    public override void _Ready()
    {
        // Build the prompt entirely in code so no .tscn changes are required.
        _panel = new PanelContainer();
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        _style = new StyleBoxFlat();
        _style.BgColor = new Color(0f, 0f, 0f, 0.6f);
        _style.CornerRadiusTopLeft = 5;
        _style.CornerRadiusTopRight = 5;
        _style.CornerRadiusBottomLeft = 5;
        _style.CornerRadiusBottomRight = 5;
        _style.ContentMarginLeft = 8f;
        _style.ContentMarginRight = 8f;
        _style.ContentMarginTop = 4f;
        _style.ContentMarginBottom = 4f;
        _panel.AddThemeStyleboxOverride("panel", _style);

        _label = new Label();
        _label.Text = "[W]";
        _label.AddThemeFontSizeOverride("font_size", 14);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.HorizontalAlignment = HorizontalAlignment.Center;

        _panel.AddChild(_label);
        AddChild(_panel);

        // Centre the panel on this node's origin.
        _panel.Position = new Vector2(-28f, OffsetY);

        Hide();
    }

    public override void _Process(double delta)
    {
        if (!_visible) return;

        _time += (float)delta;

        // Bob up and down.
        var bob = Mathf.Sin(_time * BobSpeed) * BobAmplitude;
        if (_panel != null)
            _panel.Position = new Vector2(-28f, OffsetY + bob);

        // Pulse the background color.
        if (_style != null)
        {
            var pulse = (Mathf.Sin(_time * BobSpeed * 1.5f) + 1f) * 0.5f; // 0..1
            var base_alpha = 0.55f + pulse * 0.25f;
            _style.BgColor = PulseColor.Lerp(new Color(0f, 0f, 0f, 0.6f), 0.5f - pulse * 0.3f);
            _style.BgColor = new Color(
                Mathf.Lerp(0f, PulseColor.R, pulse * 0.5f),
                Mathf.Lerp(0f, PulseColor.G, pulse * 0.5f),
                Mathf.Lerp(0f, PulseColor.B, pulse * 0.5f),
                base_alpha);
        }
    }

    public new void Show()
    {
        _visible = true;
        _panel?.Show();
    }

    public new void Hide()
    {
        _visible = false;
        _panel?.Hide();
    }
}
