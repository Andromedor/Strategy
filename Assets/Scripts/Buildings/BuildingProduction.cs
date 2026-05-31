using System;
using System.Collections;
using System.Collections.Generic;
using Strategy.Core;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Buildings
{
    /// <summary>
    /// Будівля-завод, що ставить юнітів у чергу та виробляє їх по одному, витрачаючи ресурси гравця за кожен елемент.
    /// Виготовлені юніти самостійно виїжджають через браму заводу за допомогою UnitSpawnActivator перед тим, як стати активними.
    /// </summary>
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

        /// <summary>
        /// Перевіряє елемент, витрачає ресурси гравця якщо застосовно, ставить юніта у чергу виробництва
        /// та запускає корутину ProcessQueue, якщо вона ще не виконується.
        /// </summary>
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

        /// <summary>Виймає елементи з черги по одному, очікує час виробництва кожного юніта, після чого викликає SpawnAndReleaseUnit.</summary>
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

        /// <summary>
        /// Створює префаб юніта у точці спавну у вимкненому стані, відкриває браму,
        /// переміщує юніта до точки виходу через UnitSpawnActivator, після чого закриває браму.
        /// </summary>
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

        /// <summary>Копіює команду заводу на TeamComponent щойно створеного юніта та призначає правильну маску шару.</summary>
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

        /// <summary>Переводить юніта у стан спавну (вимкнений) через UnitSpawnActivator перед виходом із заводу.</summary>
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
