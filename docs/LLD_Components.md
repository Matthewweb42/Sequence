# Low Level Design: Components

## Scope

This document defines low-level contracts for all component scripts under `Components/` and related bus contracts in `Autoloads/SignalBus.cs`.

Goals:

1. Establish concrete interfaces and runtime responsibilities.
2. Minimize cross-component coupling.
3. Standardize exported fields, signal names, and update order.
4. Provide implementation-ready specs for both implemented and planned components.

## Design Conventions

1. Components are attachable and reusable across player and enemies.
2. Data tuning lives in `Resources/*.tres`; components hold transient runtime state.
3. Direct component references are allowed only for same-entity relationships.
4. Cross-system observability is published via `SignalBus`.
5. Damage model is `float`.
6. Friendly fire is disabled by team checks (`CombatTeam`).

## Runtime Event Contracts

Global events defined in `Autoloads/SignalBus.cs`:

1. `HealthChanged(Node entity, float current, float max)`
2. `EntityDied(Node entity, Node source)`
3. `HitLanded(Node attacker, Node victim, float amount)`
4. `AggroAcquired(Node owner, Node target)`
5. `AggroLost(Node owner, Node target)`
6. `StateChanged(Node owner, string previousState, string nextState)`
7. `SequenceAdvanced(int newSequence)`

## Component Catalog

## `Components/Combat/CombatTeam.cs`

Purpose:

1. Shared faction enum to enforce no-friendly-fire rules.

Values:

1. `Neutral`
2. `Player`
3. `Enemy`

Rules:

1. A `HitboxComponent` must not damage `HurtboxComponent` with the same team.
2. `Neutral` can be used for traps/hazards as a future extension.

---

## `Components/Health/HealthComponent.cs`

Status: implemented.

### Exported Fields

1. `MaxHp: float` (`[1, 10000]`)
2. `StartAtMaxHp: bool`
3. `StartingHp: float` (`[0, 10000]`)

### Public API

1. `CurrentHp: float` (read-only)
2. `IsDead: bool` (read-only)
3. `TakeDamage(float amount, Node source = null): bool`
4. `Heal(float amount): bool`
5. `SetMaxHp(float newMaxHp, bool preserveRatio = true): void`

### Local Signals

1. `Damaged(float amount, float currentHp, float maxHp, Node source)`
2. `Healed(float amount, float currentHp, float maxHp)`
3. `Died(Node source)`

### Invariants

1. `CurrentHp` is always clamped to `[0, MaxHp]`.
2. Death is emitted once per life cycle.
3. Non-positive damage/heal returns `false` and does not mutate state.

### Publish Behavior

1. Publishes `HealthChanged` on ready, damage, heal, and max-hp changes.
2. Publishes `EntityDied` on lethal damage.

---

## `Components/Hitbox/HitboxComponent.cs`

Status: implemented.

### Exported Fields

1. `Damage: float`
2. `Team: CombatTeam`
3. `ActiveOnReady: bool`

### Public API

1. `IsAttackActive: bool` (read-only)
2. `ActivateWindow(): void`
3. `DeactivateWindow(): void`
4. `GetAttackerEntity(): Node`

### Local Signals

1. `HitboxActivated()`
2. `HitboxDeactivated()`

### Behavior

1. Uses `AreaEntered` callbacks to detect `HurtboxComponent` overlaps.
2. De-duplicates per target instance during a single attack window.
3. Clears hit memory when a window is opened or closed.

### Constraints

1. Never applies damage directly to `HealthComponent`.
2. Must route through `HurtboxComponent.ReceiveHit(...)`.

---

## `Components/Hurtbox/HurtboxComponent.cs`

Status: implemented.

### Exported Fields

1. `Team: CombatTeam`
2. `IsInvulnerable: bool`
3. `IFrameDurationSeconds: float`
4. `HealthPath: NodePath`

### Public API

1. `ReceiveHit(HitboxComponent source): bool`
2. `SetTemporaryInvulnerability(float durationSeconds): void`

### Local Signals

1. `HitAccepted(Node attacker, float damage)`
2. `HitRejected(Node attacker, string reason)`

### Rejection Reasons

1. `no_source`
2. `hitbox_inactive`
3. `non_positive_damage`
4. `invulnerable`
5. `friendly_fire_blocked`
6. `missing_health_component`
7. `health_rejected`

### Behavior

1. Resolves health target from `HealthPath` or sibling `HealthComponent` named `HealthComponent`.
2. Applies i-frame timer after accepted hit.
3. Publishes `HitLanded` through `SignalBus` on accepted hit.

---

## `Components/Aggro/AggroComponent.cs`

Status: implemented.

### Exported Fields

1. `RequireLineOfSight: bool = true`
2. `LoseAggroDelaySeconds: float`
3. `OcclusionMask: uint`
4. `ExplicitTargetPath: NodePath`

### Public API

1. `HasTarget: bool`
2. `CurrentTarget: Node2D`

### Local Signals

1. `AggroAcquired(Node2D target)`
2. `AggroLost(Node2D target)`

