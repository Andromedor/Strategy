# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a real-time strategy (RTS) game built in Unity (C#). Players command units (tanks, artillery) produced at factories to defeat enemy units. Capturable outposts on the map generate resources over time.

## Common Commands

This is a Unity project — there is no CLI build or test command. Development is done through the Unity Editor.

- **Build SPA prefab**: In Unity Editor, use menu `Tools/RTS/Rebuild Self-Propelled Artillery` (defined in `Assets/Editor/SelfPropelledArtilleryPrefabBuilder.cs`).
- **Configure Quad Autocannon prefab**: `Tools/RTS/Configure Quad Autocannon` (defined in `Assets/Editor/QuadAutocannonPrefabConfigurator.cs`). The same script has a `Validate()` method callable headlessly from CI.
- **Open solution**: `Strategy.sln` (Visual Studio or Rider).

## Architecture

### Unit Combat Pipeline

Units are driven by `UnitCombat.cs`, which runs a coroutine loop:

1. `CheckEnemies()` — finds a target (manual right-click or auto via `Physics.OverlapSphere`)
2. `AimAtTarget()` — rotates turret (Y) and gun (X pitch); delegates to `ArtilleryWeapon.AimAtTarget()` if the component is present
3. `Attack()` coroutine — fires via `BulletController` (standard) or `ArtilleryWeapon.Fire()` (artillery)

Artillery units skip `BulletController` entirely; `ArtilleryWeapon` creates an `ArtilleryProjectile` that flies a parabolic arc and applies splash damage on impact.

`QuadAutocannonCombat` subclasses `UnitCombat` and overrides `FireAtTarget()` to alternate between two muzzle transforms. It uses `AutocannonVisualEffects` instead of `TankCannonEffects`.

### Wheeled Vehicle Movement

Wheeled units (e.g., Quad Autocannon) use a separate movement stack that bypasses Unity's built-in NavMesh steering:

- **`NavMeshVehicleMotor`** — disables `NavMeshAgent.updatePosition` and `updateRotation`, reads the agent's path corners for look-ahead routing, then drives the GameObject directly via a bicycle steering model. Handles forward speed, braking, reverse-on-obstacle, post-reverse steering bias, and stuck detection. Calls `_agent.nextPosition` each frame to keep the agent in sync.
- **`WheeledVehicleAnimator`** — companion component that reads `NavMeshVehicleMotor.CurrentSteerAngle` to yaw front wheels and integrates forward speed to spin all drive wheels. Creates pivot GameObjects at runtime around each wheel transform.

Tracked units (tanks, SPA) use the default `NavMeshAgent` movement and `TankTrackAnimator`; wheeled units swap in `NavMeshVehicleMotor` + `WheeledVehicleAnimator` instead.

### ScriptableObject Config System

Unit stats live in `Assets/Balance/` as `UnitData` ScriptableObjects (health, damage, speed, range, aiming speeds, angle tolerances). Production costs/times are separate `ProductionItemData` assets. `Factory Production Config.asset` is the master list of producible items.

### Communication — EventManager

`EventManager` (static class) is the inter-system event bus. Key events:
- `OnUnitSelected` / `OnUnitDeselected`
- `OnUnitMoveCommand` / `OnUnitAttackTargetChanged`
- `OnFactorySelected` / `OnConstructionClosed`

Do not wire direct references between managers; go through EventManager.

### Team / Layer System

Team identity uses `TeamComponent` (implements `ITeam`). Targeting is layer-based: `PlayerUnit` and `EnemyUnit` layers. `UnitCombat` uses a layer mask so units never auto-target allies.

### Input & Selection

`UnitCommandController` handles all player input:
- Left-click drag → `BoxCast` multi-select
- Right-click on enemy → manual attack target (stored on `UnitCombat`)
- Right-click on ground → move command via `NavMeshAgent` with chess-pattern formation offsets

### Bullet Pooling

`BulletPool` pre-instantiates 200 bullets. Always retrieve bullets from the pool rather than `Instantiate`-ing them. Artillery projectiles are not pooled (they self-destroy).

### Outpost System

Capturable map objectives that generate resources for their owner.

- **`Outpost`** — core state machine (neutral / player-owned / enemy-owned). Exposes `TickCapture()` called by `OutpostCaptureZone` each frame. Generates resources via `ResourceManager` on a configurable ticks-per-minute schedule. Supports a one-time upgrade (costs resources, doubles resource income, activates an extra `ConstructionCenter` build area). Maintains a static `AllOutposts` list for aggregate queries.
- **`OutpostCaptureZone`** — trigger-collider wrapper. Tracks which `TeamComponent` units and blocking buildings are inside. A capture can only proceed when one team has units present, the other does not, and no buildings block the zone.

### Resource System

