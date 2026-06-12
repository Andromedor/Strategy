# Strategy Project Documentation

## 1. Огляд проєкту

`Strategy` - Unity RTS, натхненна Supreme Commander 2 та Company of Heroes 2. Основна ідея архітектури: gameplay-логіка має бути data-driven, UI лише відображає стан, а майбутні AI/online-команди мають проходити через спільний command pipeline.

Головні правила розробки:

- існуючі механіки не видаляються без прямого прохання;
- `Outpost` лишається точкою захоплення, не звичайною будівлею;
- конфіги юнітів, будівель, AI, карт і production живуть у ScriptableObject;
- візуали UI/барів/панелей мають редагуватися у сцені або prefab, скрипти лише оновлюють runtime-дані;
- gameplay не має напряму залежати від UI.

## 2. Сцени

- `Assets/Scenes/MainMenu.unity` - головне меню, схватка, завантаження, налаштування, online entry.
- `Assets/Scenes/mainScene.unity` - основна карта, підтримує стартові точки, AI/debug, save/load, minimap.
- `Assets/Scenes/PrototypeMap_1_2Spawns.unity` - тестова карта на 2 spawn points.
- `Assets/Scenes/PrototypeMap_2_3Spawns.unity` - тестова карта на 3 spawn points.
- `Assets/Scenes/PrototypeMap_3_4Spawns.unity` - тестова карта на 4 spawn points.

Карти описуються через `MapDefinition` і збираються у `MapCatalog`. Меню не має хардкодити сцени: нова карта додається через asset у catalog і Build Settings.

## 3. Дані та конфіги

Основні ScriptableObject:

- `UnitData` - prefab, HP, швидкість, атака, turret/idle параметри.
- `BuildingData` - prefab, ціна, build time, max HP, placement box, grid footprint, UI panel type.
- `ProductionItemData` - що виробляє factory, ціна, час, prefab юніта.
- `ProductionConfig` - список доступних production items.
- `BuildingPlacementGridConfig` - розмір клітинки, origin, marker prefab, кольори valid/invalid/build area.
- `AiDifficultyProfile` - частота рішень, бажані outpost/factory, розміри груп атаки, агресія.
- `MapDefinition` - id, назва, scene path, max players, режими, preview, стартові ресурси.
- `GameAssetRegistry` - stable IDs для save/load.

Runtime fallback assets дублюються у `Assets/Resources/GameAssetRegistry.asset` та `Assets/Resources/MapCatalog.asset`, щоб save/load працював навіть якщо сценовий reference не підключили вручну.

## 4. Команди, команди гравців і майбутній multiplayer

Command pipeline:

- `PlayerCommand` описує намір: move, attack, build, produce, upgrade.
- `CommandDispatcher` приймає команди від локального гравця, AI або майбутнього network bridge.
- `PlayerCommandExecutor` виконує команди після validation.
- `NetworkCommandBridge` залишений як точка входу для server-authoritative online v1.

Перевага такого підходу: AI, local player і remote player можуть використовувати однакові правила. Це зменшує ризик, що online або AI будуть обходити economy/placement/team validation.

## 5. Команди, альянси і match setup

Команди описує `MatchTeamSettings` та `TeamSlot`.

Важливі класи:

- `TeamType` - конкретна сторона.
- `TeamRelations` - перевірка allied/hostile.
- `LocalPlayerContext` - яка команда є локальним гравцем.
- `MatchLaunchConfig` - runtime-конфіг матчу з меню або save/load.
- `MatchLaunchContext` - handoff між меню і gameplay scene.

Правило: не покладатися лише на layer або `Player/Enemy`. Для 3+ сторін і 2v2 потрібно перевіряти team relations.

## 6. Старт матчу і spawn points

`PlayerStartPoint` - сценовий marker старту. Він має:

- `SlotIndex`;
- список player counts, для яких активний;
- позицію/поворот;
- optional visual flag.

`MatchStartSpawner` читає `MatchTeamSettings` або `MatchLaunchConfig`, знаходить активні `PlayerStartPoint`, спавнить стартову `MilitaryBase` безкоштовно і створює стартові юніти.

Стартова база:

- використовує той самий `BuildingData`, що і майбутня будівля;
- резервує grid cells через `BuildingGridOccupancy`;
- не проходить звичайний construction timer, якщо це старт матчу.

## 7. Будівлі

Звичайні будівлі:

- `MilitaryBase_Prefeb` - головна будівля/ConstructionCenter, HP 5000.
- Heavy Factory - factory, HP 2000, production queue.

`Outpost` не є будівлею. Він має окрему capture/economy логіку і не бере участі у building HP/destruction/selection як звичайна будівля.

