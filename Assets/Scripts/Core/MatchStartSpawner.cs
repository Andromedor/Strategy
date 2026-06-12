using System;
using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;
using UnityEngine.AI;

namespace Strategy.Core
{
    [DefaultExecutionOrder(-750)]
    [DisallowMultipleComponent]
    public class MatchStartSpawner : MonoBehaviour
    {
        // Подія потрібна камері та майбутнім системам старту матчу, щоб реагувати на базу без прямої залежності від спавнера.
        public static event Action<GameObject, TeamType> StartingBaseSpawned;
        public static event Action<IReadOnlyList<GameObject>> StartingBasesSpawnCompleted;

        [Header("Start Points")]
        [SerializeField] private List<PlayerStartPoint> _startPoints = new();
        [SerializeField] private bool _useSceneStartPointsWhenListEmpty = true;
        [SerializeField] private bool _spawnOnStart = true;

        [Header("Starting Base")]
        [SerializeField] private BuildingData _startingBaseData;
        [SerializeField] private BuildingPlacementGridConfig _gridConfig;
        [SerializeField] private bool _chargeStartingBaseCost;
        [SerializeField, Min(0)] private int _cellSearchRadius = 3;

        [Header("Starting Units")]
        [SerializeField] private List<StartingUnitEntry> _startingUnits = new();
        [SerializeField, Min(0.5f)] private float _startingUnitSpacing = 5f;

        private readonly List<PlayerStartPoint> _activeStartPoints = new();
        private readonly List<Vector2Int> _occupiedCells = new();
        private readonly List<GameObject> _spawnedBases = new();
        private bool _hasSpawned;

        public IReadOnlyList<GameObject> SpawnedBases => _spawnedBases;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            StartingBaseSpawned = null;
            StartingBasesSpawnCompleted = null;
        }

        private void Start()
        {
            if (_spawnOnStart && !MatchLaunchContext.HasPendingSaveLoad)
                SpawnAll();
        }

        /// <summary>Створює стартові бази й юнітів для всіх активних команд із MatchTeamSettings або MatchLaunchConfig.</summary>
        [ContextMenu("Force Spawn All")]
        public bool SpawnAll()
        {
            if (_hasSpawned)
            {
                Debug.LogWarning("MatchStartSpawner: spawn already completed.");
                return false;
            }

            MatchTeamSettings matchSettings = MatchTeamSettings.Active;
            IReadOnlyList<TeamSlot> teams = matchSettings != null ? matchSettings.Teams : null;

            if (matchSettings == null)
            {
                Debug.LogError("MatchStartSpawner: MatchTeamSettings not found in scene — add it to 'Match Systems' GameObject.");
                return false;
            }

            if (teams == null || teams.Count == 0)
            {
                Debug.LogError("MatchStartSpawner: MatchTeamSettings has no teams configured.");
                return false;
            }

            if (_startingBaseData == null || _startingBaseData.Prefab == null)
            {
                Debug.LogError("MatchStartSpawner: _startingBaseData (MilitaryBase) is not assigned or has no prefab.");
                return false;
            }

            BuildActiveStartPoints(teams.Count);

            if (_activeStartPoints.Count < teams.Count)
            {
                Debug.LogError($"MatchStartSpawner needs {teams.Count} start points, but only {_activeStartPoints.Count} are active (check _enabledForPlayerCounts on PlayerStartPoint).");
                return false;
            }

            _hasSpawned = true;
            _spawnedBases.Clear();

            MatchLaunchConfig launchConfig = MatchLaunchContext.CurrentConfig;

            for (int i = 0; i < teams.Count; i++)
            {
                TeamSlot slot = teams[i];

                if (slot.Team == TeamType.Neutral)
                    continue;

                PlayerStartPoint startPoint = launchConfig != null
                    ? ResolveStartPointForTeam(launchConfig, slot.Team, i)
                    : _activeStartPoints[i];

                if (startPoint == null)
                    continue;

                if (!SpawnBase(slot.Team, startPoint, out GameObject baseObject))
                    continue;

                _spawnedBases.Add(baseObject);
                StartingBaseSpawned?.Invoke(baseObject, slot.Team);
                SpawnStartingUnits(slot.Team, startPoint);
            }

            bool hasSpawnedBases = _spawnedBases.Count > 0;
            if (hasSpawnedBases)
                StartingBasesSpawnCompleted?.Invoke(_spawnedBases);

            return hasSpawnedBases;
        }

        public void ResetSpawnStateForDebug()
        {
            _hasSpawned = false;
            _spawnedBases.Clear();
        }

