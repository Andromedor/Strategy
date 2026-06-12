using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Core;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;

namespace Strategy.AI
{
    [DisallowMultipleComponent]
    public class AiController : MonoBehaviour, ISimulationTickable
    {
        [Header("Team")]
        [SerializeField] private TeamType _team = TeamType.Enemy;
        [SerializeField, Min(0)] private int _playerId = 1;

        [Header("Profile")]
        [SerializeField] private AiDifficultyProfile _profile;

        [Header("Build")]
        [SerializeField] private BuildingData _factoryData;
        [SerializeField] private BuildingPlacementGridConfig _gridConfig;
        [SerializeField] private LayerMask _buildingBlockMask;
        [SerializeField, Min(0)] private int _buildSearchRadiusCells = 8;

        private readonly List<UnitCombat> _ownUnits = new();
        private readonly List<GameObject> _unitObjects = new();
        private readonly List<GameObject> _squadObjects = new();
        private readonly List<BuildingProduction> _ownFactories = new();
        private readonly List<GameObject> _factoryObjects = new();
        private readonly List<ConstructionCenter> _ownConstructionCenters = new();
        private readonly List<BuildingHealth> _ownBuildings = new();
        private readonly List<ProductionItemData> _availableProductionItems = new();

        private float _nextAttackTime;
        private float _nextCaptureTime;
        private float _nextBuildTime;
        private bool _registered;

        public TeamType Team => _team;
        public AiDifficultyProfile Profile => ResolveProfile();

        private void OnEnable()
        {
            RegisterTick();
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GameTickRunner.Unregister(this);
                _registered = false;
            }
        }

        public void Initialize(
            TeamType team,
            int playerId,
            AiDifficultyProfile profile,
            BuildingData factoryData,
            BuildingPlacementGridConfig gridConfig,
            LayerMask buildingBlockMask,
            int buildSearchRadiusCells)
        {
            if (_registered)
                GameTickRunner.Unregister(this);

            _team = team;
            _playerId = Mathf.Max(0, playerId);
            _profile = profile;
            _factoryData = factoryData;
            _gridConfig = gridConfig;
            _buildingBlockMask = buildingBlockMask;
            _buildSearchRadiusCells = Mathf.Max(0, buildSearchRadiusCells);

            _registered = false;
            RegisterTick();
        }

        public void Tick(GameTickContext context)
        {
            if (!AiRuntimeSettings.IsAiEnabled)
                return;

            if (_team == TeamType.Neutral || !IsConfiguredAiTeam())
                return;

            RefreshWorldState();

            if (_ownBuildings.Count == 0)
                return;

            TryUpgradeOutpost();
            TryBuildFactory(context.SimulationTime);
            TryProduceUnit();

            if (TryDefend(context.SimulationTime))
                return;

            if (TryCaptureOutpost(context.SimulationTime))
                return;

            TryAttackEnemyBuildings(context.SimulationTime);
        }

        private void RegisterTick()
        {
            if (_registered || !isActiveAndEnabled)
                return;

            GameTickRunner.Register(this, ResolveProfile().DecisionInterval);
            _registered = true;
        }

        private AiDifficultyProfile ResolveProfile()
        {
            if (_profile != null)
                return _profile;

            _profile = AiDifficultyProfile.CreateRuntimeDefault(AiDifficultyLevel.Medium);
            return _profile;
        }

        private bool IsConfiguredAiTeam()
        {
            MatchTeamSettings matchSettings = MatchTeamSettings.Active;

            if (matchSettings == null)
                return true;

            IReadOnlyList<TeamSlot> teams = matchSettings.Teams;
            for (int i = 0; i < teams.Count; i++)
            {
                TeamSlot slot = teams[i];
                if (slot.Team == _team)
                    return slot.Controller == TeamControllerKind.AI;
            }

            return false;
        }

        private void RefreshWorldState()
        {
            _ownUnits.Clear();
            _unitObjects.Clear();
            _ownFactories.Clear();
            _factoryObjects.Clear();
            _ownConstructionCenters.Clear();
            _ownBuildings.Clear();
            _availableProductionItems.Clear();

            for (int i = 0; i < UnitCombat.All.Count; i++)
            {
                UnitCombat unit = UnitCombat.All[i];
                if (unit == null || unit.IsDead || unit.Team != _team)
                    continue;

                UnitSpawnActivator spawnActivator = unit.GetComponent<UnitSpawnActivator>();
                if (spawnActivator != null && spawnActivator.IsSpawning)
                    continue;

                _ownUnits.Add(unit);
                _unitObjects.Add(unit.gameObject);
            }

            for (int i = 0; i < BuildingProduction.All.Count; i++)
            {
                BuildingProduction factory = BuildingProduction.All[i];
                if (factory == null || !factory.isActiveAndEnabled || !BelongsToTeam(factory.gameObject))
                    continue;

                _ownFactories.Add(factory);
                _factoryObjects.Add(factory.gameObject);
                AddProductionItems(factory);
            }

            for (int i = 0; i < ConstructionCenter.All.Count; i++)
            {
                ConstructionCenter center = ConstructionCenter.All[i];
                if (center != null && center.isActiveAndEnabled && BelongsToTeam(center.gameObject))
                    _ownConstructionCenters.Add(center);
            }

            for (int i = 0; i < BuildingHealth.All.Count; i++)
            {
                BuildingHealth building = BuildingHealth.All[i];
                if (building != null && !building.IsDead && BelongsToTeam(building.gameObject))
                    _ownBuildings.Add(building);
            }
        }

