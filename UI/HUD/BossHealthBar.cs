using Godot;
using Sequence.Components.Health;
using Sequence.Entities.Enemies.Boss;

namespace Sequence.UI.HUD;

/// <summary>
/// Top-center HUD overlay that appears when a boss is alive.
/// Scans for a BossPhaseComponent in the enemies group each frame until found,
/// then tracks HP and phase in real time.
/// </summary>
public partial class BossHealthBar : Control
{
    private ProgressBar? _bar;
    private Label? _nameLabel;
    private Label? _phaseLabel;
    private StyleBoxFlat? _barFill;

    private HealthComponent? _bossHealth;
    private BossPhaseComponent? _bossPhase;
    private bool _resolved;

    private static readonly Color ColorPhase1 = new(0.75f, 0.15f, 0.15f, 1f);
    private static readonly Color ColorPhase2 = new(0.85f, 0.45f, 0.05f, 1f);
    private static readonly Color ColorPhase3 = new(1.00f, 0.10f, 0.10f, 1f);

    public override void _Ready()
    {
        AnchorLeft   = 0.5f;
        AnchorTop    = 0f;
        AnchorRight  = 0.5f;
        AnchorBottom = 0f;

        BuildUI();
        Hide();
    }

    private void BuildUI()
    {
        var panel = new PanelContainer();
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.04f, 0.04f, 0.08f, 0.88f);
        panelStyle.CornerRadiusBottomLeft  = 6;
        panelStyle.CornerRadiusBottomRight = 6;
        panelStyle.ContentMarginLeft   = 14f;
        panelStyle.ContentMarginRight  = 14f;
        panelStyle.ContentMarginTop    = 8f;
        panelStyle.ContentMarginBottom = 8f;
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        _nameLabel = new Label();
        _nameLabel.Text = "THE SEQUENCE WARDEN";
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.AddThemeFontSizeOverride("font_size", 14);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.25f, 1f));
        _nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

        _bar = new ProgressBar();
        _bar.CustomMinimumSize = new Vector2(300f, 18f);
        _bar.MinValue = 0.0;
        _bar.MaxValue = 1.0;
        _bar.Value = 1.0;
        _bar.ShowPercentage = false;
        _bar.MouseFilter = Control.MouseFilterEnum.Ignore;

        _barFill = new StyleBoxFlat();
        _barFill.BgColor = ColorPhase1;
        _barFill.CornerRadiusTopLeft     = 3;
        _barFill.CornerRadiusTopRight    = 3;
        _barFill.CornerRadiusBottomLeft  = 3;
        _barFill.CornerRadiusBottomRight = 3;
        _bar.AddThemeStyleboxOverride("fill", _barFill);

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        bgStyle.CornerRadiusTopLeft     = 3;
        bgStyle.CornerRadiusTopRight    = 3;
        bgStyle.CornerRadiusBottomLeft  = 3;
        bgStyle.CornerRadiusBottomRight = 3;
        _bar.AddThemeStyleboxOverride("background", bgStyle);

        _phaseLabel = new Label();
        _phaseLabel.Text = "Phase I";
        _phaseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _phaseLabel.AddThemeFontSizeOverride("font_size", 12);
        _phaseLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f, 1f));
        _phaseLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

        vbox.AddChild(_nameLabel);
        vbox.AddChild(_bar);
        vbox.AddChild(_phaseLabel);
        panel.AddChild(vbox);
        AddChild(panel);

        // Centre the panel horizontally at the top.
        OffsetLeft   = -164f;
        OffsetRight  =  164f;
        OffsetTop    =  10f;
        OffsetBottom =  80f;
    }

    public override void _ExitTree()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_bossHealth != null)
        {
            _bossHealth.Damaged -= OnBossDamaged;
            _bossHealth.Died    -= OnBossDied;
            _bossHealth = null;
        }
        if (_bossPhase != null)
        {
            _bossPhase.BossPhaseChanged -= OnBossPhaseChanged;
            _bossPhase = null;
        }
    }

    public override void _Process(double delta)
    {
        if (!_resolved)
        {
            TryResolve();
        }
    }

    private void TryResolve()
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Node enemy) continue;

            var phase = enemy.GetNodeOrNull<BossPhaseComponent>("BossPhaseComponent");
            if (phase == null) continue;

            var health = enemy.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (health == null) continue;

            _bossPhase  = phase;
            _bossHealth = health;

            _bossHealth.Damaged += OnBossDamaged;
            _bossHealth.Died    += OnBossDied;
            _bossPhase.BossPhaseChanged += OnBossPhaseChanged;

            UpdateBar(health.CurrentHp, health.MaxHp);
            _resolved = true;
            Show();
            break;
        }
    }

    private void OnBossDamaged(float amount, float currentHp, float maxHp, Node? source)
    {
        UpdateBar(currentHp, maxHp);
    }

    private void OnBossDied(Node? source)
    {
        if (_bar != null) _bar.Value = 0.0;
        // Fade out after a brief pause so the player sees the bar hit zero.
        GetTree().CreateTimer(1.5f).Timeout += Hide;
        Unsubscribe();
    }

    private void OnBossPhaseChanged(int newPhase)
    {
        if (_phaseLabel != null)
            _phaseLabel.Text = newPhase switch { 2 => "Phase II", 3 => "Phase III", _ => "Phase I" };

        if (_barFill != null)
            _barFill.BgColor = newPhase switch { 2 => ColorPhase2, 3 => ColorPhase3, _ => ColorPhase1 };
    }

    private void UpdateBar(float current, float max)
    {
        if (_bar == null || max <= 0f) return;
        _bar.Value = current / max;
    }
}
