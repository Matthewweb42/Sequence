# Components Implementation Plan (Phase 1)

## Purpose

This document defines the first implementation slice for the component system so we can build once with clear contracts and minimal rework.

Confirmed priorities for this phase:

1. `HealthComponent`
2. `StateMachineComponent`
3. `AggroComponent`
4. `HitboxComponent`
5. `HurtboxComponent`

## Constraints (Locked)

1. Composition-first architecture (no deep inheritance trees for gameplay behavior).
2. Cross-entity communication should route through `SignalBus` instead of direct component coupling.
3. Components must be reusable by both player and enemies.
4. Data lives in `Resource` assets; components own runtime behavior only.

## Definition Of Done (Per Component)

A component is complete only when all items are true:

1. Script compiles and runs in Godot 4 C#.
2. Unit tests are added or updated for key behavior.
3. Exported editor fields are documented in XML comments and/or this plan.

## Current Baseline

The repository is in scaffold state: most component and entity scripts are empty placeholders. Existing implemented logic and tests currently center on `World/RoomGraph.cs` and `World/Tests/RoomGraphTests.cs`.

Implication: we should first establish stable component contracts and event semantics before scene wiring.

## Cross-Component Contracts

## `HealthComponent`

Responsibilities:

1. Store `MaxHp` and `CurrentHp`.
2. Apply `TakeDamage(float amount, Node source = null)`.
3. Apply `Heal(float amount)`.
4. Emit local events/signals for damaged and death transitions.
5. Broadcast health changes on `SignalBus` without assuming specific UI or AI listeners.

Rules:

1. Clamp HP to range `[0, MaxHp]`.
2. Death transition must fire exactly once.
3. Negative damage/heal inputs are ignored or sanitized.

## `HitboxComponent`

Responsibilities:

1. Represent outgoing attack payload (damage, knockback direction/magnitude, team/faction).
2. Be enable/disable controlled by animation windows or AI attack states.
3. Detect overlaps and forward a normalized hit payload to `HurtboxComponent`.

Rules:

1. Never directly modify target `HealthComponent`; route through target hurtbox.
2. Prevent duplicate hit application within a single active attack window.

## `HurtboxComponent`

Responsibilities:

1. Receive hit payloads from hitboxes.
2. Validate friendliness/faction/invulnerability checks.
3. Apply damage to sibling/assigned `HealthComponent`.
4. Emit/broadcast hit accepted and hit rejected events for debug and gameplay reactions.

Rules:

1. One source of truth for invulnerability gate checks.
2. Supports temporary invulnerability frames (`iFrames`).

## `AggroComponent`

Responsibilities:

1. Detect player within aggro area.
2. Optionally verify line-of-sight before aggro acquired.
3. Emit aggro state transitions (`Acquired`, `Lost`).
4. Provide target reference to `StateMachineComponent` without hard-coding enemy state logic.

Rules:

1. Hysteresis/cooldown for aggro loss to avoid rapid flicker.
2. Signal-only interface to state machine (no direct state mutation in aggro component).

## `StateMachineComponent`

Responsibilities:

1. Own current state and state transition rules.
2. Tick `Enter`, `Update`, `Exit` lifecycle of active state.
3. Consume events from aggro, hurtbox/health, and animation systems.
4. Expose debug-friendly current state for inspector/logging.

Rules:

1. Guard invalid transitions.
2. Deterministic order: process queued transitions once per frame/tick.
3. No direct dependency on concrete enemy classes.

## Event Topology (Decoupling Plan)

Use `SignalBus` for global observability and optional listeners. Keep direct references local only for required sibling links.

Recommended event set for Phase 1:

1. `HealthChanged(entity, current, max)`
2. `EntityDied(entity, source)`
3. `HitLanded(attacker, victim, amount)`
4. `AggroAcquired(owner, target)`
5. `AggroLost(owner, target)`
6. `StateChanged(owner, previousState, nextState)`

## Implementation Order

1. Foundations: `State.cs` and `StateMachineComponent.cs`
2. Vital stats: `HealthComponent.cs`
3. Combat ingress: `HurtboxComponent.cs`
4. Combat egress: `HitboxComponent.cs`
5. Enemy awareness: `AggroComponent.cs`
6. Bus integration: `Autoloads/SignalBus.cs` event definitions for phase scope

Why this order:

1. State machine and health are required by all behavior loops.
2. Hurtbox before hitbox enforces one-way combat flow and avoids circular implementation.
3. Aggro can wire cleanly once state transitions exist.

## Testing Strategy

## Unit Tests (Required)

1. `HealthComponent`
   1. Damage clamps to 0.
   2. Heal clamps to max.
   3. Death event emits once.
2. `HurtboxComponent`
   1. Valid payload applies damage.
   2. Invulnerable target rejects damage.
   3. Friendly-fire rules behave as configured.
3. `HitboxComponent`
   1. Disabled hitbox does not apply hits.
   2. Duplicate overlap in same attack window is de-duplicated.
4. `AggroComponent`
   1. Acquire on target entry + LOS pass.
   2. Lose with configured delay/hysteresis.
5. `StateMachineComponent`
   1. Valid transition path succeeds.
   2. Invalid transition rejected.
   3. Enter/Exit ordering is correct.

## Integration Checks (Minimal)

1. Enemy detects player -> transitions from idle to chase.
2. Enemy hitbox damages player hurtbox/health once per swing.
3. On HP zero, death event reaches state machine and drop logic listeners.

## Risk Controls

Primary risks identified:

1. Tight coupling between components.
2. Event ordering/race conditions.

Mitigations:

1. Use event queue in state machine instead of immediate recursive transitions.
2. Keep hit processing one-directional (`Hitbox -> Hurtbox -> Health`).
3. Restrict global side effects to `SignalBus` listeners, not component internals.

## File-Level Execution Checklist

1. `Components/StateMachine/State.cs`
   1. Define state interface/base (`Enter`, `Update`, `Exit`, optional `CanTransitionTo`).
2. `Components/StateMachine/StateMachineComponent.cs`
   1. Implement registration, transition queue, tick dispatch, state change event.
3. `Components/Health/HealthComponent.cs`
   1. Implement HP model, clamps, and death semantics.
4. `Components/Hurtbox/HurtboxComponent.cs`
   1. Implement hit receiver validation and HP forwarding.
5. `Components/Hitbox/HitboxComponent.cs`
   1. Implement active window handling and overlap hit dispatch.
6. `Components/Aggro/AggroComponent.cs`
   1. Implement detection + LOS + acquire/loss events.
7. `Autoloads/SignalBus.cs`
   1. Add phase-1 event signatures only.
8. `World/Tests/` (or `Components/Tests/` if added)
   1. Add focused tests for each component contract.

## Out Of Scope For This Phase

1. Ability system internals.
2. Inventory/pathway/sequence implementation details.
3. Full scene authoring and animation polish.
4. Network replication concerns.

## Decision Log

1. Plan-first workflow selected to reduce rework.
2. `docs/` chosen as source of truth for implementation plan.
3. Phase 1 limited to combat and enemy behavior core loop components.
4. Damage model for Phase 1 is `float`.
5. Friendly fire is disabled (single-player), so enemy attacks target player only and player attacks target enemies only.
6. Aggro defaults to requiring line-of-sight.
7. State machine updates will run in `_Process` for this phase.
8. New component tests will be placed in `World/Tests`.

## Locked Phase-1 Defaults

1. Damage type: `float`
2. Friendly fire: disabled
3. Aggro line-of-sight: required by default
4. State tick mode: `_Process`
5. Test location: `World/Tests`
