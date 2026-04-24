using Godot;
using Sequence.Components.Ability;
using Sequence.Components.Health;
using Sequence.Components.Hitbox;
using Sequence.Components.Sanity;
using Sequence.Components.Sequence;
using Sequence.Components.Status;
using Sequence.Entities.Projectiles;
using Sequence.Entities.Enemies.Boss;

namespace Sequence.Entities.Player;

/// <summary>
/// Handles all Bard Pathway ability logic for sequences 9–5.
/// Attach as a child node of the Player scene alongside AbilityComponent.
/// Input actions required: ability_1 through ability_6
/// </summary>
public partial class BardAbilityHandler : Node
{
    // --- Cooldown constants (seconds) ---
    private const float CdSpiritualHymn = 12f;
    private const float CdSunshineBurst = 8f;
    private const float CdBlindingFlash = 15f;
    private const float CdTheurgicalPurification = 20f;
    private const float CdLightOfHoliness = 25f;
    private const float CdTruthSeal = 30f;
    private const float CdContractBinding = 35f;
    private const float CdSolarOath = 45f;
    private const float CdPureWhiteRay = 10f;

    // --- Sanity cost constants ---
    private const float ScHymn = 5f;
    private const float ScSunshineBurst = 15f;
    private const float ScBlindingFlash = 20f;
    private const float ScPurification = 25f;
    private const float ScHoliness = 30f;
    private const float ScTruthSeal = 10f;
    private const float ScContractBinding = 25f;
    private const float ScSolarOath = 40f;
    private const float ScPureWhiteRay = 12f;

    [Export] public NodePath? AbilityPath { get; set; }
    [Export] public NodePath? SanityPath { get; set; }
    [Export] public NodePath? HealthPath { get; set; }
    [Export] public NodePath? StatusPath { get; set; }
    [Export] public NodePath? ChannelPath { get; set; }
    [Export] public NodePath? SequencePath { get; set; }
    [Export] public NodePath? AoEHitboxPath { get; set; }

    [Export] public PackedScene? SunshineBurstScene { get; set; }
    [Export] public PackedScene? PureWhiteRayScene { get; set; }
    [Export] public PackedScene? LightPillarScene { get; set; }

    private AbilityComponent? _ability;
    private SanityComponent? _sanity;
    private HealthComponent? _health;
    private StatusEffectComponent? _status;
    private ChannelComponent? _channel;
    private SequenceComponent? _sequence;
    private HitboxComponent? _aoEHitbox;

    // State
    private bool _sunEyesActive;
    private float _sunEyesTimer;
    private bool _physFortApplied;
    private float _ailmentDurationMultiplier = 1f;
    private bool _facingRight = true;
    private float _deactivateAoENextFrame = -1f;

    public override void _Ready()
    {
        var parent = GetParent();

        _ability = ResolveSibling<AbilityComponent>(AbilityPath, "AbilityComponent");
        _sanity = ResolveSibling<SanityComponent>(SanityPath, "SanityComponent");
        _health = ResolveSibling<HealthComponent>(HealthPath, "HealthComponent");
        _status = ResolveSibling<StatusEffectComponent>(StatusPath, "StatusEffectComponent");
        _channel = ResolveSibling<ChannelComponent>(ChannelPath, "ChannelComponent");
        _sequence = ResolveSibling<SequenceComponent>(SequencePath, "SequenceComponent");

        if (AoEHitboxPath != null && !AoEHitboxPath.IsEmpty)
            _aoEHitbox = GetNodeOrNull<HitboxComponent>(AoEHitboxPath);
        else
            _aoEHitbox = parent?.GetNodeOrNull<HitboxComponent>("AoEHitboxComponent");

        if (_ability != null)
            _ability.AbilityActivated += OnAbilityActivated;

        if (_channel != null)
        {
            _channel.ChannelPulse += OnChannelPulse;
            _channel.ChannelEnded += OnChannelEnded;
        }

        if (_sequence != null)
            _sequence.SequenceAdvanced += OnSequenceAdvanced;

        if (_status != null)
            _status.EffectApplied += OnStatusEffectApplied;
    }