Компоненти будівель:

- `BuildingHealth` - HP, damage, death event, construction health cap.
- `BuildingSelectionState` - окремий selection visual для будівель.
- `BuildingHealthBarPresenter` - world-space HP bar, selection/damage/Alt visibility.
- `BuildingConstructionState` - build timer, блокує production/build area до завершення.
- `BuildingConstructionVisual` - поетапна збірка будівлі з плавною появою частин.
- `BuildingConstructionStatusPresenter` - UI/progress timer будівництва, якщо підключений у prefab.
- `BuildingGridOccupancy` - які grid cells зайняті.

## 8. Будівництво і grid placement

`BuildingPlacementManager` відповідає за інтерактивне розміщення:

- preview йде за мишею;
- snap до grid;
- rotation Q/E;
- validation footprint;
- перевірка friendly build radius;
- перевірка block mask;
- витрата ресурсів;
- запуск `BuildingConstructionState`.

`BuildingGridPlacementService` містить чисту логіку:

- `WorldToCell`;
- `CellToWorld`;
- `GetOccupiedCells`;
- `CanPlace/EvaluatePlacement`;
- `Reserve/Release`.

Розмір будівлі редагується в `BuildingData.GridFootprintCells`, а не у gameplay code.

## 9. Production

`BuildingProduction` тримає queue і runtime state. UI не володіє чергою.

Для multi-factory:

- `FactoryProductionDistributor` обирає сумісну factory з найменшим pending work;
- кліки по production button розподіляються як `1-0-0`, `1-1-0`, `1-1-1`, `2-1-1`;
- `ProductionButtonStateAggregator` сумує queue count для вибраних factory і бере progress із factory, яка найшвидше завершує цей item.

Візуали:

- кнопка production показує queue badge і progress strip;
- factory має world-space production bar, розміщений у prefab, не інстанситься gameplay-скриптом.

## 10. Юніти і combat

Основні компоненти:

- `UnitCombat` - HP, target selection, damage, turret target intent.
- `UnitHealth` - runtime HP model.
- `UnitHealthBarPresenter` - HP bar над юнітом.
- `UnitSelectionState` - selection visual.
- `NavMeshVehicleMotor`, `TrackedVehicleMotor`, `WheeledVehicleAnimator` - рух і візуали техніки.
- `BulletPool`, `BulletController`, `TankCannonEffects`, `AutocannonVisualEffects` - projectile/weapon visuals.

HP bar юніта:

- з’являється після damage;
- зникає після затримки;
- через Alt показується/ховається для своїх і ворогів;
- повертається разом із юнітом, не billboard до камери.

## 11. Selection і control groups

Selection rules:

- left click: спочатку юніт, якщо немає - Factory/MilitaryBase;
- drag box: якщо у рамці є player units, будівлі ігноруються;
- drag box без юнітів може вибрати Factory/MilitaryBase;
- Ctrl + left click toggle-ить будівлі по одній;
- right click команди отримують тільки юніти.

Control groups 1-9 можуть містити:

- юніти;
- factory;
- MilitaryBase.

Знищені об’єкти мають прибиратись із selection/control group через events.

## 12. AI

AI побудовано модульно:

- `AiDirector` створює controller-и для AI команд;
- `AiController` працює через `GameTickRunner`;
- `AiTargetSelector` шукає hostile non-Outpost buildings;
- `AiDifficultyProfile` задає Easy/Medium/Hard.

AI цілі:

- захоплювати Outpost для економіки;
- будувати factory, якщо потрібно production;
- виробляти юніти без resource cheats;
- атакувати групами, а не по одному;
- знищити всі hostile non-Outpost buildings.

## 13. Victory/defeat

`MatchVictorySystem` перевіряє alive non-Outpost buildings.

Команда/альянс програє, якщо не має живих звичайних будівель. `MatchResultPanelUI` показує Victory/Defeat локальному гравцю і дає повернення в меню.

## 14. Main menu, lobby і online foundation

`GameMenuController` керує:

- головним меню;
- `Схватка`;
- режимом з ботами;
- online entry;
- load menu;
- resolution settings;
- exit.

Offline match:

- вибір карти;
- player slots;
- bot/open/local player;
- team/alliance;
- AI difficulty тільки для ботів;
- starting resources.

Online foundation:

- `NetworkSessionService` - host/join lobby entry;
- `NetworkCommandBridge` - майбутнє виконання remote commands через server authority;
- AI в online має працювати на host.

Для реального Relay/Lobby потрібен налаштований Unity Services Project ID.

## 15. Save/load

Файли збереження лежать у:

`Application.persistentDataPath/Saves/quick_save.json`

Snapshot містить:

