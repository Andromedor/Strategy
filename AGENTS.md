# AGENTS.md

## Project Overview

This project is a Unity RTS inspired by Company of Heroes 2 and Supreme Commander 2.

## Core Architecture Rules

* Follow SOLID, KISS and DRY.
* Use composition over inheritance whenever practical.
* Use ScriptableObjects for unit, building and upgrade data.
* Use event-driven architecture.
* Avoid direct dependencies between UI and gameplay systems.
* Avoid static managers unless justified.
* Do not remove or disable existing mechanics unless the user explicitly asks for removal.
* When extending gameplay, preserve previous behaviour first, then add new functionality on top.

## Systems

### Units

* Units must be data-driven.
* Unit configuration comes from ScriptableObjects.
* Unit behaviour should be modular.
* Avoid large unit classes.

### Buildings

* Building data must come from ScriptableObjects.
* Production queues should be separated from UI.
* Building placement logic must be separated from building prefabs.
* `Outpost` is a capture point, not a regular building or the main base; do not add building HP, destruction or building-selection mechanics to it unless explicitly requested.

### AI

* Use state-based AI.
* Separate movement, targeting and attack behaviour.
* Support future squad-level AI.

### UI

* UI observes gameplay state.
* UI does not own gameplay state.
* Use events for communication.
* If a visual, layout, position, color, size or hierarchy can be edited in the Unity Editor, author it in Unity instead of constructing it from gameplay code.
* Author Unity UI visuals in scenes or prefabs: anchors, layout groups, colors, fonts, sprites, spacing, static labels and hierarchy must be edited through Unity UI tools whenever possible.
* Scripts may bind serialized references, react to events and update runtime data only; do not create or style UI elements in code when the same result can be configured in the Unity editor.
* World-space building UI, including production bars and similar status visuals, must be placed directly in the building prefab or scene hierarchy and wired through serialized references; do not instantiate these visuals from gameplay scripts unless explicitly requested.
* Store reusable UI prefabs in `Assets/Prefabs/UI/`.

### Performance

* Use object pooling.
* Minimize allocations.
* Avoid expensive operations in Update.
* Cache frequently used references.

### Folder Structure

Assets/
|- Scripts/
|  |- Core/
|  |- Gameplay/
|  |- AI/
|  |- Buildings/
|  |- Units/
|  |- UI/
|  `- Infrastructure/
|- ScriptableObjects/
|- Prefabs/
|  `- UI/
|- Art/
`- Scenes/

When proposing solutions always prioritize maintainability and scalability.