        private void AddProductionItems(BuildingProduction factory)
        {
            IReadOnlyList<ProductionItemData> items = factory.Items;

            for (int i = 0; i < items.Count; i++)
            {
                ProductionItemData item = items[i];
                if (item == null || _availableProductionItems.Contains(item))
                    continue;

                _availableProductionItems.Add(item);
            }
        }

        private void TryUpgradeOutpost()
        {
            if (ResourceManager.Instance == null)
                return;

            AiDifficultyProfile profile = ResolveProfile();

            for (int i = 0; i < Outpost.All.Count; i++)
            {
                Outpost outpost = Outpost.All[i];
                if (outpost == null || outpost.Owner != _team || outpost.IsUpgraded)
                    continue;

                int requiredResource = Mathf.CeilToInt(outpost.UpgradeCost * profile.OutpostUpgradeResourceMultiplier);
                if (ResourceManager.Instance.GetResource(_team) < requiredResource)
                    continue;

                PlayerCommand command = PlayerCommand.UpgradeOutpost(_team, _playerId, outpost.transform);
                CommandDispatcher.Dispatch(command, PlayerCommandExecutor.Execute);
                return;
            }
        }

        private void TryBuildFactory(float simulationTime)
        {
            AiDifficultyProfile profile = ResolveProfile();

            if (_factoryData == null || _factoryData.Prefab == null ||
                _ownFactories.Count >= profile.DesiredFactoryCount ||
                _ownConstructionCenters.Count == 0 ||
                simulationTime < _nextBuildTime)
            {
                return;
            }

            if (ResourceManager.Instance != null &&
                ResourceManager.Instance.GetResource(_team) < _factoryData.EconomyCost + profile.ResourceReserve)
            {
                return;
            }

            ConstructionCenter center = _ownConstructionCenters[0];
            Vector3 targetPosition = center.Position + center.transform.forward * Mathf.Min(12f, center.BuildRadius * 0.5f);
            PlayerCommand command = PlayerCommand.BuildStructure(_team, _playerId, _factoryData, targetPosition);

            CommandDispatcher.Dispatch(
                command,
                executed =>
                {
                    if (BuildingCommandExecutor.TryExecuteBuildStructure(
                            executed,
                            _gridConfig,
                            _buildingBlockMask,
                            _buildSearchRadiusCells,
                            out _))
                    {
                        _nextBuildTime = simulationTime + profile.BuildCooldown;
                    }
                });
        }

        private void TryProduceUnit()
        {
            AiDifficultyProfile profile = ResolveProfile();

            if (_ownFactories.Count == 0 || _availableProductionItems.Count == 0)
                return;

            int pendingLimit = _ownFactories.Count * profile.MaxPendingWorkPerFactory;
            int currentPending = 0;
            for (int i = 0; i < _ownFactories.Count; i++)
                currentPending += _ownFactories[i].PendingWorkCount;

            if (currentPending >= pendingLimit)
                return;

            ProductionItemData item = profile.SelectProductionItem(_availableProductionItems);
            if (item == null)
                return;

            if (ResourceManager.Instance != null &&
                ResourceManager.Instance.GetResource(_team) < item.Cost + profile.ResourceReserve)
            {
                return;
            }

            PlayerCommand command = PlayerCommand.ProduceUnit(_team, _playerId, _factoryObjects, item);
            CommandDispatcher.Dispatch(command, PlayerCommandExecutor.Execute);
        }

        private bool TryDefend(float simulationTime)
        {
            if (_ownUnits.Count == 0 || _ownBuildings.Count == 0)
                return false;

            Transform threat = FindClosestThreatNearOwnBuilding(ResolveProfile().DefenseRadius);
            if (threat == null)
                return false;

            SelectClosestUnits(threat.position, Mathf.Min(_ownUnits.Count, ResolveProfile().AttackGroupSize), _squadObjects);
            DispatchAttack(threat, _squadObjects);
            _nextAttackTime = Mathf.Max(_nextAttackTime, simulationTime + 2f);
            return true;
        }

