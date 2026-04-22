# Low Level Design: Characters

## Scope

This document defines character-level architecture for:

1. Player (`Entities/Player`)
2. Enemy base and archetypes (`Entities/Enemies`)
3. Boss (`Entities/Enemies/Boss`)

It also specifies component composition contracts and scene wiring requirements.

## Shared Character Architecture

All characters are composition-based and should follow this shape:

1. Root `CharacterBody2D` or entity node.
2. Visual stack: `Sprite2D`, `AnimationPlayer`.
3. Combat stack: `HealthComponent`, `HurtboxComponent`, optional `HitboxComponent`.
4. Behavior stack: `StateMachineComponent` or controller.
5. Optional specialization components per archetype.

Design rules:

1. Behavior decisions should not be embedded in `Hitbox`/`Hurtbox`.
2. Character scripts orchestrate components; components implement isolated logic.
3. Entity root owns references needed by movement, animation, and ability calls.

## Character Node Blueprints

## Player Scene Blueprint

Target scene: `Entities/Player/Player.tscn`

Recommended child composition:

1. `Player` (root)
2. `AnimationPlayer`
3. `Sprite2D`
4. `HealthComponent` (`Team = Player` on linked hurtbox)
5. `SanityComponent` (planned)
6. `HurtboxComponent` (`Team = Player`)
7. `HitboxComponent` (`Team = Player`)
8. `SequenceComponent` (planned)
9. `InventoryComponent` (planned)
10. `AbilityComponent` (planned)
11. `PathwayComponent` (planned)

Primary script responsibilities for `Entities/Player/PlayerController.cs`:

1. Read input and convert to move/attack intents.
2. Trigger animation states and hitbox windows.
3. Delegate health, damage, and resource costs to components.
4. Publish high-level player events (death, sequence change, interact action).

## Enemy Base Scene Blueprint

Target scene: `Entities/Enemies/Enemy.tscn`

Recommended child composition:

1. `Enemy` (root)
2. `AnimationPlayer`
3. `Sprite2D`
4. `HealthComponent`
5. `HurtboxComponent` (`Team = Enemy`)
6. `HitboxComponent` (`Team = Enemy`)
7. `StateMachineComponent`
8. `AggroComponent`
9. `DropComponent` (planned)

Primary script responsibilities for `Entities/Enemies/EnemyBase.cs`:

1. Resolve and cache required component references in `_Ready()`.
2. Register FSM states and transitions.
3. Subscribe to aggro and death events.
4. Drive movement and animation according to current state.

## Boss Scene Blueprint

Target scene: `Entities/Enemies/Boss/Boss.tscn`

Additional composition:

1. `BossPhaseComponent` (existing placeholder)
2. `AbilityComponent` with pathway mirror abilities (planned)

Boss-specific rules:

1. Shares same combat contracts as enemies.
2. Adds phase thresholds and transition behavior.
3. Must remain compatible with base enemy events (`StateChanged`, `EntityDied`).

## State Model Specifications

## Player State Model (controller-driven)

Planned states:

1. `Idle`
2. `Run`
3. `Attack`
4. `Dodge`
5. `Hurt`
6. `Dead`

Core transitions:

1. `Idle <-> Run` from movement input magnitude.
2. `Idle/Run -> Attack` when attack requested and not locked.
3. `Any -> Hurt` on accepted damage (unless invulnerable).
4. `Any -> Dead` on `HealthComponent.IsDead`.

## Enemy State Model (FSM-driven)

Minimum states:

1. `Idle`
2. `Patrol`
3. `Chase`
4. `Attack`
5. `Stagger` (optional early)
6. `Death`

Core transitions:

1. `Idle/Patrol -> Chase` on `AggroAcquired`.
2. `Chase -> Attack` when in range and attack off cooldown.
3. `Attack -> Chase` after recovery window.
4. `Any -> Death` on `HealthComponent.Died`.
5. `Chase/Attack -> Patrol or Idle` on `AggroLost` timeout.