    public override void _ExitTree()
    {
        if (_ability != null) _ability.AbilityActivated -= OnAbilityActivated;
        if (_channel != null)
        {
            _channel.ChannelPulse -= OnChannelPulse;
            _channel.ChannelEnded -= OnChannelEnded;
        }
        if (_sequence != null) _sequence.SequenceAdvanced -= OnSequenceAdvanced;
        if (_status != null) _status.EffectApplied -= OnStatusEffectApplied;
    }

    public override void _Process(double delta)
    {
        TrackFacing();
        HandleChannelInput();
        HandleInstantAbilityInput();
        TickSunEyesTimer((float)delta);
        TickAoEDeactivation((float)delta);
    }

    // -------------------------------------------------------------------------
    // Input Handling
    // -------------------------------------------------------------------------

    private void HandleChannelInput()
    {
        if (_ability == null || _channel == null) return;

        if (Input.IsActionPressed("ability_1"))
        {
            if (!_channel.IsChanneling && IsUnlocked("bardic_song"))
            {
                // Use 0 cost here — ChannelComponent drains sanity over time
                if (_ability.TryActivate("bardic_song", 0f, 0f))
                {
                    _channel.TryBeginChannel("bardic_song");
                }
            }
        }
        else if (_channel.IsChanneling && _channel.ActiveChannelId == "bardic_song")
        {
            _channel.EndChannel("released");
        }
    }

    private void HandleInstantAbilityInput()
    {
        if (_ability == null) return;

        if (Input.IsActionJustPressed("ability_2"))
            _ability.TryActivate("spiritual_recovery_hymn", CdSpiritualHymn, ScHymn);

        if (Input.IsActionJustPressed("ability_3"))
            _ability.TryActivate("sunshine_burst", CdSunshineBurst, ScSunshineBurst);

        if (Input.IsActionJustPressed("ability_4"))
            _ability.TryActivate("blinding_flash", CdBlindingFlash, ScBlindingFlash);

        if (Input.IsActionJustPressed("ability_5"))
            _ability.TryActivate("theurgical_purification", CdTheurgicalPurification, ScPurification);

        if (Input.IsActionJustPressed("ability_6"))
        {
            // S5: Solar Oath takes priority if unlocked; otherwise Light of Holiness
            if (IsUnlocked("solar_oath"))
                _ability.TryActivate("solar_oath", CdSolarOath, ScSolarOath);
            else if (IsUnlocked("light_of_holiness"))
                _ability.TryActivate("light_of_holiness", CdLightOfHoliness, ScHoliness);
        }

        if (Input.IsActionJustPressed("ability_7"))
            _ability.TryActivate("pure_white_ray", CdPureWhiteRay, ScPureWhiteRay);

        if (Input.IsActionJustPressed("ability_8"))
            _ability.TryActivate("truth_seal", CdTruthSeal, ScTruthSeal);

        if (Input.IsActionJustPressed("ability_9"))
            _ability.TryActivate("contract_binding", CdContractBinding, ScContractBinding);
    }

    // -------------------------------------------------------------------------
    // Ability Dispatch
    // -------------------------------------------------------------------------

    private void OnAbilityActivated(string abilityId)
    {
        switch (abilityId)
        {
            case "spiritual_recovery_hymn": ExecuteSpiritualRecoveryHymn(); break;
            case "sunshine_burst": ExecuteSunshineBurst(); break;
            case "blinding_flash": ExecuteBlindingFlash(); break;
            case "theurgical_purification": ExecuteTheurgicalPurification(); break;
            case "light_of_holiness": ExecuteLightOfHoliness(); break;
            case "truth_seal": ExecuteTruthSeal(); break;
            case "contract_binding": ExecuteContractBinding(); break;
            case "solar_oath": ExecuteSolarOath(); break;
            case "pure_white_ray": ExecutePureWhiteRay(); break;
            case "holy_water_ritual": ExecuteHolyWaterRitual(); break;
        }
    }