        /// <summary>Ставить стартову базу на найближчу вільну клітинку grid та одразу завершує її будівництво.</summary>
        private bool SpawnBase(TeamType team, PlayerStartPoint startPoint, out GameObject baseObject)
        {
            baseObject = null;

            if (_chargeStartingBaseCost &&
                ResourceManager.Instance != null &&
                !ResourceManager.Instance.Spend(team, _startingBaseData.EconomyCost))
            {
                return false;
            }

            int rotationSteps = ResolveRotationSteps(startPoint.Rotation);
            Vector2Int originCell = BuildingGridPlacementService.WorldToPlacementOriginCell(
                _startingBaseData,
                startPoint.Position,
                rotationSteps,
                _gridConfig);

            if (!TryFindFreeCell(_startingBaseData, originCell, rotationSteps, out Vector2Int freeCell))
                freeCell = originCell;

            Vector3 position = BuildingGridPlacementService.GetPlacementPosition(
                _startingBaseData,
                freeCell,
                rotationSteps,
                _gridConfig);
            position.y = startPoint.Position.y;

            baseObject = Instantiate(
                _startingBaseData.Prefab,
                position,
                BuildingGridPlacementService.RotationFromSteps(rotationSteps),
                RuntimeObjectContainer.Get("Buildings"));
            baseObject.name = $"{_startingBaseData.BuildingName} ({team})";

            TeamObjectSetup.AssignTeam(baseObject, team);

            BuildingGridOccupancy occupancy = baseObject.GetComponent<BuildingGridOccupancy>();
            if (occupancy == null)
                occupancy = baseObject.AddComponent<BuildingGridOccupancy>();

            occupancy.Initialize(_startingBaseData, _gridConfig, freeCell, rotationSteps);
            baseObject.GetComponent<BuildingConstructionState>()?.CompleteImmediately();
            return true;
        }

        private void SpawnStartingUnits(TeamType team, PlayerStartPoint startPoint)
        {
            if (_startingUnits == null || _startingUnits.Count == 0)
                return;

            int spawnedCount = 0;
            Transform unitsRoot = RuntimeObjectContainer.Get("Units");

            for (int i = 0; i < _startingUnits.Count; i++)
            {
                StartingUnitEntry entry = _startingUnits[i];
                if (entry.UnitData == null || entry.UnitData.Prefab == null)
                    continue;

                for (int count = 0; count < entry.Count; count++)
                {
                    Vector3 offset = entry.LocalOffset + ResolveUnitFormationOffset(spawnedCount++);
                    Vector3 position = startPoint.Position + startPoint.Rotation * offset;

                    if (NavMesh.SamplePosition(position, out NavMeshHit hit, _startingUnitSpacing, NavMesh.AllAreas))
                        position = hit.position;

                    GameObject unit = Instantiate(entry.UnitData.Prefab, position, startPoint.Rotation, unitsRoot);
                    TeamObjectSetup.AssignTeam(unit, team);
                    TeamObjectSetup.AssignUnitLayer(unit, team);
                }
            }
        }

        private void BuildActiveStartPoints(int playerCount)
        {
            _activeStartPoints.Clear();

            IReadOnlyList<PlayerStartPoint> source =
                _startPoints != null && _startPoints.Count > 0 || !_useSceneStartPointsWhenListEmpty
                    ? _startPoints
                    : PlayerStartPoint.All;

            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                PlayerStartPoint startPoint = source[i];
                if (startPoint != null && startPoint.isActiveAndEnabled && startPoint.IsEnabledForPlayerCount(playerCount))
                    _activeStartPoints.Add(startPoint);
            }

            _activeStartPoints.Sort((first, second) => first.SlotIndex.CompareTo(second.SlotIndex));
        }

        private PlayerStartPoint ResolveStartPointForTeam(
            MatchLaunchConfig launchConfig,
            TeamType team,
            int fallbackIndex)
        {
            TeamLaunchSlot slot = launchConfig.FindTeam(team);
            if (slot != null)
            {
                for (int i = 0; i < _activeStartPoints.Count; i++)
                {
                    PlayerStartPoint startPoint = _activeStartPoints[i];
                    if (startPoint != null && startPoint.SlotIndex == slot.SpawnSlotIndex)
                        return startPoint;
                }
            }

            return fallbackIndex >= 0 && fallbackIndex < _activeStartPoints.Count
                ? _activeStartPoints[fallbackIndex]
                : null;
        }

        private bool TryFindFreeCell(
            BuildingData buildingData,
            Vector2Int preferredCell,
            int rotationSteps,
            out Vector2Int freeCell)
        {
            freeCell = preferredCell;
            int searchRadius = Mathf.Max(0, _cellSearchRadius);

            for (int radius = 0; radius <= searchRadius; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                            continue;

                        Vector2Int candidate = preferredCell + new Vector2Int(x, y);

                        if (AreCellsFree(buildingData, candidate, rotationSteps))
                        {
                            freeCell = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool AreCellsFree(BuildingData buildingData, Vector2Int originCell, int rotationSteps)
        {
            _occupiedCells.Clear();
            BuildingGridPlacementService.GetOccupiedCells(
                buildingData,
                originCell,
                rotationSteps,
                _gridConfig,
                _occupiedCells);

            for (int i = 0; i < _occupiedCells.Count; i++)
            {
                if (BuildingGridPlacementService.IsReservedByOther(_occupiedCells[i], null))
                    return false;
            }

            return true;
        }

        private Vector3 ResolveUnitFormationOffset(int index)
        {
            int row = index / 4;
            int column = index % 4;
            float x = (column - 1.5f) * _startingUnitSpacing;
            float z = 12f + row * _startingUnitSpacing;
            return new Vector3(x, 0f, z);
        }

        private static int ResolveRotationSteps(Quaternion rotation)
        {
            return BuildingGridPlacementService.NormalizeRotationSteps(
                Mathf.RoundToInt(rotation.eulerAngles.y / 90f));
        }
    }
}
