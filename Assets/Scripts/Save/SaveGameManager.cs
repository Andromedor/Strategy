using System;
using System.Collections;
using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Core;
using Strategy.Data;
using Strategy.Maps;
using Strategy.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Strategy.Save
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class SaveGameManager : MonoBehaviour
    {
        public static event Action<string> SaveStatusMessage;

        [SerializeField] private GameAssetRegistry _registry;
        [SerializeField] private MapCatalog _mapCatalog;
        [SerializeField] private BuildingPlacementGridConfig _gridConfig;

        private readonly List<TeamResourceAmount> _resourceBuffer = new();
        private readonly List<ProductionItemData> _queuedItemsBuffer = new();
        private readonly List<ProductionItemData> _restoreQueueBuffer = new();

        private void Start()
        {
            if (MatchLaunchContext.HasPendingSaveLoad)
                StartCoroutine(RestorePendingSave());
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
                SaveQuick();
        }

        public void SaveQuick()
        {
            MatchLaunchConfig config = MatchLaunchContext.CurrentConfig;
            if (config != null && !config.AllowsSaving)
            {
                Publish("Збереження недоступне в мережевій грі");
                return;
            }

            if (_registry == null)
            {
                Publish("Немає GameAssetRegistry для збереження");
                return;
            }

            SaveGameSnapshot snapshot = CaptureSnapshot();
            SaveGameFileIO.WriteQuickSave(snapshot);
            Publish("Гру збережено");
        }

        private IEnumerator RestorePendingSave()
        {
            yield return null;

            string savePath = MatchLaunchContext.PendingSavePath;
            if (!SaveGameFileIO.TryRead(savePath, out SaveGameSnapshot snapshot))
            {
                Publish("Не вдалося завантажити гру");
                MatchLaunchContext.ClearPendingSaveLoad();
                yield break;
            }

            RestoreSnapshot(snapshot);
            MatchLaunchContext.ClearPendingSaveLoad();
            Publish("Гру завантажено");
        }

        private SaveGameSnapshot CaptureSnapshot()
        {
            MatchLaunchConfig config = MatchLaunchContext.CurrentConfig ?? BuildSceneConfig();
            SaveGameSnapshot snapshot = new()
            {
                savedAtUtc = DateTime.UtcNow.ToString("u"),
                mapId = config.MapId,
                mode = config.Mode,
                teamMode = config.TeamMode,
                localTeam = config.LocalTeam,
                localPlayerId = config.LocalPlayerId
            };

            CaptureTeams(config, snapshot);
            CaptureResources(snapshot);
            CaptureUnits(snapshot);
            CaptureBuildings(snapshot);
            CaptureOutposts(snapshot);
            return snapshot;
        }

        private MatchLaunchConfig BuildSceneConfig()
        {
            MatchTeamSettings settings = MatchTeamSettings.Active;
            List<TeamLaunchSlot> slots = new();

            if (settings != null)
            {
                for (int i = 0; i < settings.Teams.Count; i++)
                {
                    TeamSlot team = settings.Teams[i];
                    slots.Add(new TeamLaunchSlot(
                        team.Team,
                        team.AllianceId,
                        team.Controller,
                        team.PlayerId,
                        i,
                        team.Team.ToString(),
                        Strategy.AI.AiDifficultyLevel.Medium,
                        ResourceManager.Instance != null ? ResourceManager.Instance.GetResource(team.Team) : 0));
                }
            }

            MapDefinition map = _mapCatalog != null && _mapCatalog.Maps.Count > 0 ? _mapCatalog.Maps[0] : null;
            SkirmishTeamMode teamMode = slots.Count switch
            {
                >= 4 => SkirmishTeamMode.TwoVsTwo,
                3 => SkirmishTeamMode.ThreePlayer,
                _ => SkirmishTeamMode.OneVsOne
            };
            return new MatchLaunchConfig(MatchLaunchMode.OfflineBots, teamMode, map, LocalPlayerContext.LocalTeam, 0, slots);
        }

        private void CaptureTeams(MatchLaunchConfig config, SaveGameSnapshot snapshot)
        {
            for (int i = 0; i < config.Teams.Count; i++)
            {
                TeamLaunchSlot slot = config.Teams[i];
                if (slot == null)
                    continue;

                snapshot.teams.Add(new TeamSlotSnapshot
                {
                    team = slot.Team,
                    allianceId = slot.AllianceId,
                    controller = slot.Controller,
                    playerId = slot.PlayerId,
                    spawnSlotIndex = slot.SpawnSlotIndex,
                    playerName = slot.PlayerName,
                    aiDifficulty = slot.AiDifficulty,
                    startingResources = slot.StartingResources
                });
            }
        }

        private void CaptureResources(SaveGameSnapshot snapshot)
        {
            if (ResourceManager.Instance == null)
                return;

            ResourceManager.Instance.CopyResources(_resourceBuffer);
            for (int i = 0; i < _resourceBuffer.Count; i++)
            {
                TeamResourceAmount resource = _resourceBuffer[i];
                snapshot.resources.Add(new ResourceSnapshot
                {
                    team = resource.Team,
                    amount = resource.Amount
                });
            }
        }

        private void CaptureUnits(SaveGameSnapshot snapshot)
        {
            for (int i = 0; i < UnitCombat.All.Count; i++)
            {
                UnitCombat unit = UnitCombat.All[i];
                if (unit == null || unit.IsDead || unit.UnitData == null)
                    continue;

                if (!_registry.TryGetId(unit.UnitData, out string unitId))
                    continue;

                snapshot.units.Add(new UnitSnapshot
                {
                    unitId = unitId,
                    team = unit.Team,
                    position = new SerializableVector3(unit.transform.position),
                    rotation = new SerializableQuaternion(unit.transform.rotation),
                    currentHealth = unit.CurrentHealth
                });
            }
        }

        private void CaptureBuildings(SaveGameSnapshot snapshot)
        {
            for (int i = 0; i < BuildingHealth.All.Count; i++)
            {
                BuildingHealth health = BuildingHealth.All[i];
                if (health == null || health.IsDead || health.GetComponentInParent<Outpost>() != null)
                    continue;

                BuildingGridOccupancy occupancy = health.GetComponent<BuildingGridOccupancy>();
                BuildingData buildingData = occupancy != null ? occupancy.BuildingData : null;
                if (buildingData == null || !_registry.TryGetId(buildingData, out string buildingId))
                    continue;

                TeamComponent team = health.GetComponentInParent<TeamComponent>();
                BuildingSnapshot building = new()
                {
                    buildingId = buildingId,
                    team = team != null ? team.Team : TeamType.Neutral,
                    position = new SerializableVector3(health.transform.position),
                    rotation = new SerializableQuaternion(health.transform.rotation),
                    currentHealth = health.CurrentHealth,
                    originCell = new SerializableVector2Int(occupancy != null ? occupancy.OriginCell : Vector2Int.zero),
                    rotationSteps = occupancy != null ? occupancy.RotationSteps : 0
                };

                CaptureFactory(health.GetComponent<BuildingProduction>(), building);
                snapshot.buildings.Add(building);
            }
        }

        private void CaptureFactory(BuildingProduction factory, BuildingSnapshot building)
        {
            if (factory == null)
                return;

            FactorySnapshot factorySnapshot = new();
            if (factory.TryGetCurrentProduction(out FactoryProductionRuntimeState state) &&
                _registry.TryGetId(state.Item, out string currentId))
            {
                factorySnapshot.currentItemId = currentId;
                factorySnapshot.remainingSeconds = state.RemainingSeconds;
            }

            factory.CopyQueuedItems(_queuedItemsBuffer);
            for (int i = 0; i < _queuedItemsBuffer.Count; i++)
            {
                if (_registry.TryGetId(_queuedItemsBuffer[i], out string queuedId))
                    factorySnapshot.queuedItemIds.Add(queuedId);
            }

            building.factory = factorySnapshot;
        }

        private void CaptureOutposts(SaveGameSnapshot snapshot)
        {
            for (int i = 0; i < Outpost.All.Count; i++)
            {
                Outpost outpost = Outpost.All[i];
                if (outpost == null)
                    continue;

                OutpostSaveState state = outpost.CaptureState();
                snapshot.outposts.Add(new OutpostSnapshot
                {
                    sceneName = outpost.name,
                    position = new SerializableVector3(outpost.transform.position),
                    hasOwner = state.HasOwner,
                    owner = state.Owner,
                    hasCapturingTeam = state.HasCapturingTeam,
                    capturingTeam = state.CapturingTeam,
                    captureProgressSeconds = state.CaptureProgressSeconds,
                    isUpgraded = state.IsUpgraded,
                    resourceTimer = state.ResourceTimer
                });
            }
        }

        private void RestoreSnapshot(SaveGameSnapshot snapshot)
        {
            if (_registry == null || snapshot == null)
                return;

            ClearRuntimeObjects();
            RestoreResources(snapshot);
            RestoreBuildings(snapshot);
            RestoreUnits(snapshot);
            RestoreOutposts(snapshot);
        }

        private void ClearRuntimeObjects()
        {
            for (int i = UnitCombat.All.Count - 1; i >= 0; i--)
            {
                if (UnitCombat.All[i] != null)
                    Destroy(UnitCombat.All[i].gameObject);
            }

            for (int i = BuildingHealth.All.Count - 1; i >= 0; i--)
            {
                BuildingHealth building = BuildingHealth.All[i];
                if (building != null && building.GetComponentInParent<Outpost>() == null)
                    Destroy(building.gameObject);
            }
        }

        private void RestoreResources(SaveGameSnapshot snapshot)
        {
            if (ResourceManager.Instance == null)
                return;

            _resourceBuffer.Clear();
            for (int i = 0; i < snapshot.resources.Count; i++)
            {
                ResourceSnapshot resource = snapshot.resources[i];
                _resourceBuffer.Add(new TeamResourceAmount(resource.team, resource.amount));
            }

            ResourceManager.Instance.RestoreResources(_resourceBuffer);
        }

        private void RestoreBuildings(SaveGameSnapshot snapshot)
        {
            Transform root = RuntimeObjectContainer.Get("Buildings");
            for (int i = 0; i < snapshot.buildings.Count; i++)
            {
                BuildingSnapshot saved = snapshot.buildings[i];
                BuildingData data = _registry.GetBuilding(saved.buildingId);
                if (data == null || data.Prefab == null)
                    continue;

                GameObject building = Instantiate(
                    data.Prefab,
                    saved.position.ToVector3(),
                    saved.rotation.ToQuaternion(),
                    root);
                building.name = $"{data.BuildingName} ({saved.team})";
                TeamObjectSetup.AssignTeam(building, saved.team);
                building.GetComponent<BuildingConstructionState>()?.CompleteImmediately();

                BuildingGridOccupancy occupancy = building.GetComponent<BuildingGridOccupancy>();
                if (occupancy == null)
                    occupancy = building.AddComponent<BuildingGridOccupancy>();

                occupancy.Initialize(data, _gridConfig != null ? _gridConfig : occupancy.GridConfig, saved.originCell.ToVector2Int(), saved.rotationSteps);

                BuildingHealth health = building.GetComponent<BuildingHealth>();
                if (health != null)
                    health.SetCurrentHealthForLoad(saved.currentHealth);

                RestoreFactory(building.GetComponent<BuildingProduction>(), saved.factory);
            }
        }

        private void RestoreFactory(BuildingProduction factory, FactorySnapshot snapshot)
        {
            if (factory == null || snapshot == null)
                return;

            ProductionItemData current = _registry.GetProductionItem(snapshot.currentItemId);
            _restoreQueueBuffer.Clear();
            for (int i = 0; i < snapshot.queuedItemIds.Count; i++)
            {
                ProductionItemData queuedItem = _registry.GetProductionItem(snapshot.queuedItemIds[i]);
                if (queuedItem != null)
                    _restoreQueueBuffer.Add(queuedItem);
            }

            factory.RestoreProductionState(current, snapshot.remainingSeconds, _restoreQueueBuffer);
        }

        private void RestoreUnits(SaveGameSnapshot snapshot)
        {
            Transform root = RuntimeObjectContainer.Get("Units");
            for (int i = 0; i < snapshot.units.Count; i++)
            {
                UnitSnapshot saved = snapshot.units[i];
                UnitData data = _registry.GetUnit(saved.unitId);
                if (data == null || data.Prefab == null)
                    continue;

                GameObject unit = Instantiate(data.Prefab, saved.position.ToVector3(), saved.rotation.ToQuaternion(), root);
                TeamObjectSetup.AssignTeam(unit, saved.team);
                TeamObjectSetup.AssignUnitLayer(unit, saved.team);

                UnitCombat combat = unit.GetComponent<UnitCombat>();
                if (combat != null)
                    combat.SetCurrentHealthForLoad(saved.currentHealth);
            }
        }

        private void RestoreOutposts(SaveGameSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.outposts.Count; i++)
            {
                OutpostSnapshot saved = snapshot.outposts[i];
                Outpost outpost = FindOutpost(saved);
                if (outpost == null)
                    continue;

                outpost.RestoreState(new OutpostSaveState(
                    saved.hasOwner,
                    saved.owner,
                    saved.hasCapturingTeam,
                    saved.capturingTeam,
                    saved.captureProgressSeconds,
                    saved.isUpgraded,
                    saved.resourceTimer));
            }
        }

        private static Outpost FindOutpost(OutpostSnapshot snapshot)
        {
            Outpost best = null;
            float bestDistanceSqr = float.MaxValue;
            Vector3 position = snapshot.position.ToVector3();

            for (int i = 0; i < Outpost.All.Count; i++)
            {
                Outpost outpost = Outpost.All[i];
                if (outpost == null)
                    continue;

                float distanceSqr = (outpost.transform.position - position).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                best = outpost;
                bestDistanceSqr = distanceSqr;
            }

            return best;
        }

        private static void Publish(string message)
        {
            Debug.Log(message);
            SaveStatusMessage?.Invoke(message);
        }
    }
}
