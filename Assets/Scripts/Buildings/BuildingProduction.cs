using System;
using System.Collections;
using System.Collections.Generic;
using Strategy.Core;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Buildings
{
    public class BuildingProduction : MonoBehaviour
    {
        [Header("Spawn Points")]
        [SerializeField] private Transform _unitSpawnPoint;
        [SerializeField] private Transform _unitExitPoint;

        [Header("Production")]
        [SerializeField] private ProductionConfig _productionConfig;

        [Header("Gate")]
        [SerializeField] private FactoryGate _gate;

        private readonly Queue<ProductionItemData> _queue = new();
        private TeamComponent _teamComponent;
        private bool _isProducing;

        public static readonly List<BuildingProduction> All = new();
        public static event Action FactoriesChanged;

        public IReadOnlyList<ProductionItemData> Items => _productionConfig != null
            ? _productionConfig.Items
            : System.Array.Empty<ProductionItemData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            All.Clear();
            FactoriesChanged = null;
        }

        private void Awake()
        {
            _teamComponent = GetComponent<TeamComponent>();
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);

            FactoriesChanged?.Invoke();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _isProducing = false;
            All.Remove(this);
            FactoriesChanged?.Invoke();
        }

        public bool AddToQueue(ProductionItemData item)
        {
            if (item == null || item.UnitData == null || item.UnitData.Prefab == null)
                return false;

            if (_unitSpawnPoint == null)
                return false;

            if (_teamComponent != null &&
                _teamComponent.Team == TeamType.Player &&
                ResourceManager.Instance != null &&
                !ResourceManager.Instance.Spend(item.Cost))
            {
                return false;
            }

            _queue.Enqueue(item);

            if (!_isProducing && isActiveAndEnabled)
                StartCoroutine(ProcessQueue());

            return true;
        }

        private IEnumerator ProcessQueue()
        {
            _isProducing = true;

            while (_queue.Count > 0)
            {
                ProductionItemData item = _queue.Dequeue();

                yield return new WaitForSeconds(Mathf.Max(0f, item.ProductionTime));

                yield return StartCoroutine(SpawnAndReleaseUnit(item));
            }

            _isProducing = false;
        }

        private IEnumerator SpawnAndReleaseUnit(ProductionItemData item)
        {
            UnitData unitData = item.UnitData;
            Transform unitsContainer = RuntimeObjectContainer.Get("Units");

            GameObject spawnedUnit = Instantiate(
                unitData.Prefab,
                _unitSpawnPoint.position,
                _unitSpawnPoint.rotation,
                unitsContainer);

            SetupUnitTeam(spawnedUnit);
            DisableUnitBeforeExit(spawnedUnit);

            if (_gate != null)
                yield return StartCoroutine(_gate.Open());

            UnitSpawnActivator activator = spawnedUnit.GetComponent<UnitSpawnActivator>();

            if (activator != null && _unitExitPoint != null)
                yield return StartCoroutine(activator.MoveOutOfFactory(_unitExitPoint.position));
            else if (activator != null)
                activator.SetSpawningState(false);

            if (_gate != null)
                yield return StartCoroutine(_gate.Close());
        }

        private void SetupUnitTeam(GameObject spawnedUnit)
        {
            if (spawnedUnit == null || _teamComponent == null)
                return;

            TeamComponent unitTeam = spawnedUnit.GetComponent<TeamComponent>();

            if (unitTeam == null)
                return;

            unitTeam.SetTeam(_teamComponent.Team);

            int layer = LayerMask.NameToLayer(
                _teamComponent.Team == TeamType.Player ? "PlayerUnit" : "EnemyUnit");

            if (layer >= 0)
                spawnedUnit.layer = layer;
        }

        private static void DisableUnitBeforeExit(GameObject spawnedUnit)
        {
            if (spawnedUnit == null)
                return;

            UnitSpawnActivator activator = spawnedUnit.GetComponent<UnitSpawnActivator>();

            if (activator != null)
                activator.SetSpawningState(true);
        }
    }
}