Transition ownership:

1. State evaluation lives in state classes and/or enemy base script.
2. `AggroComponent` and `HealthComponent` only emit signals; they do not mutate state directly.

## Character-to-Component Wiring Contracts

## Required Links

For each character entity:

1. `HurtboxComponent` must resolve a valid `HealthComponent`.
2. `HitboxComponent.Team` and `HurtboxComponent.Team` must be set correctly.
3. `StateMachineComponent` must have initial state registered before first frame update.
4. `AggroComponent` requires collision layers/masks consistent with player detection.

## Animation Windows

Attack animation events should call:

1. `HitboxComponent.ActivateWindow()` on damage-start frame.
2. `HitboxComponent.DeactivateWindow()` on damage-end frame.

This ensures hit timing is animation-authored, not hardcoded in AI update loops.

## Character Event Handling

Each character root should subscribe to:

1. Local `HealthComponent.Died` for immediate death handling.
2. Optional local `HurtboxComponent.HitAccepted` for feedback effects.
3. Optional `SignalBus` events only when cross-entity response is required.

## Per-Archetype Enemy Notes

## Corrupted Seer

1. Preferred distance band; avoid melee proximity.
2. `Chase` state becomes reposition behavior.
3. `Attack` uses telegraphed ranged window.

## Wraith Enforcer

1. Aggressive chase + lunge attack.
2. Post-lunge recovery window to expose counterplay.

## Ritual Construct

1. Minimal locomotion; timed pulse attack.
2. State loop may skip patrol/chase entirely.

## Pale Stalker

1. Lower aggro radius, higher movement speed.
2. Ambush-biased attack entry rules.

## Boss Character LLD

`BossPhaseComponent` contract:

1. Observe current boss HP ratio.
2. Trigger phase transitions at configured thresholds.
3. On transition, mutate boss behavior parameters:
   1. attack cadence
   2. movement speed
   3. ability availability

Phase transition requirements:

1. Transition event should be idempotent (one trigger per threshold).
2. Should not interrupt death transition once death begins.

## Character Data Contracts

Character scripts should consume only resource data, not hardcoded balance constants:

1. `EnemyResource`: max hp, speed, drop table, optional aggro tuning.
2. `PathwayResource`: player start kit and stat modifiers.
3. `AbilityResource`: cooldown, cost, hit profile.

Recommended pattern:

1. Resolve resource in `_Ready()`.
2. Push values into runtime components once.
3. Keep runtime transient state in components, not resources.

## Testing Matrix (Characters)

## Player Tests

1. Attack window only damages enemy during active frames.
2. Player hurtbox ignores player-owned hitbox.
3. Death transition locks movement/inputs.

## Enemy Tests

1. Aggro acquire causes chase transition.
2. Aggro loss causes fallback transition after delay.
3. Enemy death triggers drop flow hook.

## Boss Tests

1. HP threshold triggers exactly one phase transition.
2. Final death state ends encounter and emits completion event.

## Implementation Checklist By File

1. `Entities/Player/PlayerController.cs`
   1. Input map integration, state dispatch, animation event hooks.
2. `Entities/Enemies/EnemyBase.cs`
   1. Component cache, state registration, aggro and death subscriptions.
3. `Entities/Enemies/Boss/BossPhaseComponent.cs`
   1. Threshold model, one-shot phase transitions, tuning application.
4. `Entities/Enemies/*` archetype scripts (planned)
   1. Override tuning and custom state transitions only.

## Risks And Guardrails

1. Risk: logic duplicated between enemy archetypes.
   Guardrail: keep shared behavior in enemy base + state classes.
2. Risk: state/event feedback loops.
   Guardrail: only one owner decides transitions each frame.
3. Risk: animation and hit windows drift apart.
   Guardrail: all damage windows are animation-notify-driven.