    // -------------------------------------------------------------------------
    // S9 — Bard
    // -------------------------------------------------------------------------

    private void OnChannelPulse(string channelId, float elapsed)
    {
        if (channelId != "bardic_song" || _status == null) return;

        var notarized = IsUnlocked("notarized_enhancement");
        var moveBonus = notarized ? 1.225f : 1.15f;   // +50% of the 15% bonus
        var atkBonus  = notarized ? 1.15f  : 1.10f;

        var pulseDuration = _channel?.PulseIntervalSeconds * 2f ?? 0.5f;
        pulseDuration *= _ailmentDurationMultiplier;

        _status.ApplyEffect("bardic_song_buff", pulseDuration,
            modifiers: new[]
            {
                new StatModifier("move_speed", moveBonus, ModifierOp.Multiplicative),
                new StatModifier("attack_speed_multiplier", atkBonus, ModifierOp.Multiplicative),
            },
            tags: new[] { "buff", "bardic_song" });
    }

    private void OnChannelEnded(string channelId, string reason)
    {
        if (channelId != "bardic_song") return;
        _status?.RemoveEffect("bardic_song_buff");
    }

    private void ExecuteSpiritualRecoveryHymn()
    {
        _sanity?.Restore(30f);
        _health?.Heal(5f);
    }

    // -------------------------------------------------------------------------
    // S8 — Light Suppliant
    // -------------------------------------------------------------------------

    private void ExecuteSunshineBurst()
    {
        if (SunshineBurstScene == null) return;

        var projectile = SunshineBurstScene.Instantiate<SunshineBurstProjectile>();

        // S8 enhancement: add burn DoT on hit
        if (IsUnlocked("sunshine_burst"))
        {
            projectile.StatusOnHit = "burn";
            projectile.StatusDuration = 3f;
        }

        projectile.Direction = _facingRight ? Vector2.Right : Vector2.Left;
        GetTree()?.CurrentScene?.AddChild(projectile);
        projectile.GlobalPosition = GetParentPosition();
    }

    private void ExecuteBlindingFlash()
    {
        if (_aoEHitbox == null) return;

        _aoEHitbox.Damage = 0f;
        _aoEHitbox.StatusEffectOnHit = "blind";
        _aoEHitbox.StatusDurationSeconds = 3f;
        _aoEHitbox.ActivateWindow();
        _deactivateAoENextFrame = 0.016f; // deactivate after ~1 frame
    }

    private void ExecuteHolyWaterRitual()
    {
        // TODO: holy_water_ritual — requires item/crafting system
        GD.Print("[Bard] Holy Water Ritual: not yet implemented.");
    }

    // -------------------------------------------------------------------------
    // S7 — Solar High Priest
    // -------------------------------------------------------------------------

    private void OnSequenceAdvanced(int newSequence)
    {
        if (newSequence == 7 && !_physFortApplied)
        {
            ApplyPhysicalFortification();
            ApplyBasicAttackDamageBonus(1.25f);
        }
        if (newSequence == 5)
        {
            ApplyBasicAttackDamageBonus(1.2f); // Combined with S7: 1.25 * 1.2 = 1.5x total
        }
    }

    private void ApplyBasicAttackDamageBonus(float multiplier)
    {
        var hitbox = GetParent()?.GetNodeOrNull<Sequence.Components.Hitbox.HitboxComponent>("HitboxComponent");
        if (hitbox != null)
            hitbox.Damage *= multiplier;
    }

    private void ApplyPhysicalFortification()
    {
        if (_health == null) return;
        _physFortApplied = true;
        _health.SetMaxHp(_health.MaxHp * 1.25f, preserveRatio: true);
        _ailmentDurationMultiplier = 0.5f;
    }

