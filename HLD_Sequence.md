# High Level Design — *Sequence*
### A Lord of the Mysteries Roguelite | CS 5410 Final Project

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture Overview](#2-architecture-overview)
3. [Scene & Node Hierarchy](#3-scene--node-hierarchy)
4. [Component Reference](#4-component-reference)
   - [Player Components](#41-player-components)
   - [Enemy Components](#42-enemy-components)
   - [Boss Components](#43-boss-components)
5. [Data Resources](#5-data-resources)
6. [Level & World Systems](#6-level--world-systems)
7. [UI Systems](#7-ui-systems)
8. [Autoloads & Infrastructure](#8-autoloads--infrastructure)
9. [Signal Architecture](#9-signal-architecture)
10. [Core Game Loop](#10-core-game-loop)
11. [File & Folder Structure](#11-file--folder-structure)

---

## 1. Project Overview

**Genre:** Roguelite Metroidvania  
**Engine:** Godot 4.x (C#)  
**Team Size:** 2  
**Target Session Length:** 20–40 minutes  

*Sequence* casts the player as a newly awakened Beyonder navigating a procedurally assembled network of hand-crafted rooms. The primary progression loop is **Sequence Advancement** — consuming alchemical potions to descend from Sequence 9 toward higher power, unlocking new abilities and opening Sequence-locked areas along the way. Death is permanent.

---

## 2. Architecture Overview

The project follows a **component-based composition** pattern inside Godot's scene tree. Shared logic lives in reusable component nodes (e.g., `HealthComponent`, `HurtboxComponent`) that attach to any actor. Game-wide state lives in **Autoloads**. All inter-system communication goes through a **SignalBus** Autoload to keep systems decoupled.

```
Autoloads (global singletons)
  ├── SignalBus       — global event bus
  ├── RunManager      — current run state, permadeath, scene transitions
  └── GameManager     — persistent settings, run reference

Scenes
  ├── World           — root of a run; owns RoomGraph + active RoomInstances
  ├── Player          — composed of component nodes
  ├── Enemy (base)    — composed of component nodes; subclassed per type
  └── UI              — HUD, menus, inventory overlay
```

**Key design principles:**
- `HealthComponent`, `HurtboxComponent`, and `HitboxComponent` are fully reusable — the same scripts attach to the player, all enemy types, and the boss.
- All progression state (Sequence level, inventory, formulas) lives on the **Player** and is readable by any system via `RunManager.Player`.
- Room state (cleared, locked, visited) is owned by `RoomGraph` and persists for the duration of a run.

---

## 3. Scene & Node Hierarchy

```
World
├── RoomGraph           (generates and manages the room map)
├── RoomContainer       (holds active RoomInstance scenes)
│   └── RoomInstance    (loaded room scene)
│       ├── Tilemap
│       ├── ConnectionPoints (North/South/East/West markers)
│       ├── SequenceDoor[]
│       ├── EnemySpawnPoints[]
│       └── MaterialNodes[]
├── Player
│   ├── CharacterBody2D
│   ├── AnimationPlayer
│   ├── Sprite2D
│   ├── HealthComponent
│   ├── SanityComponent
│   ├── HurtboxComponent    (Area2D)
│   ├── HitboxComponent     (Area2D — melee)
│   ├── SequenceComponent
│   ├── InventoryComponent
│   ├── AbilityComponent
│   └── PathwayComponent
├── EnemyContainer
│   └── Enemy (per archetype)
│       ├── CharacterBody2D
│       ├── AnimationPlayer
│       ├── Sprite2D
│       ├── HealthComponent
│       ├── HurtboxComponent
│       ├── HitboxComponent
│       ├── StateMachineComponent
│       ├── AggroComponent  (Area2D)
│       └── DropComponent
└── HUD
    ├── HealthBar
    ├── SanityBar
    ├── SequenceTracker
    ├── AbilityIcons
    └── Minimap
```

---

## 4. Component Reference

### 4.1 Player Components

| Component | Type | Responsibility |
|---|---|---|
| `PlayerController` | Node | Input handling; drives the player state machine (Idle / Run / Jump / Dodge / Attack / Hurt / Dead) |
| `HealthComponent` | Node | Stores `CurrentHp` / `MaxHp`; exposes `TakeDamage(float)` and `Heal(float)`; emits `Damaged` and `Died` signals |
| `SanityComponent` | Node | Resource pool for ability use; drains on ability activation, regenerates over time; emits `Depleted` when empty |
| `HurtboxComponent` | Area2D | Receives collision from enemy `HitboxComponent`; routes hit data to `HealthComponent` and `SignalBus` |
| `HitboxComponent` | Area2D | Active during attack frames; carries damage value and knockback vector; toggled by `AnimationPlayer` |
| `SequenceComponent` | Node | Tracks current Sequence number (9 → lower); checks advancement eligibility; applies Law of Convergence debuff if advancement is overdue; emits `SequenceAdvanced` |
| `InventoryComponent` | Node | Manages `Dictionary<MaterialResource, int>` for materials; tracks discovered `FormulaResource[]` and held `SealedArtifactResource[]` |
| `AbilityComponent` | Node | Registry of unlocked active/passive abilities; handles activation, cooldown timers, and resource cost checks |
| `PathwayComponent` | Node | Holds the player's chosen `PathwayResource`; feeds initial ability list to `AbilityComponent` and stat modifiers to `HealthComponent` / `SanityComponent` |

---

### 4.2 Enemy Components

All enemies inherit a base `Enemy` scene. Each archetype extends it with its own state machine states and ability definitions.

| Component | Type | Responsibility |
|---|---|---|
| `StateMachineComponent` | Node | Generic FSM; each enemy wires up states: `Idle → Patrol → Chase → Attack → Stagger → Death` |
| `HealthComponent` | Node | Same script as player; configured with enemy-specific `MaxHp` |
| `HurtboxComponent` | Area2D | Same script as player |
| `HitboxComponent` | Area2D | Same script as player; damage value set per archetype |
| `AggroComponent` | Area2D | Detection radius; performs a raycast line-of-sight check; notifies `StateMachineComponent` when player is detected |
| `DropComponent` | Node | On `HealthComponent.Died`, consults the enemy's `EnemyResource.DropTable` and spawns `MaterialResource` pickups via `LootTable` |

**Enemy Archetypes (minimum four):**

| Archetype | Behavior Summary |
|---|---|
| Corrupted Seer | Ranged; maintains distance; telegraphs projectile with windup animation |
| Wraith Enforcer | Melee charger; long lunge attack; vulnerable after missing |
| Ritual Construct | Stationary; area-of-effect pulse on a timer; destroyed in one focused burst |
| Pale Stalker | Stealth-adjacent; low `AggroComponent` radius but high movement speed; ambushes from flanks |

---

### 4.3 Boss Components

The boss is a corrupted, Sequence-elevated mirror of the player's chosen Pathway. It reuses all base enemy components and adds:

| Component | Type | Responsibility |
|---|---|---|
| `BossPhaseComponent` | Node | Tracks HP thresholds; transitions between Phase 1, 2, and 3; escalates ability usage and speed per phase |
| `AbilityComponent` | Node | Same script as player; boss's ability set mirrors the player's Pathway abilities at higher damage and frequency |

---

## 5. Data Resources

All tunable parameters are stored in Godot `Resource` subclasses (`.tres` files). No game balance values live in code.

| Resource | Key Fields |
|---|---|
| `PathwayResource` | `Name`, `FlavorText`, `StartingAbilities: AbilityResource[]`, `StatModifiers` |
| `SequenceResource` | `SequenceNumber` (int), `RequiredFormula: FormulaResource`, `UnlockedAbilities: AbilityResource[]` |
| `AbilityResource` | `Name`, `Damage`, `Cooldown`, `SanityCost`, `AnimationTrigger`, `HitboxShape` |
| `FormulaResource` | `RequiredMaterials: Dictionary<MaterialResource, int>`, `OutputSequence`, `LoreDescription` |
| `MaterialResource` | `Name`, `Rarity`, `Icon`, `DropWeight` |
| `SealedArtifactResource` | `ModifierType`, `ModifierValue`, `FlavorText` |
| `EnemyResource` | `MaxHp`, `MovementSpeed`, `DropTable: Dictionary<MaterialResource, float>` |
| `RoomResource` | `ArchetypeTag` (enum), `ConnectionPoints`, `SequenceLockTier` (int) |

---

## 6. Level & World Systems

### RoomGraph

The procedural level generator. Runs once at the start of each run using a seeded `RandomNumberGenerator`.

**Responsibilities:**
- Builds a directed graph of `RoomResource` nodes guaranteeing a valid critical path from start → Sequence Shrine(s) → boss antechamber → boss.
- Distributes optional branches for material rooms, lore rooms, and hidden rooms.
- Places `SequenceDoor` locks at tier boundaries; validates that all required Shrines are reachable before locks.
- Exposes `GetAdjacentRooms(roomId)` and `IsRoomReachable(roomId, playerSequence)` to other systems.

**Validation pass:** After graph generation, a depth-first traversal confirms that the boss room is reachable assuming the player completes every mandatory Shrine. If not, the generator reruns with a new seed (max 10 retries before fallback to a known-good layout).

### RoomInstance

A loaded room scene placed in `RoomContainer`. Manages its own state within the run:
- `IsCleared` (bool) — enemies defeated
- `IsVisited` (bool) — used by Minimap
- `ConnectionPoints` — Node2D markers at each cardinal edge, used by the transition system

### SequenceDoor

A gate (AnimatableBody2D or Area2D trigger) placed in hand-crafted rooms. Listens for `SignalBus.SequenceAdvanced`; opens permanently when `PlayerSequence <= RequiredSequence`.

### Room Archetypes

| Archetype | Content |
|---|---|
| Combat Room | Enemy spawns; clears on all enemies defeated |
| Sequence Shrine | Potion Synthesis UI interaction; advances Sequence on completion |
| Material Room | MaterialNode pickups; no enemies |
| Lore Room | Text fragments; no enemies; optional |
| Boss Antechamber | Transition buffer before boss fight; no enemies |
| Boss Room | Boss encounter; run ends on clear |
| Hidden Room | Sealed Artifact chest; requires wall-break or secret interaction |

---

## 7. UI Systems

| Scene | Responsibility |
|---|---|
| `HUD` | Persistent overlay during gameplay; owns `HealthBar`, `SanityBar`, `SequenceTracker`, `AbilityIcons`, `Minimap` |
| `PotionSynthesisUI` | Opened at Sequence Shrines; displays formula, lists required vs. held materials, confirms synthesis |
| `InventoryUI` | Overlay (pause or dedicated key); shows materials, formulas, and Sealed Artifacts |
| `PathwaySelectScreen` | Pre-run screen; shows three Pathways with name, flavor text, and ability preview |
| `PauseMenu` | In-run pause; options to resume, view controls, or abandon run |
| `RunSummaryScreen` | Displayed on death or run completion; shows Sequence reached, enemies defeated, materials collected, cause of death, and total time |
| `MainMenu` | Entry point; New Run, Settings, Quit |

---

## 8. Autoloads & Infrastructure

### SignalBus *(Autoload)*

Global event bus. All inter-system signals are declared here and connected through it. No direct node references between unrelated systems.

*(See Section 9 for full signal list.)*

### RunManager *(Autoload)*

Owns all mutable state for the current run.

| Field / Method | Description |
|---|---|
| `CurrentSeed: int` | RNG seed for this run |
| `Player: PlayerController` | Reference to the active player node |
| `RoomGraph: RoomGraph` | Reference to the active room graph |
| `StartRun(pathway, seed)` | Initializes a new run |
| `EndRun(cause)` | Triggers permadeath; saves summary data; returns to main menu |
| `TransitionToRoom(roomId)` | Handles room scene swap and player repositioning |

### GameManager *(Autoload)*

Handles settings and any data that persists across runs (e.g., unlocked Pathways if meta-progression is added as a stretch goal).

### LootTable *(Static Utility Class)*

```csharp
public static T Roll<T>(Dictionary<T, float> weightTable, RandomNumberGenerator rng)
```

Used by `DropComponent` and loot chests to perform weighted random selection.

### ParticleSpawner *(Autoload)*

Pooled particle effect manager. Exposes `Spawn(effectId, position)`. Prevents repeated instantiation of common effects (hit sparks, Sequence advancement flash, ability trails).

---

## 9. Signal Architecture

All signals are declared on `SignalBus` and emitted/received by components without direct references to each other.

| Signal | Emitted By | Received By |
|---|---|---|
| `PlayerDamaged(float amount, Node source)` | `HurtboxComponent` (player) | `HUD`, `RunManager` |
| `PlayerDied` | `HealthComponent` (player) | `RunManager` |
| `EnemyDied(Node enemy)` | `HealthComponent` (enemy) | `DropComponent`, `RoomInstance`, `RunManager` |
| `SequenceAdvanced(int newSequence)` | `SequenceComponent` | `HUD`, `SequenceDoor`, `AbilityComponent`, `RunManager` |
| `InventoryChanged` | `InventoryComponent` | `InventoryUI`, `PotionSynthesisUI` |
| `RoomCleared(int roomId)` | `RoomInstance` | `RoomGraph`, `Minimap` |
| `RoomEntered(int roomId)` | `RunManager` | `Minimap`, `HUD` |
| `AbilityActivated(AbilityResource ability)` | `AbilityComponent` | `HUD`, `ParticleSpawner` |
| `SanityDepleted` | `SanityComponent` | `AbilityComponent`, `HUD` |
| `ArtifactPickedUp(SealedArtifactResource artifact)` | `InventoryComponent` | `InventoryUI`, `HUD` |

---

## 10. Core Game Loop

```
[Main Menu]
    │
    ▼
[Pathway Select Screen]
    │  Player chooses Pathway
    ▼
RunManager.StartRun(pathway, seed)
    │  RoomGraph generates map
    │  Player spawns in start room
    ▼
[Run Loop]
    │
    ├── Combat Room ──► defeat enemies ──► room cleared ──► loot drops
    │
    ├── Material Room ──► collect MaterialResources ──► InventoryComponent updated
    │
    ├── Sequence Shrine ──► open PotionSynthesisUI
    │       │  has formula + ingredients?
    │       ├── YES ──► synthesis ritual ──► SequenceComponent.Advance()
    │       │               │  SequenceAdvanced signal fires
    │       │               │  New abilities unlock
    │       │               └  SequenceDoors at next tier open
    │       └── NO  ──► return to exploration
    │
    ├── Hidden Room ──► SealedArtifact chest ──► passive modifier applied
    │
    ├── Lore Room ──► text fragment displayed
    │
    ├── Boss Antechamber ──► transition
    │
    └── Boss Room
            │
            ├── Player wins ──► Run Summary Screen (Victory)
            └── Player dies ──► RunManager.EndRun(cause)
                                    └──► Run Summary Screen (Death)
                                             └──► Main Menu
```

**Law of Convergence:** If the player remains at a Sequence level past a time or room-count threshold without advancing, `SequenceComponent` applies a stacking debuff (reduced max sanity, increased damage taken). This creates pressure to seek Shrines actively.

---

## 11. File & Folder Structure

```
res://
├── Autoloads/
│   ├── SignalBus.cs
│   ├── RunManager.cs
│   ├── GameManager.cs
│   └── ParticleSpawner.cs
│
├── Components/
│   ├── Health/
│   │   └── HealthComponent.cs
│   ├── Hurtbox/
│   │   └── HurtboxComponent.cs
│   ├── Hitbox/
│   │   └── HitboxComponent.cs
│   ├── StateMachine/
│   │   ├── StateMachineComponent.cs
│   │   └── State.cs          (base class)
│   ├── Sanity/
│   │   └── SanityComponent.cs
│   ├── Sequence/
│   │   └── SequenceComponent.cs
│   ├── Inventory/
│   │   └── InventoryComponent.cs
│   ├── Ability/
│   │   └── AbilityComponent.cs
│   ├── Pathway/
│   │   └── PathwayComponent.cs
│   ├── Aggro/
│   │   └── AggroComponent.cs
│   └── Drop/
│       └── DropComponent.cs
│
├── Entities/
│   ├── Player/
│   │   ├── Player.tscn
│   │   └── PlayerController.cs
│   ├── Enemies/
│   │   ├── Enemy.tscn          (base scene)
│   │   ├── EnemyBase.cs
│   │   ├── CorruptedSeer/
│   │   ├── WraithEnforcer/
│   │   ├── RitualConstruct/
│   │   ├── PaleStalker/
│   │   └── Boss/
│   │       ├── Boss.tscn
│   │       └── BossPhaseComponent.cs
│   └── Interactables/
│       ├── SequenceDoor.tscn
│       ├── MaterialNode.tscn
│       └── LootChest.tscn
│
├── World/
│   ├── RoomGraph.cs
│   ├── RoomInstance.cs
│   ├── Rooms/              (hand-crafted .tscn files)
│   │   ├── Combat/
│   │   ├── Shrine/
│   │   ├── Material/
│   │   ├── Lore/
│   │   ├── BossAntechamber/
│   │   ├── BossRoom/
│   │   └── Hidden/
│   └── World.tscn
│
├── Resources/
│   ├── Pathways/           (.tres files)
│   ├── Sequences/
│   ├── Abilities/
│   ├── Materials/
│   ├── Formulas/
│   ├── Artifacts/
│   ├── Enemies/
│   └── Rooms/
│
├── UI/
│   ├── HUD/
│   │   ├── HUD.tscn
│   │   └── HUDController.cs
│   ├── Menus/
│   │   ├── MainMenu.tscn
│   │   ├── PathwaySelectScreen.tscn
│   │   ├── PauseMenu.tscn
│   │   └── RunSummaryScreen.tscn
│   └── Overlays/
│       ├── InventoryUI.tscn
│       └── PotionSynthesisUI.tscn
│
└── Utilities/
    └── LootTable.cs
```

---

*Document version 1.0 — Spring 2025*
