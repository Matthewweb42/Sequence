# Sprint 1 Checklist: Components + Characters

## Objective

Ship a playable vertical slice where player and one enemy archetype can:

1. detect each other,
2. attack,
3. take damage,
4. die,
5. progress sequence once.

## Task List

1. Scene wiring baseline
   1. Add component nodes to `Player.tscn` and `Enemy.tscn`.
   2. Set `CombatTeam` correctly for hitbox/hurtbox pairs.
2. Player controller baseline
   1. Movement input.
   2. Attack input and animation window hooks.
3. Enemy baseline
   1. Register `Idle`, `Chase`, `Attack`, `Death` states.
   2. Transition on `AggroAcquired`, `AggroLost`, and death.
4. Ability/sanity/sequence pass
   1. Implement one player ability with cooldown + sanity cost.
   2. Trigger sequence advance event once from a debug action or shrine stub.
5. Drop/inventory loop
   1. Hook enemy death to drop spawn intent.
   2. Pickup increments inventory material count.
6. Boss phase skeleton
   1. Define thresholds and one-shot transitions.
7. Validation
   1. Run component and world tests.
   2. Manual playtest: player vs one enemy archetype.

## Definition Of Done

1. No compile errors in modified files.
2. Existing tests pass.
3. New tests added for any new non-trivial logic.
4. Vertical slice loop works in editor play mode.