        private bool TryCaptureOutpost(float simulationTime)
        {
            AiDifficultyProfile profile = ResolveProfile();

            if (_ownUnits.Count == 0 ||
                simulationTime < _nextCaptureTime ||
                Outpost.GetOwnedCount(_team) >= profile.MinimumOwnedOutposts)
            {
                return false;
            }

            Outpost target = FindBestOutpostTarget();
            if (target == null)
                return false;

            SelectClosestUnits(target.transform.position, Mathf.Min(_ownUnits.Count, profile.CaptureSquadSize), _squadObjects);

            if (_squadObjects.Count == 0)
                return false;

            PlayerCommand command = PlayerCommand.MoveUnits(_team, _playerId, _squadObjects, target.transform.position);
            CommandDispatcher.Dispatch(command, PlayerCommandExecutor.Execute);
            _nextCaptureTime = simulationTime + profile.CaptureCooldown;
            return true;
        }

        private void TryAttackEnemyBuildings(float simulationTime)
        {
            AiDifficultyProfile profile = ResolveProfile();

            if (_ownUnits.Count < profile.AttackGroupSize || simulationTime < _nextAttackTime)
                return;

            BuildingHealth target = AiTargetSelector.FindBestBuildingTarget(_team, profile.FocusHighValueBuildings);
            if (target == null)
                return;

            SelectClosestUnits(target.transform.position, profile.AttackGroupSize, _squadObjects);
            DispatchAttack(target.transform, _squadObjects);
            _nextAttackTime = simulationTime + profile.AttackCooldown;
        }

        private Transform FindClosestThreatNearOwnBuilding(float radius)
        {
            float radiusSqr = radius * radius;
            UnitCombat bestThreat = null;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < UnitCombat.All.Count; i++)
            {
                UnitCombat candidate = UnitCombat.All[i];
                if (candidate == null || candidate.IsDead || !TeamRelations.AreHostile(_team, candidate.Team))
                    continue;

                for (int b = 0; b < _ownBuildings.Count; b++)
                {
                    BuildingHealth building = _ownBuildings[b];
                    if (building == null)
                        continue;

                    float distanceSqr = (candidate.transform.position - building.transform.position).sqrMagnitude;
                    if (distanceSqr > radiusSqr || distanceSqr >= bestDistanceSqr)
                        continue;

                    bestThreat = candidate;
                    bestDistanceSqr = distanceSqr;
                }
            }

            return bestThreat != null ? bestThreat.transform : null;
        }

        private Outpost FindBestOutpostTarget()
        {
            Outpost best = null;
            float bestDistanceSqr = float.MaxValue;
            Vector3 origin = ResolveArmyOrigin();

            for (int i = 0; i < Outpost.All.Count; i++)
            {
                Outpost outpost = Outpost.All[i];
                if (outpost == null)
                    continue;

                if (outpost.Owner != null && TeamRelations.AreAllied(_team, outpost.Owner.Value))
                    continue;

                float distanceSqr = (outpost.transform.position - origin).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                best = outpost;
                bestDistanceSqr = distanceSqr;
            }

            return best;
        }

        private Vector3 ResolveArmyOrigin()
        {
            if (_ownUnits.Count > 0)
                return _ownUnits[0].transform.position;

            if (_ownBuildings.Count > 0)
                return _ownBuildings[0].transform.position;

            return transform.position;
        }

        private void SelectClosestUnits(Vector3 targetPosition, int maxCount, List<GameObject> results)
        {
            results.Clear();
            maxCount = Mathf.Max(0, maxCount);

            while (results.Count < maxCount)
            {
                UnitCombat bestUnit = null;
                float bestDistanceSqr = float.MaxValue;

                for (int i = 0; i < _ownUnits.Count; i++)
                {
                    UnitCombat unit = _ownUnits[i];
                    if (unit == null || results.Contains(unit.gameObject))
                        continue;

                    float distanceSqr = (unit.transform.position - targetPosition).sqrMagnitude;
                    if (distanceSqr >= bestDistanceSqr)
                        continue;

                    bestUnit = unit;
                    bestDistanceSqr = distanceSqr;
                }

                if (bestUnit == null)
                    break;

                results.Add(bestUnit.gameObject);
            }
        }

        private void DispatchAttack(Transform target, IReadOnlyList<GameObject> units)
        {
            if (target == null || units == null || units.Count == 0)
                return;

            PlayerCommand command = PlayerCommand.AttackTarget(_team, _playerId, units, target);
            CommandDispatcher.Dispatch(command, PlayerCommandExecutor.Execute);
        }

        private bool BelongsToTeam(GameObject target)
        {
            TeamComponent teamComponent = target != null ? target.GetComponentInParent<TeamComponent>() : null;
            return teamComponent != null && teamComponent.Team == _team;
        }
    }
}