`ResourceManager` (singleton) tracks separate player and enemy resource pools. Outposts call `ResourceManager.Instance.Add(team, amount)` on each tick. UI listens to the static `OnResourceChanged` event. `ResourceManager.Instance.Spend(amount)` is the single point for deducting player resources.

### Effects

`TankCannonEffects` manages muzzle flash, gun recoil, and smoke for tracked units — all procedurally created at runtime without effect prefabs.

`AutocannonVisualEffects` is the equivalent for the Quad Autocannon. It creates per-muzzle flash/smoke/light sets and handles gun recoil shared across both barrels.

`TankTrackAnimator` animates track segment GameObjects along a looping path computed geometrically; 30 segments per run by default.

## Key Files

| File | Purpose |
|---|---|
| `Assets/Scripts/UnitController/UnitCombat.cs` | Central unit combat controller (base class) |
| `Assets/Scripts/UnitController/QuadAutocannonCombat.cs` | Autocannon subclass — dual-muzzle alternating fire |
| `Assets/Scripts/UnitController/ArtilleryWeapon.cs` | Artillery elevation, accuracy, firing |
| `Assets/Scripts/UnitController/ArtilleryProjectile.cs` | Parabolic projectile + splash damage |
| `Assets/Scripts/UnitController/NavMeshVehicleMotor.cs` | Bicycle-model wheeled vehicle driver (bypasses NavMesh steering) |
| `Assets/Scripts/UnitController/WheeledVehicleAnimator.cs` | Wheel spin and steer animation for wheeled units |
| `Assets/Scripts/UnitController/TankTrackAnimator.cs` | Animated track segments for tracked units |
| `Assets/Scripts/UnitController/AutocannonVisualEffects.cs` | Procedural per-muzzle effects for Quad Autocannon |
| `Assets/Scripts/UnitController/TankCannonEffects.cs` | Muzzle flash, recoil, smoke for tracked units |
| `Assets/Scripts/UnitCommandController.cs` | Player input and unit orders |
| `Assets/Scripts/Building and creat Uniit/Outpost.cs` | Outpost state, capture logic, resource generation |
| `Assets/Scripts/Building and creat Uniit/OutpostCaptureZone.cs` | Trigger that feeds unit counts into Outpost.TickCapture() |
| `Assets/Scripts/ResourceManager.cs` | Singleton resource pool for player and enemy |
| `Assets/Scripts/Data/UnitData.cs` | Unit stat ScriptableObject definition |
| `Assets/Scripts/Data/ProductionItemData.cs` | Production cost/time definition |
| `Assets/Editor/SelfPropelledArtilleryPrefabBuilder.cs` | Editor tool to rebuild the SPA prefab |
| `Assets/Editor/QuadAutocannonPrefabConfigurator.cs` | Editor tool to configure the Quad Autocannon prefab |
| `Assets/Balance/Factory Production Config.asset` | Master producible-unit list |

## Adding a New Unit Type

1. Create a `UnitData` ScriptableObject in `Assets/Balance/`.
2. Create a `ProductionItemData` asset and add it to `Factory Production Config.asset`.
3. Build or hand-craft a prefab in `Assets/Prefabs/`.

**Tracked unit** (tank, SPA):
- Attach `UnitCombat` (or `ArtilleryWeapon` for artillery — `UnitCombat` detects it via `GetComponent`).
- `NavMeshAgent` uses default `updatePosition`/`updateRotation = true`.
- Add `TankTrackAnimator` for track animation; `TankCannonEffects` is created procedurally.

**Wheeled unit** (Quad-style):
- Subclass `UnitCombat` or use `QuadAutocannonCombat` if dual-muzzle is needed.
- Attach `NavMeshVehicleMotor` + `WheeledVehicleAnimator`. Set `NavMeshAgent.updatePosition = false` and `updateRotation = false` — the motor manages position/rotation directly.
- `AutocannonVisualEffects` handles per-muzzle particles and recoil.
- Use `QuadAutocannonPrefabConfigurator` as an editor-script template.

## Development Rules

- Дотримуйся ООП, SOLID, KISS та DRY.
- Надавай перевагу композиції над наслідуванням.
- Використовуй наслідування лише коли існує очевидний зв'язок "is-a".
- Не створюй God Object.
- Не використовуй зайві Singleton.
- UI не повинен містити gameplay логіку.
- Використовуй ScriptableObject для конфігурацій.
- Використовуй Object Pooling для часто створюваних об'єктів.
- Не використовуй FindObjectOfType та GameObject.Find у runtime коді.
- Для взаємодії незалежних систем використовуй EventManager та інтерфейси.

## Workflow

Перед написанням коду:

1. Проаналізуй задачу.
2. Запропонуй архітектуру.
3. Перерахуйте необхідні класи.
4. Поясни відповідальність кожного класу.
5. Лише після цього генеруй код.

Якщо для якісної архітектури потрібні додаткові класи, інтерфейси, ScriptableObject або сервіси — створюй їх самостійно.