    private void ExecuteTheurgicalPurification()
    {
        // Remove all debuffs from player
        if (_status != null)
        {
            _status.RemoveByTag("debuff");

            // Notarized enhancement: seal wounds — brief debuff immunity after cleanse
            if (IsUnlocked("notarized_enhancement"))
            {
                _status.ApplyEffect("wound_sealed", 5f, tags: new[] { "buff", "debuff_immune" });
            }
        }

        // AoE holy damage ring
        if (_aoEHitbox != null)
        {
            _aoEHitbox.Damage = 30f;
            _aoEHitbox.StatusEffectOnHit = string.Empty;
            _aoEHitbox.ActivateWindow();
            _deactivateAoENextFrame = 0.016f;
        }

        // Instant-kill undead below HP threshold
        var purifyThreshold = 0.3f;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Node2D enemy) continue;

            var tags = enemy.GetNodeOrNull<Sequence.Components.Combat.EntityTagComponent>("EntityTagComponent");

            if (tags == null || !tags.HasTag("undead")) continue;

            var hp = enemy.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (hp == null || hp.IsDead) continue;

            if (hp.CurrentHp / hp.MaxHp < purifyThreshold)
            {
                hp.TakeDamage(99999f, this);
            }
        }
    }

    private void ExecuteLightOfHoliness()
    {
        if (LightPillarScene == null) return;

        var ascended = IsUnlocked("light_of_holiness_ascended");
        var vaporizeThreshold = ascended ? 0.6f : 0.4f;

        SpawnLightPillar(GetParentPosition(), vaporizeThreshold);

        if (ascended)
        {
            // Flanking pillars
            var offset = new Vector2(120f, 0f);
            SpawnLightPillar(GetParentPosition() + offset, vaporizeThreshold);
            SpawnLightPillar(GetParentPosition() - offset, vaporizeThreshold);
        }

        // Activate sun eyes for 30 seconds (unless permanent already)
        if (!IsUnlocked("night_vision_permanent"))
        {
            _sunEyesActive = true;
            _sunEyesTimer = 30f;
        }
    }

    private void SpawnLightPillar(Vector2 position, float vaporizeThreshold)
    {
        if (LightPillarScene == null) return;

        var pillar = LightPillarScene.Instantiate<LightPillarProjectile>();
        pillar.VaporizeThreshold = vaporizeThreshold;
        pillar.ApplyNotarySigil = IsUnlocked("notarized_enhancement") || IsUnlocked("light_of_holiness_ascended");
        GetTree()?.CurrentScene?.AddChild(pillar);
        // Spawn above screen; pillar moves downward
        pillar.GlobalPosition = new Vector2(position.X, position.Y - 400f);
    }

    // -------------------------------------------------------------------------
    // S6 — Notary
    // -------------------------------------------------------------------------

    private void ExecuteTruthSeal()
    {
        // Apply truth_sealed to the nearest enemy
        var nearest = FindNearestEnemy();
        if (nearest == null) return;

        var status = nearest.GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");

        // S6: also apply +20% damage taken multiplier
        status?.ApplyEffect("truth_sealed", 15f,
            modifiers: new[] { new StatModifier("damage_received_multiplier", 1.2f, ModifierOp.Multiplicative) },
            tags: new[] { "truth_sealed", "debuff" });
    }

    private void ExecuteContractBinding()
    {
        var pos = GetParentPosition();
        const float radius = 200f;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Node2D enemy) continue;

            if (enemy.GlobalPosition.DistanceTo(pos) > radius) continue;

            var status = enemy.GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");
            if (status == null) continue;

            // Bosses get slowed; regular enemies fully bound
            var isBoss = enemy.GetNodeOrNull<BossPhaseComponent>("BossPhaseComponent") != null;
            if (isBoss)
            {
                status.ApplyEffect("contract_slowed", 8f,
                    modifiers: new[] { new StatModifier("move_speed", 0.5f, ModifierOp.Multiplicative) },
                    tags: new[] { "debuff", "slowed" });
            }
            else
            {
                status.ApplyEffect("contract_bound", 8f, tags: new[] { "debuff", "contract_bound" });
            }
        }
    }

    // -------------------------------------------------------------------------
    // S5 — Priest of Light
    // -------------------------------------------------------------------------

    private void ExecuteSolarOath()
    {
        if (_status == null) return;

        // S5: extended duration 20→25s
        var duration = IsUnlocked("solar_oath") ? 25f : 20f;

        _status.ApplyEffect("solar_oath", duration,
            modifiers: new[]
            {
                new StatModifier("damage_multiplier", 1.35f, ModifierOp.Multiplicative),
                new StatModifier("move_speed", 1.20f, ModifierOp.Multiplicative),
            },
            tags: new[] { "buff", "solar_oath" });
    }

    private void ExecutePureWhiteRay()
    {
        if (PureWhiteRayScene == null) return;

        var projectile = PureWhiteRayScene.Instantiate<ProjectileController>();
        projectile.Direction = _facingRight ? Vector2.Right : Vector2.Left;
        GetTree()?.CurrentScene?.AddChild(projectile);
        projectile.GlobalPosition = GetParentPosition();
    }

    // -------------------------------------------------------------------------
    // Status event handlers (passives)
    // -------------------------------------------------------------------------

    private void OnStatusEffectApplied(string effectId)
    {
        if (_status == null) return;

        // horror_immunity: immediately nullify fear/panic effects when unlocked
        if (IsUnlocked("horror_immunity"))
        {
            if (_status.HasTag("fear") || _status.HasTag("panic"))
            {
                _status.RemoveByTag("fear");
                _status.RemoveByTag("panic");
            }
        }

        // dispel_fear: 25% chance to auto-nullify fear effects
        if (IsUnlocked("dispel_fear") && !IsUnlocked("horror_immunity"))
        {
            if (_status.HasTag("fear") && GD.Randf() < 0.25f)
            {
                _status.RemoveByTag("fear");
            }
        }

        // debuff_immune (from wound_sealed): reject new debuffs
        if (IsUnlocked("theurgical_purification") && _status.HasEffect("wound_sealed"))
        {
            var effect = effectId;
            if (_status.HasTag("debuff"))
            {
                _status.RemoveEffect(effect);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private bool IsUnlocked(string abilityId) => _ability?.IsUnlocked(abilityId) ?? false;

    private void TrackFacing()
    {
        // Delegate to PlayerController's canonical facing state
        if (GetParent() is PlayerController pc)
        {
            _facingRight = pc.FacingRight;
            return;
        }
        var moveX = Input.GetAxis("move_left", "move_right");
        if (moveX > 0.1f) _facingRight = true;
        else if (moveX < -0.1f) _facingRight = false;
    }

    private void TickSunEyesTimer(float delta)
    {
        if (_sunEyesActive && !IsUnlocked("night_vision_permanent"))
        {
            _sunEyesTimer -= delta;
            if (_sunEyesTimer <= 0f)
            {
                _sunEyesActive = false;
            }
        }
    }

    private void TickAoEDeactivation(float delta)
    {
        if (_deactivateAoENextFrame < 0f) return;
        _deactivateAoENextFrame -= delta;
        if (_deactivateAoENextFrame <= 0f)
        {
            _aoEHitbox?.DeactivateWindow();
            _deactivateAoENextFrame = -1f;
        }
    }

    private Node2D? FindNearestEnemy()
    {
        var pos = GetParentPosition();
        Node2D? nearest = null;
        var nearestDist = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Node2D enemy) continue;
            var dist = enemy.GlobalPosition.DistanceTo(pos);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    private Vector2 GetParentPosition()
    {
        return GetParent() is Node2D n ? n.GlobalPosition : Vector2.Zero;
    }

    private T? ResolveSibling<T>(NodePath? path, string fallbackName) where T : Node
    {
        if (path != null && !path.IsEmpty)
            return GetNodeOrNull<T>(path);
        return GetParent()?.GetNodeOrNull<T>(fallbackName);
    }
}
