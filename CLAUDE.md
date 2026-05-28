# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a real-time strategy (RTS) game built in Unity (C#). Players command units (tanks, artillery) produced at factories to defeat enemy units.

## Common Commands

This is a Unity project — there is no CLI build or test command. Development is done through the Unity Editor.

- **Build prefab from script**: In Unity Editor, use menu `Tools/RTS/Rebuild Self-Propelled Artillery` (defined in `Assets/Editor/SelfPropelledArtilleryPrefabBuilder.cs`).
- **Open solution**: `Strategy.sln` (Visual Studio or Rider).

## Architecture

### Unit Combat Pipeline

Units are driven by `UnitCombat.cs`, which runs a coroutine loop:

1. `CheckEnemies()` — finds a target (manual right-click or auto via `Physics.OverlapSphere`)
2. `AimAtTarget()` — rotates turret (Y) and gun (X pitch); delegates to `ArtilleryWeapon.AimAtTarget()` if the component is present
3. `Attack()` coroutine — fires via `BulletController` (standard) or `ArtilleryWeapon.Fire()` (artillery)

Artillery units skip `BulletController` entirely; `ArtilleryWeapon` creates an `ArtilleryProjectile` that flies a parabolic arc and applies splash damage on impact.

### ScriptableObject Config System

Unit stats live in `Assets/Balance/` as `UnitData` ScriptableObjects (health, damage, speed, range, aiming speeds, angle tolerances). Production costs/times are separate `ProductionItemData` assets. `Factory Production Config.asset` is the master list of producible items — the `SelfPropelledArtilleryPrefabBuilder` editor script updates it automatically.

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

### Effects

`TankCannonEffects` manages muzzle flash (cone particles + point light), gun recoil (0.045s back / 0.16s return), and smoke — all procedurally created at runtime without requiring effect prefabs.

`TankTrackAnimator` animates track segment GameObjects along a looping path (arc over wheels + straight runs) computed geometrically; 30 segments per run by default.

## Key Files

| File | Purpose |
|---|---|
| `Assets/Scripts/UnitController/UnitCombat.cs` | Central unit combat controller |
| `Assets/Scripts/UnitController/ArtilleryWeapon.cs` | Artillery elevation, accuracy, firing |
| `Assets/Scripts/UnitController/ArtilleryProjectile.cs` | Parabolic projectile + splash damage |
| `Assets/Scripts/UnitController/TankTrackAnimator.cs` | Animated track segments |
| `Assets/Scripts/UnitCommandController.cs` | Player input and unit orders |
| `Assets/Scripts/Data/UnitData.cs` | Unit stat ScriptableObject definition |
| `Assets/Scripts/Data/ProductionItemData.cs` | Production cost/time definition |
| `Assets/Editor/SelfPropelledArtilleryPrefabBuilder.cs` | Editor tool to rebuild the SPA prefab |
| `Assets/Balance/Factory Production Config.asset` | Master producible-unit list |

## Adding a New Unit Type

1. Create a `UnitData` ScriptableObject in `Assets/Balance/`.
2. Create a `ProductionItemData` asset and add it to `Factory Production Config.asset`.
3. Build or hand-craft a prefab in `Assets/Prefabs/` with `UnitCombat` (+ optional `ArtilleryWeapon`) attached.
4. If artillery-style: add `ArtilleryWeapon` — `UnitCombat` detects it via `GetComponent` and delegates aiming/firing automatically.
5. For complex prefab setup, create an Editor builder script following `SelfPropelledArtilleryPrefabBuilder` as a template.
