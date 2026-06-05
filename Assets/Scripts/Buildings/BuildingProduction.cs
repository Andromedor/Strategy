using System;
using System.Collections;
using System.Collections.Generic;
using Strategy.Core;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;
using UnityEngine.AI;

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

        [Header("Rally Point")]
        [SerializeField] private Transform _unitExitPoint;

        [Header("Factory Exit")]
        [SerializeField] private Transform _factoryExitClearPoint;
        [SerializeField, Min(0.5f)] private float _factoryExitClearDistance = 7f;

        [Header("Rally Formation")]
        [SerializeField, Min(0.5f)]
        private float _rallySlotSpacing = 5.75f;
        [SerializeField, Min(1)]
        private int _rallySlotSearchRows = 4;
        [SerializeField, Min(1)] private int _rallySlotsPerRow = 6;
        [SerializeField, Min(0.05f)]
        private float _rallySlotSampleRadius = 3f;
        [SerializeField, Min(0f)]
        private float _rallyClearancePadding = 0.75f;
        [SerializeField]
        private LayerMask _rallyBlockerMask;

        [Header("Production")]
        [SerializeField] private ProductionConfig _productionConfig;

        [Header("Gate")]
        [SerializeField] private FactoryGate _gate;

        private readonly Queue<ProductionItemData> _queue = new();
        private readonly Collider[] _rallyOverlapBuffer = new Collider[24];
        private readonly List<Vector3> _reservedRallyDestinations = new();
        private TeamComponent _teamComponent;
        private bool _isProducing;
        private ProductionItemData _currentProductionItem;
        private float _currentProductionDuration;
        private float _currentProductionElapsed;

        public static readonly List<BuildingProduction> All = new();
        public static event Action FactoriesChanged;

        public IReadOnlyList<ProductionItemData> Items => _productionConfig != null
            ? _productionConfig.Items
            : System.Array.Empty<ProductionItemData>();

        public int QueuedCount => _queue.Count;
        public bool IsProducing => _isProducing;
        public int PendingWorkCount => _queue.Count + (_currentProductionItem != null ? 1 : 0);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            All.Clear();
            FactoriesChanged = null;
        }

        private void Awake()
        {
            _teamComponent = GetComponent<TeamComponent>();

            if (_rallyBlockerMask.value == 0)
                _rallyBlockerMask = LayerMask.GetMask("PlayerUnit", "EnemyUnit");
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
            ClearCurrentProductionState();
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

        public bool CanProduce(ProductionItemData item)
        {
            return TryResolveProductionItem(item, out _);
        }

        public bool TryResolveProductionItem(ProductionItemData requestedItem, out ProductionItemData productionItem)
        {
            productionItem = null;

            if (!IsValidProductionItem(requestedItem))
                return false;

            IReadOnlyList<ProductionItemData> items = Items;
            for (int i = 0; i < items.Count; i++)
            {
                ProductionItemData item = items[i];
                if (item == requestedItem)
                {
                    productionItem = item;
                    return true;
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                ProductionItemData item = items[i];
                if (IsEquivalentProductionItem(item, requestedItem))
                {
                    productionItem = item;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetCurrentProduction(out FactoryProductionRuntimeState state)
        {
            state = default;

            if (_currentProductionItem == null)
                return false;

            float duration = Mathf.Max(0f, _currentProductionDuration);
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(_currentProductionElapsed / duration);
            float remainingSeconds = duration <= 0f ? 0f : Mathf.Max(0f, duration - _currentProductionElapsed);

            state = new FactoryProductionRuntimeState(
                this,
                _currentProductionItem,
                progress,
                remainingSeconds,
                duration);
            return true;
        }

        public bool TryGetActiveProductionFor(
            ProductionItemData item,
            out FactoryProductionRuntimeState state)
        {
            if (!TryGetCurrentProduction(out state))
                return false;

            if (AreEquivalentProductionItems(state.Item, item))
                return true;

            state = default;
            return false;
        }

        public int CountPendingWorkFor(ProductionItemData item)
        {
            if (!IsValidProductionItem(item))
                return 0;

            int count = 0;

            if (AreEquivalentProductionItems(_currentProductionItem, item))
                count++;

            foreach (ProductionItemData queuedItem in _queue)
            {
                if (AreEquivalentProductionItems(queuedItem, item))
                    count++;
            }

            return count;
        }

        public static bool AreEquivalentProductionItems(
            ProductionItemData first,
            ProductionItemData second)
        {
            if (!IsValidProductionItem(first) || !IsValidProductionItem(second))
                return false;

            if (first == second || first.UnitData == second.UnitData)
                return true;

            return first.UnitData.Prefab == second.UnitData.Prefab;
        }

        private static bool IsValidProductionItem(ProductionItemData item)
        {
            return item != null && item.UnitData != null && item.UnitData.Prefab != null;
        }

        private static bool IsEquivalentProductionItem(
            ProductionItemData availableItem,
            ProductionItemData requestedItem)
        {
            if (!IsValidProductionItem(availableItem) || !IsValidProductionItem(requestedItem))
                return false;

            return AreEquivalentProductionItems(availableItem, requestedItem);
        }

        /// <summary>Виймає елементи з черги по одному, очікує час виробництва кожного юніта, після чого викликає SpawnAndReleaseUnit.</summary>
        private IEnumerator ProcessQueue()
        {
            _isProducing = true;

            while (_queue.Count > 0)
            {
                ProductionItemData item = _queue.Dequeue();
                _currentProductionItem = item;
                _currentProductionDuration = Mathf.Max(0f, item.ProductionTime);
                _currentProductionElapsed = 0f;

                while (_currentProductionElapsed < _currentProductionDuration)
                {
                    _currentProductionElapsed = Mathf.Min(
                        _currentProductionDuration,
                        _currentProductionElapsed + Time.deltaTime);
                    yield return null;
                }

                yield return StartCoroutine(SpawnAndReleaseUnit(item));
                ClearCurrentProductionState();
            }

            _isProducing = false;
        }

        private void ClearCurrentProductionState()
        {
            _currentProductionItem = null;
            _currentProductionDuration = 0f;
            _currentProductionElapsed = 0f;
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
            UnitTrafficCoordinator.Ensure(spawnedUnit);
            DisableUnitBeforeExit(spawnedUnit);

            if (_gate != null)
                yield return StartCoroutine(_gate.Open());

            UnitSpawnActivator activator = spawnedUnit.GetComponent<UnitSpawnActivator>();

            if (activator != null && _unitExitPoint != null)
            {
                Vector3 rallyDestination = ResolveRallyDestination(spawnedUnit);
                yield return StartCoroutine(activator.MoveOutOfFactory(
                    ResolveFactoryExitClearPoint(rallyDestination),
                    rallyDestination,
                    ReleaseRallyDestination));
            }
            else if (activator != null)
            {
                activator.SetSpawningState(false);
            }

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
                SetLayerRecursively(spawnedUnit.transform, layer);
        }

        private static void SetLayerRecursively(Transform target, int layer)
        {
            if (target == null)
                return;

            target.gameObject.layer = layer;

            foreach (Transform child in target)
                SetLayerRecursively(child, layer);
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

        /// <summary>
        /// Підбирає вільний слот у рядку біля rally point, щоб виготовлені юніти не займали одну координату.
        /// </summary>
        private Vector3 ResolveRallyDestination(GameObject spawnedUnit)
        {
            if (_unitExitPoint == null)
                return _unitSpawnPoint != null ? _unitSpawnPoint.position : transform.position;

            NavMeshAgent agent = spawnedUnit != null ? spawnedUnit.GetComponent<NavMeshAgent>() : null;
            float spacing = ResolveRallySlotSpacing(agent);
            float clearanceRadius = ResolveRallyClearanceRadius(agent);
            Vector3 forward = GetRallyApproachDirection();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            int slotsPerRow = Mathf.Max(1, _rallySlotsPerRow);

            // Слоти перебираються від центру rally point у рядок і далі рядами вперед, щоб нові юніти ставали поруч, а не в одну координату.
            for (int row = 0; row < _rallySlotSearchRows; row++)
            {
                for (int slot = 0; slot < slotsPerRow; slot++)
                {
                    Vector3 candidate = _unitExitPoint.position +
                                        GetRallyLineOffset(row, slot, spacing, forward, right);

                    if (TryResolveRallyCandidate(
                            candidate,
                            spawnedUnit,
                            agent,
                            clearanceRadius,
                            out Vector3 resolvedPoint))
                    {
                        ReserveRallyDestination(resolvedPoint);
                        return resolvedPoint;
                    }
                }
            }

            if (TryResolveRallyCandidate(
                    _unitExitPoint.position,
                    spawnedUnit,
                    agent,
                    clearanceRadius,
                    out Vector3 fallbackPoint))
            {
                ReserveRallyDestination(fallbackPoint);
                return fallbackPoint;
            }

            ReserveRallyDestination(_unitExitPoint.position);
            return _unitExitPoint.position;
        }

        private Vector3 ResolveFactoryExitClearPoint(Vector3 rallyDestination)
        {
            if (_factoryExitClearPoint != null)
                return _factoryExitClearPoint.position;

            Vector3 startPoint = _unitSpawnPoint != null ? _unitSpawnPoint.position : transform.position;
            Vector3 direction = rallyDestination - startPoint;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.01f)
                direction = transform.forward;

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.01f)
                direction = Vector3.forward;

            direction.Normalize();
            float rallyDistance = Vector3.Distance(
                new Vector3(startPoint.x, 0f, startPoint.z),
                new Vector3(rallyDestination.x, 0f, rallyDestination.z));
            float clearDistance = Mathf.Min(
                Mathf.Max(0.5f, _factoryExitClearDistance),
                Mathf.Max(0.5f, rallyDistance));

            return startPoint + direction * clearDistance;
        }

        private bool TryResolveRallyCandidate(
            Vector3 candidate,
            GameObject spawnedUnit,
            NavMeshAgent agent,
            float clearanceRadius,
            out Vector3 resolvedPoint)
        {
            resolvedPoint = candidate;

            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _rallySlotSampleRadius, areaMask))
                return false;

            resolvedPoint = hit.position;
            return !IsRallySlotBlocked(spawnedUnit, resolvedPoint, clearanceRadius);
        }

        private bool IsRallySlotBlocked(GameObject spawnedUnit, Vector3 point, float clearanceRadius)
        {
            if (IsRallySlotReserved(point, clearanceRadius))
                return true;

            // Локальні бронювання заводу не бачать накази інших систем, тому додатково перевіряємо глобальні бронювання цілей.
            if (UnitDestinationReservations.IsReservedByOther(spawnedUnit, point, clearanceRadius))
                return true;

            if (_rallyBlockerMask.value == 0)
                return false;

            int hitCount = Physics.OverlapSphereNonAlloc(
                point + Vector3.up * 0.5f,
                clearanceRadius,
                _rallyOverlapBuffer,
                _rallyBlockerMask,
                QueryTriggerInteraction.Ignore);

            Transform spawnedRoot = spawnedUnit != null ? spawnedUnit.transform.root : null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _rallyOverlapBuffer[i];

                if (hit == null || !hit.enabled)
                    continue;

                if (spawnedRoot != null && hit.transform.root == spawnedRoot)
                    continue;

                return true;
            }

            return false;
        }

        private void ReserveRallyDestination(Vector3 destination)
        {
            _reservedRallyDestinations.Add(destination);
        }

        private void ReleaseRallyDestination(Vector3 destination)
        {
            int closestIndex = -1;
            float closestDistanceSqr = float.MaxValue;
            float releaseRadius = Mathf.Max(1f, _rallySlotSpacing);
            float releaseRadiusSqr = releaseRadius * releaseRadius;

            for (int i = 0; i < _reservedRallyDestinations.Count; i++)
            {
                Vector3 offset = _reservedRallyDestinations[i] - destination;
                offset.y = 0f;
                float distanceSqr = offset.sqrMagnitude;

                if (distanceSqr > releaseRadiusSqr || distanceSqr >= closestDistanceSqr)
                    continue;

                closestDistanceSqr = distanceSqr;
                closestIndex = i;
            }

            if (closestIndex >= 0)
                _reservedRallyDestinations.RemoveAt(closestIndex);
        }

        private bool IsRallySlotReserved(Vector3 point, float clearanceRadius)
        {
            float reservedDistance = Mathf.Max(clearanceRadius * 2f, _rallySlotSpacing);
            float reservedDistanceSqr = reservedDistance * reservedDistance;

            foreach (Vector3 reservedDestination in _reservedRallyDestinations)
            {
                Vector3 offset = reservedDestination - point;
                offset.y = 0f;

                if (offset.sqrMagnitude <= reservedDistanceSqr)
                    return true;
            }

            return false;
        }

        private Vector3 GetRallyApproachDirection()
        {
            if (_unitSpawnPoint != null && _unitExitPoint != null)
            {
                Vector3 spawnToExit = _unitExitPoint.position - _unitSpawnPoint.position;
                spawnToExit.y = 0f;

                if (spawnToExit.sqrMagnitude > 0.01f)
                    return spawnToExit.normalized;
            }

            Vector3 forward = _unitExitPoint != null ? _unitExitPoint.forward : transform.forward;
            forward.y = 0f;

            return forward.sqrMagnitude > 0.01f ? forward.normalized : transform.forward;
        }

        private static Vector3 GetRallyLineOffset(
            int row,
            int slot,
            float spacing,
            Vector3 forward,
            Vector3 right)
        {
            // Порядок 0, -1, +1, -2, +2 робить формацію симетричною відносно rally point і не зміщує всю чергу в один бік.
            int sideStep = (slot + 1) / 2;
            int side = slot == 0 ? 0 : slot % 2 == 1 ? -1 : 1;
            float lateral = side * sideStep * spacing;
            float depth = row * spacing;

            return right * lateral + forward * depth;
        }

        private float ResolveRallySlotSpacing(NavMeshAgent agent)
        {
            float agentDiameter = agent != null ? agent.radius * 2f : 0f;
            return Mathf.Max(_rallySlotSpacing, agentDiameter + _rallyClearancePadding);
        }

        private float ResolveRallyClearanceRadius(NavMeshAgent agent)
        {
            float agentRadius = agent != null ? agent.radius : _rallySlotSpacing * 0.5f;
            return Mathf.Max(0.1f, agentRadius + _rallyClearancePadding);
        }
    }
}
