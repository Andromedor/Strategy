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

### AI

* Use state-based AI.
* Separate movement, targeting and attack behaviour.
* Support future squad-level AI.

### UI

* UI observes gameplay state.
* UI does not own gameplay state.
* Use events for communication.

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
|- Art/
`- Scenes/

When proposing solutions always prioritize maintainability and scalability.
