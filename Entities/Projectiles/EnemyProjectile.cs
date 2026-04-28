using Godot;
using Sequence.Components.Animation;

namespace Sequence.Entities.Projectiles;

/// <summary>
/// Animated enemy projectile. Uses SpriteAnimator to loop through the spore sheet frames.
/// </summary>
public partial class EnemyProjectile : ProjectileController
{
    [Export] public Texture2D? ProjectileSheet { get; set; }
    [Export(PropertyHint.Range, "8,256,1")] public int FrameWidth { get; set; } = 50;
    [Export(PropertyHint.Range, "8,256,1")] public int FrameHeight { get; set; } = 50;
    [Export(PropertyHint.Range, "1,30,1")] public int FrameCount { get; set; } = 8;
    [Export(PropertyHint.Range, "1,60,1")] public float AnimFps { get; set; } = 12f;

    private SpriteAnimator? _animator;

    public override void _Ready()
    {
        base._Ready();

        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null && ProjectileSheet != null)
        {
            _animator = new SpriteAnimator(sprite, FrameWidth, FrameHeight);
            _animator.RegisterClip("fly", ProjectileSheet, 0, 0, FrameCount, AnimFps, loop: true);
            _animator.Play("fly");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        _animator?.Tick((float)delta);
    }
}