- version;
- date/time;
- map id;
- match mode/team mode;
- local player/team;
- camera position/rotation/zoom;
- teams/alliances/controllers;
- resources;
- units: id, team, position, rotation, HP;
- buildings: id, team, position, rotation, HP, construction state, grid occupancy, factory queue;
- outposts: owner/capture/upgrade/resource timer.

Load flow:

1. UI читає список через `SaveGameFileIO.GetSaveFiles`.
2. В меню показується дружній формат `Сейв N • дата • карта`.
3. `MatchLaunchContext.SetPendingSaveLoad` передає save path і config у gameplay scene.
4. `SaveGameManager.RestorePendingSave` очищає runtime units/buildings, відновлює ресурси, будівлі, юніти, outposts і камеру.

Сумісність:

- version `2` зберігає construction-state явно;
- старі сейви без construction-state вважаються legacy і для готових будівель не мають відновлювати випадковий construction cap як low HP.

## 16. HUD і gameplay UI

HUD системи:

- `ConstructionPanelUI` - build buttons, disabled стан при нестачі ресурсів;
- `ProductionPanelUI` - units production;
- `SelectionInfoPanelUI` - характеристики вибраного об’єкта;
- `ControlGroupBarUI` - групи 1-9;
- `OutpostPanelUI`/`OutpostStatusUI` - outpost capture/upgrade;
- `InGamePauseMenuUI` - ESC меню;
- `SaveGameToastUI` - коротке повідомлення після save/load;
- `MatchResultPanelUI` - victory/defeat.

UI має спостерігати за gameplay state, але не бути джерелом gameplay state.

## 17. Minimap і межі карти

`GameplayPresentationSetupBuilder` налаштовує:

- `MinimapCamera`;
- `MinimapView`;
- `MapBoundary`;
- червону лінію межі;
- чорні outside planes за картою;
- save toast CanvasGroup.

Це editor setup, не gameplay runtime побудова UI.

## 18. Editor setup builders

Editor tools:

- `GameMenuSetupBuilder` - rebuild main menu, map UI, debug panel, toast.
- `InGamePauseMenuSetupBuilder` - in-game ESC menu і save dependencies у gameplay scenes.
- `GameplayPresentationSetupBuilder` - minimap, map boundary, toast visual setup.
- validators у `Strategy/Validate/...` перевіряють, що сцени мають потрібні UI/service objects.

Після зміни сценових UI бажано проганяти відповідний setup/validate.

## 19. Як додати нову карту

1. Створити сцену.
2. Додати `PlayerStartPoint` для потрібної кількості гравців.
3. Перевірити NavMesh.
4. Створити `MapDefinition`.
5. Виставити `MapId`, `DisplayName`, `ScenePath`, `MaxPlayers`, `SupportedModes`.
6. Додати asset у `MapCatalog`.
7. Додати сцену в Build Settings.
8. Запустити меню і перевірити, що карта є у списку.

## 20. Як додати нову будівлю

1. Створити prefab.
2. Додати/налаштувати `BuildingHealth`, selection visual, optional production/construction visuals.
3. Створити `BuildingData`.
4. Виставити price, build time, max HP, prefab, grid footprint, check box.
5. Додати stable id у `GameAssetRegistry`.
6. Додати building у build panel list.
7. Перевірити placement, construction, HP bar, save/load.

## 21. Як додати новий юніт

1. Створити prefab.
2. Додати movement/combat/health/selection компоненти.
3. Створити `UnitData`.
4. Створити `ProductionItemData`.
5. Додати stable IDs у `GameAssetRegistry`.
6. Додати production item у `ProductionConfig`.
7. Перевірити factory queue, spawn, HP bar, save/load.

## 22. Тестування

Основні перевірки:

- `dotnet build Strategy.sln`
- `dotnet test Strategy.PlayModeTests.csproj --no-build`
- Unity batchmode validators:
  - `GameplayPresentationSetupBuilder.Validate`
  - smoke PlayMode tests

PlayMode coverage має закривати:

- selection priority;
- control groups;
- grid placement;
- construction timer/HP;
- production distribution;
- AI spawn/difficulty;
- save/load resources, buildings, camera;
- main menu smoke;
- minimap/boundary presence.

## 23. Відомі обмеження

- Online foundation є, але повний Relay/Lobby потребує Unity Services Project ID.
- Save/load v2 не зберігає активні projectile-и та точні NavMesh paths.
- Старі legacy-сейви без construction-state не можуть точно відрізнити “пошкоджена будівля” від “construction HP cap”; нові сейви це вже зберігають явно.
- UI builder-и мають використовуватись обережно: якщо дизайнер вручну міняє scene hierarchy, builder може перебудувати частину ієрархії.