### Behavior

1. Tracks candidate targets from `BodyEntered/Exited` and `AreaEntered/Exited`.
2. Acquires first visible candidate each frame.
3. Uses delayed loss hysteresis to avoid flicker.
4. Publishes aggro acquire/loss on `SignalBus`.

### LOS Check

1. Raycast from component `GlobalPosition` to target `GlobalPosition`.
2. Excludes own and target RIDs.
3. LOS passes when no blocking hit is returned.

---

## `Components/StateMachine/State.cs`

Status: implemented.

### Base Contract

1. `Name: string`
2. `Enter(StateMachineComponent owner): virtual`
3. `Update(StateMachineComponent owner, float delta): virtual`
4. `Exit(StateMachineComponent owner): virtual`
5. `CanTransitionTo(string targetState): virtual bool`

### Usage Rules

1. State classes must be side-effect light in constructors.
2. Heavy initialization belongs in `Enter(...)`.
3. External transitions should be requested through the state machine.

---

## `Components/StateMachine/StateMachineComponent.cs`

Status: implemented.

### Exported Fields

1. `ProcessInProcessLoop: bool = true`

### Public API

1. `CurrentState: State`
2. `CurrentStateName: string`
3. `RegisterState(State state, bool setAsInitial = false): bool`
4. `HasState(string stateName): bool`
5. `QueueTransition(string targetState): bool`
6. `TransitionNow(string targetState): bool`

### Local Signals

1. `StateChanged(string previousState, string nextState)`

### Behavior

1. Keeps state map by unique state name.
2. Processes at most one queued transition per `_Process` tick.
3. Rejects transition if target missing, same state, or blocked by `CanTransitionTo`.
4. Publishes state-change to `SignalBus`.

### Determinism Contract

1. Transition dequeue executes before `CurrentState.Update(...)` each frame.
2. Enter/Exit order: `old.Exit -> new.Enter -> state changed emit`.

---

## Planned Component Specs (Not Yet Implemented)

## `Components/Ability/AbilityComponent.cs`

Planned responsibilities:

1. Register unlocked abilities from pathway/sequence state.
2. Validate activation preconditions (cooldown, resource cost).
3. Trigger animation windows and hitbox activation.
4. Publish `AbilityActivated`, `AbilityBlocked`, `AbilityCooldownStarted` events.

Required dependencies:

1. `SanityComponent` (cost checks)
2. `SequenceComponent` (unlock gates)
3. `HitboxComponent` (for attack windows)

## `Components/Drop/DropComponent.cs`

Planned responsibilities:

1. Subscribe to sibling `HealthComponent.Died`.
2. Resolve weighted loot from `EnemyResource.DropTable`.
3. Spawn pickups via `LootTable` and/or world pickup factory.

## `Components/Inventory/InventoryComponent.cs`

Planned responsibilities:

1. Track material counts.
2. Track discovered formulas.
3. Track held artifacts and modifiers.
4. Publish inventory change events for HUD/menus.

## `Components/Pathway/PathwayComponent.cs`

Planned responsibilities:

1. Hold selected pathway resource.
2. Apply pathway stat modifiers to health/sanity.
3. Seed initial ability unlocks.

## `Components/Sanity/SanityComponent.cs`

Planned responsibilities:

1. Maintain sanity pool current/max.
2. Drain for abilities and regenerate over time.
3. Emit depleted/recovered signals.

## `Components/Sequence/SequenceComponent.cs`

Planned responsibilities:

1. Track sequence progression from start to target.
2. Validate advancement requirements.
3. Emit `SequenceAdvanced` and publish via `SignalBus`.

## Integration Flows

## Combat Damage Flow

1. `HitboxComponent.ActivateWindow()`
2. Collision: `HitboxComponent.OnAreaEntered(...)`
3. `HurtboxComponent.ReceiveHit(hitbox)`
4. `HealthComponent.TakeDamage(...)`
5. Events: `Damaged`, `HealthChanged`, optional `Died`, `EntityDied`, `HitLanded`

## Enemy Awareness Flow

1. Candidate enters `AggroComponent` area.
2. LOS check passes.
3. `AggroAcquired` signal + bus publish.
4. State machine consumer queues transition to `Chase`.

## Sequence Door Unlock Flow

1. Player advances sequence.
2. `SignalBus.PublishSequenceAdvanced(newSequence)`
3. `RoomGraph` listener unlocks eligible doors.

## Testing Requirements

Minimum component test matrix:

1. Health clamp, heal clamp, death-once.
2. Hitbox inactive no-op.
3. Hurtbox rejects same-team hits.
4. Hurtbox accepts valid enemy hit and applies damage.
5. State machine queue order and transition guard.
6. Aggro acquire/loss with LOS and delay.

## Open Technical Decisions

1. Whether to add a shared `DamagePayload` struct for knockback/crit/status.
2. Whether state machine should support `_PhysicsProcess` mode.
3. Whether aggro should prioritize nearest target instead of first candidate.
