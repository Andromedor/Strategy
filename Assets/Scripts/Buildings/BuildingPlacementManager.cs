using System.Collections.Generic;
using Strategy.Core;
using Strategy.Buildings;
using Strategy.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using Strategy.Data;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Керує інтерактивним процесом розміщення будівель: створює примарний об'єкт попереднього перегляду, що
    /// слідує за курсором, перевіряє розміщення відносно зон будівництва та на перетин,
    /// обробляє обертання клавішами Q/E, підтверджує або скасовує розміщення за кліком миші.
    /// </summary>
    public class BuildingPlacementManager : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private UnityEngine.Camera _camera;
        [SerializeField] private LayerMask _groundMask;

        [Header("Team")]
        [SerializeField] private TeamType _currentTeam;

        [Header("Placement check")]
        [SerializeField] private LayerMask _blockMask;

        [Header("Grid")]
        [SerializeField] private BuildingPlacementGridConfig _gridConfig;
        [SerializeField] private bool _useGridPlacement = true;

        [Header("Rotation")]
        [SerializeField] private float _rotationStep = 90f;

        [Header("Preview Colors")]
        [SerializeField] private Color _validColor = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);

        public static bool IsPlacing { get; private set; }
        private bool _isValidPlacement;
        private bool _canPlaceClick;

        private GameObject _previewObject;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;
        private BuildingData _currentBuildingData;
        private ConstructionCenter _currentConstructionCenter;
        private Vector2Int _currentGridCell;
        private int _rotationSteps;
        private readonly List<BehaviourState> _previewBehaviourStates = new();
        private readonly List<ColliderState> _previewColliderStates = new();
        private readonly List<RigidbodyState> _previewRigidbodyStates = new();
        private readonly List<Vector2Int> _gridCellBuffer = new();
        private readonly List<Vector3> _gridCellCenterBuffer = new();
        private readonly List<Vector2Int> _invalidGridCellBuffer = new();
        private readonly List<Vector3> _buildAreaGridCellCenters = new();
        private readonly List<GridMarkerState> _gridMarkers = new();
        private readonly List<GridMarkerState> _buildAreaGridMarkers = new();
        private MaterialPropertyBlock _gridMarkerPropertyBlock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsPlacing = false;
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = UnityEngine.Camera.main;
        }

        private void OnEnable()
        {
            EventManager.OnConstructionCenterSelected += SetConstructionCenter;
            EventManager.OnConstructionClosed += ClearConstructionCenter;
        }

        private void OnDisable()
        {
            EventManager.OnConstructionCenterSelected -= SetConstructionCenter;
            EventManager.OnConstructionClosed -= ClearConstructionCenter;
        }

        /// <summary>Зберігає активний ConstructionCenter, якщо він належить поточній команді; інакше очищає посилання.</summary>
        private void SetConstructionCenter(ConstructionCenter constructionCenter)
        {
            _currentConstructionCenter = IsConstructionCenterForCurrentTeam(constructionCenter)
                ? constructionCenter
                : null;
        }

        /// <summary>Скасовує активне розміщення, приховує візуал зони будівництва та очищає збережене посилання на ConstructionCenter.</summary>
        private void ClearConstructionCenter()
        {
            if (IsPlacing)
                CancelPlacement();

            if (_currentConstructionCenter != null)
                _currentConstructionCenter.HideBuildArea();

            _currentConstructionCenter = null;
        }

        private void Update()
        {
            if (!IsPlacing || _previewObject == null || Mouse.current == null)
                return;

            PositionObject();
            HandleRotation();
            CheckPlacement();
            UpdatePreviewColor();

            if (!_canPlaceClick)
            {
                if (!Mouse.current.leftButton.isPressed)
                    _canPlaceClick = true;

                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                if (_isValidPlacement)
                    ConfirmPlacement();
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
            }
        }

        /// <summary>
        /// Починає сеанс розміщення для вказаного BuildingData: створює об'єкт попереднього перегляду,
        /// вимикає його ігрові компоненти та показує всі доступні оверлеї зон будівництва.
        /// </summary>
        public void StartPlacement(BuildingData buildingData)
        {
            if (IsPlacing)
                return;

            if (buildingData == null || buildingData.Prefab == null)
                return;

            if (!HasAvailableConstructionArea())
                return;

            _currentBuildingData = buildingData;
            _rotationSteps = 0;

            CreatePreviewObject(buildingData.Prefab);

            IsPlacing = true;
            _canPlaceClick = false;

            ShowAllBuildAreas();

            _renderers = _previewObject.GetComponentsInChildren<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();

            PositionObject();
            CheckPlacement();
            UpdatePreviewColor();
        }

        /// <summary>Створює префаб усередині тимчасового неактивного кореня, щоб відкласти виклик Awake, після чого переміщує його до контейнера попереднього перегляду.</summary>
        private void CreatePreviewObject(GameObject prefab)
        {
            Transform previewContainer = RuntimeObjectContainer.Get("Building Previews");
            GameObject inactiveRoot = new GameObject("BuildingPlacementPreviewRoot");
            inactiveRoot.transform.SetParent(previewContainer, false);
            inactiveRoot.SetActive(false);

            _previewObject = Instantiate(prefab, inactiveRoot.transform);
            _previewObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            CacheAndDisablePreviewGameplay();

            _previewObject.transform.SetParent(previewContainer, true);
            _previewObject.SetActive(true);
            Destroy(inactiveRoot);
        }

        /// <summary>Зберігає початкові стани увімкнення/кінематики всіх Behaviour, Collider та Rigidbody на попередньому перегляді, після чого вимикає їх.</summary>
        private void CacheAndDisablePreviewGameplay()
        {
            _previewBehaviourStates.Clear();
            _previewColliderStates.Clear();
            _previewRigidbodyStates.Clear();

            foreach (Behaviour behaviour in _previewObject.GetComponentsInChildren<Behaviour>(true))
            {
                _previewBehaviourStates.Add(new BehaviourState(behaviour));
                behaviour.enabled = false;
            }

            foreach (Collider previewCollider in _previewObject.GetComponentsInChildren<Collider>(true))
            {
                _previewColliderStates.Add(new ColliderState(previewCollider));
                previewCollider.enabled = false;
            }

            foreach (Rigidbody rigidbody in _previewObject.GetComponentsInChildren<Rigidbody>(true))
            {
                _previewRigidbodyStates.Add(new RigidbodyState(rigidbody));
                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
            }
        }

        /// <summary>Кидає промінь від курсора миші проти маски землі та переміщує об'єкт попереднього перегляду до точки попадання.</summary>
        private void PositionObject()
        {
            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            if (_camera == null || Mouse.current == null)
                return;

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 1000,_groundMask))
            {
                if (IsGridPlacementActive())
                {
                    _currentGridCell = BuildingGridPlacementService.WorldToCell(hit.point, _gridConfig);
                    _previewObject.transform.SetPositionAndRotation(
                        BuildingGridPlacementService.GetPlacementPosition(
                            _currentBuildingData,
                            _currentGridCell,
                            _rotationSteps,
                            _gridConfig),
                        BuildingGridPlacementService.RotationFromSteps(_rotationSteps));
                    return;
                }

                _previewObject.transform.position = hit.point;
            }
        }

        /// <summary>Обертає об'єкт попереднього перегляду на _rotationStep градусів навколо осі Y при натисканні Q або E.</summary>
        private void HandleRotation()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                if (IsGridPlacementActive())
                {
                    _rotationSteps = BuildingGridPlacementService.NormalizeRotationSteps(_rotationSteps - ResolveRotationStepCells());
                    _previewObject.transform.SetPositionAndRotation(
                        BuildingGridPlacementService.GetPlacementPosition(
                            _currentBuildingData,
                            _currentGridCell,
                            _rotationSteps,
                            _gridConfig),
                        BuildingGridPlacementService.RotationFromSteps(_rotationSteps));
                }
                else
                {
                    _previewObject.transform.Rotate(Vector3.up, -_rotationStep);
                }
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (IsGridPlacementActive())
                {
                    _rotationSteps = BuildingGridPlacementService.NormalizeRotationSteps(_rotationSteps + ResolveRotationStepCells());
                    _previewObject.transform.SetPositionAndRotation(
                        BuildingGridPlacementService.GetPlacementPosition(
                            _currentBuildingData,
                            _currentGridCell,
                            _rotationSteps,
                            _gridConfig),
                        BuildingGridPlacementService.RotationFromSteps(_rotationSteps));
                }
                else
                {
                    _previewObject.transform.Rotate(Vector3.up, _rotationStep);
                }
            }
        }

        /// <summary>
        /// Встановлює _isValidPlacement, перевіряючи, чи знаходиться попередній перегляд усередині дійсної зони будівництва
        /// та чи не виявляє OverlapBox у його позиції жодних блокуючих об'єктів.
        /// </summary>
        private void CheckPlacement()
        {
            _isValidPlacement = true;

            if (!HasAvailableConstructionArea())
            {
                _isValidPlacement = false;
                RefreshGridFootprintBuffers();
                return;
            }

            if (IsGridPlacementActive())
            {
                _isValidPlacement = BuildingGridPlacementService.EvaluatePlacement(
                    _currentBuildingData,
                    _currentGridCell,
                    _rotationSteps,
                    _gridConfig,
                    ResolveCurrentTeam(),
                    _blockMask,
                    _previewObject.transform,
                    _gridCellBuffer,
                    _gridCellCenterBuffer,
                    _invalidGridCellBuffer);
                return;
            }

            if (!IsInsideAnyConstructionArea(_previewObject.transform.position))
            {
                _isValidPlacement = false;
                return;
            }

            Vector3 center =
                _previewObject.transform.position +
                _previewObject.transform.rotation * _currentBuildingData.CheckBoxOffset;

            Collider[] hits = Physics.OverlapBox(
                center,
                _currentBuildingData.CheckBoxSize / 2f,
                _previewObject.transform.rotation,
                _blockMask
            );

            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(_previewObject.transform))
                    continue;

                _isValidPlacement = false;
                break;
            }
        }

        /// <summary>Повертає true, якщо вказана світова позиція знаходиться в межах радіуса будівництва хоча б одного активного центру будівництва поточної команди.</summary>
        private bool IsInsideAnyConstructionArea(Vector3 position)
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center) && center.IsInsideBuildArea(position))
                    return true;
            }

            return false;
        }

        /// <summary>Забарвлює всі рендерери попереднього перегляду зеленим (допустимо) або червоним (недопустимо) через MaterialPropertyBlock.</summary>
        private void UpdatePreviewColor()
        {
            if (_renderers == null || _propertyBlock == null)
                return;

            Color color = _isValidPlacement ? _validColor : _invalidColor;

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_propertyBlock);
            }

            UpdateGridMarkers();
        }

        /// <summary>Видаляє блок властивостей тонування з усіх рендерерів попереднього перегляду, відновлюючи їх оригінальний вигляд матеріалу.</summary>
        private void ResetPreviewColor()
        {
            if (_renderers == null)
                return;

            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                    renderer.SetPropertyBlock(null);
            }
        }

        /// <summary>
        /// Витрачає вартість будівлі, перебатьківщує попередній перегляд до контейнера Buildings,
        /// відновлює всі ігрові компоненти та завершує розміщення.
        /// </summary>
        private void ConfirmPlacement()
        {
            if (!TrySpendPlacementCost())
                return;

            ResetPreviewColor();

            TeamComponent teamComponent =
                _previewObject.GetComponent<TeamComponent>();

            if (teamComponent != null)
                teamComponent.SetTeam(ResolveCurrentTeam());

            _previewObject.transform.SetParent(RuntimeObjectContainer.Get("Buildings"), true);

            if (IsGridPlacementActive())
            {
                BuildingGridOccupancy occupancy = _previewObject.GetComponent<BuildingGridOccupancy>();

                if (occupancy == null)
                    occupancy = _previewObject.AddComponent<BuildingGridOccupancy>();

                occupancy.Initialize(_currentBuildingData, _gridConfig, _currentGridCell, _rotationSteps);
            }

            RestorePreviewGameplay();
            ClearPreviewState();

            _previewObject = null;
            IsPlacing = false;

            HideAllBuildAreas();
        }

        /// <summary>Знищує об'єкт попереднього перегляду та виходить із режиму розміщення, приховуючи всі оверлеї зон будівництва.</summary>
        private void CancelPlacement()
        {
            if (_previewObject != null)
                Destroy(_previewObject);

            ClearPreviewState();

            _previewObject = null;
            IsPlacing = false;
            HideAllBuildAreas();
        }

        /// <summary>Повторно вмикає всі Behaviour, Collider та Rigidbody підтвердженої будівлі, використовуючи збережені початкові стани.</summary>
        private void RestorePreviewGameplay()
        {
            foreach (RigidbodyState state in _previewRigidbodyStates)
            {
                if (state.Component == null)
                    continue;

                state.Component.isKinematic = state.IsKinematic;
                state.Component.detectCollisions = state.DetectCollisions;
            }

            foreach (ColliderState state in _previewColliderStates)
            {
                if (state.Component != null)
                    state.Component.enabled = state.Enabled;
            }

            foreach (BehaviourState state in _previewBehaviourStates)
            {
                if (state.Component != null)
                    state.Component.enabled = state.Enabled;
            }
        }

        private void ClearPreviewState()
        {
            _previewBehaviourStates.Clear();
            _previewColliderStates.Clear();
            _previewRigidbodyStates.Clear();
            _renderers = null;
            _propertyBlock = null;
            _currentBuildingData = null;
            _gridCellBuffer.Clear();
            _gridCellCenterBuffer.Clear();
            _invalidGridCellBuffer.Clear();
            HideGridMarkers();
        }

        /// <summary>Вираховує економічну вартість будівлі з ресурсів гравця; повертає true, якщо доступно (або безкоштовно).</summary>
        private bool TrySpendPlacementCost()
        {
            TeamType currentTeam = ResolveCurrentTeam();
            if (_currentBuildingData == null || !LocalPlayerContext.IsLocalTeam(currentTeam))
                return true;

            int cost = Mathf.Max(0, _currentBuildingData.EconomyCost);
            return cost == 0 ||
                   ResourceManager.Instance == null ||
                   ResourceManager.Instance.Spend(currentTeam, cost);
        }

        private bool IsGridPlacementActive()
        {
            return _useGridPlacement && _currentBuildingData != null;
        }

        private int ResolveRotationStepCells()
        {
            if (Mathf.Approximately(_rotationStep, 0f))
                return 1;

            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(_rotationStep) / 90f));
        }

        private void RefreshGridFootprintBuffers()
        {
            _gridCellBuffer.Clear();
            _gridCellCenterBuffer.Clear();
            _invalidGridCellBuffer.Clear();

            if (!IsGridPlacementActive())
                return;

            BuildingGridPlacementService.GetOccupiedCells(
                _currentBuildingData,
                _currentGridCell,
                _rotationSteps,
                _gridConfig,
                _gridCellBuffer);
            BuildingGridPlacementService.GetOccupiedCellCenters(
                _currentBuildingData,
                _currentGridCell,
                _rotationSteps,
                _gridConfig,
                _gridCellCenterBuffer);

            _invalidGridCellBuffer.AddRange(_gridCellBuffer);
        }

        private void UpdateGridMarkers()
        {
            if (!IsGridPlacementActive() ||
                _gridConfig == null ||
                _gridConfig.MarkerPrefab == null ||
                _gridCellCenterBuffer.Count == 0)
            {
                HideGridMarkers();
                return;
            }

            EnsureGridMarkerCount(_gridMarkers, _gridCellCenterBuffer.Count, "Placement Cell");

            float cellSize = BuildingGridPlacementService.ResolveCellSize(_gridConfig);
            float markerSize = cellSize * _gridConfig.MarkerScale;

            for (int i = 0; i < _gridMarkers.Count; i++)
            {
                GridMarkerState marker = _gridMarkers[i];
                bool visible = i < _gridCellCenterBuffer.Count;
                marker.GameObject.SetActive(visible);

                if (!visible)
                    continue;

                Vector3 markerPosition = _gridCellCenterBuffer[i] + Vector3.up * _gridConfig.MarkerYOffset;
                marker.GameObject.transform.SetPositionAndRotation(markerPosition, Quaternion.identity);
                marker.GameObject.transform.localScale = new Vector3(markerSize, 0.04f, markerSize);
                Color color = i < _gridCellBuffer.Count && _invalidGridCellBuffer.Contains(_gridCellBuffer[i])
                    ? _gridConfig.InvalidCellColor
                    : _gridConfig.ValidCellColor;
                SetGridMarkerColor(marker, color);
            }
        }

        private void UpdateBuildAreaGridMarkers()
        {
            if (!_useGridPlacement ||
                _gridConfig == null ||
                _gridConfig.MarkerPrefab == null)
            {
                HideMarkers(_buildAreaGridMarkers);
                return;
            }

            _buildAreaGridCellCenters.Clear();
            HashSet<Vector2Int> visitedCells = new();
            float cellSize = BuildingGridPlacementService.ResolveCellSize(_gridConfig);

            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (!IsConstructionCenterForCurrentTeam(center))
                    continue;

                Vector2Int centerCell = BuildingGridPlacementService.WorldToCell(center.Position, _gridConfig);
                int radiusCells = Mathf.CeilToInt(center.BuildRadius / cellSize);

                for (int x = -radiusCells; x <= radiusCells; x++)
                {
                    for (int y = -radiusCells; y <= radiusCells; y++)
                    {
                        Vector2Int cell = centerCell + new Vector2Int(x, y);

                        if (!visitedCells.Add(cell))
                            continue;

                        Vector3 cellCenter = BuildingGridPlacementService.CellToWorld(cell, _gridConfig);

                        if (center.IsInsideBuildArea(cellCenter))
                            _buildAreaGridCellCenters.Add(cellCenter);
                    }
                }
            }

            EnsureGridMarkerCount(_buildAreaGridMarkers, _buildAreaGridCellCenters.Count, "Build Area Cell");

            float markerSize = cellSize * _gridConfig.MarkerScale;

            for (int i = 0; i < _buildAreaGridMarkers.Count; i++)
            {
                GridMarkerState marker = _buildAreaGridMarkers[i];
                bool visible = i < _buildAreaGridCellCenters.Count;
                marker.GameObject.SetActive(visible);

                if (!visible)
                    continue;

                Vector3 markerPosition = _buildAreaGridCellCenters[i] + Vector3.up * (_gridConfig.MarkerYOffset * 0.5f);
                marker.GameObject.transform.SetPositionAndRotation(markerPosition, Quaternion.identity);
                marker.GameObject.transform.localScale = new Vector3(markerSize, 0.025f, markerSize);
                Vector2Int cell = BuildingGridPlacementService.WorldToCell(_buildAreaGridCellCenters[i], _gridConfig);
                Color color = BuildingGridPlacementService.IsReservedByOther(cell, null) ||
                              BuildingGridPlacementService.IsCellBlocked(
                                  _buildAreaGridCellCenters[i],
                                  _gridConfig,
                                  _blockMask)
                    ? _gridConfig.InvalidCellColor
                    : _gridConfig.BuildAreaCellColor;
                SetGridMarkerColor(marker, color);
            }
        }

        private void EnsureGridMarkerCount(List<GridMarkerState> markers, int count, string suffix)
        {
            while (markers.Count < count)
                markers.Add(CreateGridMarker(suffix));
        }

        private GridMarkerState CreateGridMarker(string suffix)
        {
            Transform markerContainer = RuntimeObjectContainer.Get("Building Grid Markers");
            GameObject markerObject = Instantiate(_gridConfig.MarkerPrefab, markerContainer);
            markerObject.name = $"{_gridConfig.MarkerPrefab.name} ({suffix})";

            foreach (Collider markerCollider in markerObject.GetComponentsInChildren<Collider>(true))
                markerCollider.enabled = false;

            return new GridMarkerState
            {
                GameObject = markerObject,
                Renderers = markerObject.GetComponentsInChildren<Renderer>(true)
            };
        }

        private void SetGridMarkerColor(GridMarkerState marker, Color color)
        {
            if (_gridMarkerPropertyBlock == null)
                _gridMarkerPropertyBlock = new MaterialPropertyBlock();

            foreach (Renderer renderer in marker.Renderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_gridMarkerPropertyBlock);
                _gridMarkerPropertyBlock.SetColor("_BaseColor", color);
                _gridMarkerPropertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(_gridMarkerPropertyBlock);
            }
        }

        private void HideGridMarkers()
        {
            HideMarkers(_gridMarkers);
        }

        private static void HideMarkers(List<GridMarkerState> markers)
        {
            foreach (GridMarkerState marker in markers)
            {
                if (marker.GameObject != null)
                    marker.GameObject.SetActive(false);
            }
        }

        private void ShowAllBuildAreas()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center))
                    center.ShowBuildArea();
            }

            UpdateBuildAreaGridMarkers();
        }

        private void HideAllBuildAreas()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center))
                    center.HideBuildArea();
            }

            HideMarkers(_buildAreaGridMarkers);
            _buildAreaGridCellCenters.Clear();
        }

        /// <summary>Повертає true, якщо в сцені існує хоча б один увімкнений ConstructionCenter, що належить поточній команді.</summary>
        private bool HasAvailableConstructionArea()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center))
                    return true;
            }

            return false;
        }

        /// <summary>Повертає true, якщо центр активний і або не має TeamComponent, або його команда збігається з _currentTeam.</summary>
        private bool IsConstructionCenterForCurrentTeam(ConstructionCenter center)
        {
            if (center == null || !center.isActiveAndEnabled)
                return false;

            TeamComponent teamComponent = center.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == ResolveCurrentTeam();
        }

        private TeamType ResolveCurrentTeam()
        {
            return _currentTeam == TeamType.Player
                ? LocalPlayerContext.LocalTeam
                : _currentTeam;
        }

        private sealed class GridMarkerState
        {
            public GameObject GameObject;
            public Renderer[] Renderers;
        }

        private struct BehaviourState
        {
            public BehaviourState(Behaviour component)
            {
                Component = component;
                Enabled = component.enabled;
            }

            public Behaviour Component;
            public bool Enabled;
        }

        private struct ColliderState
        {
            public ColliderState(Collider component)
            {
                Component = component;
                Enabled = component.enabled;
            }

            public Collider Component;
            public bool Enabled;
        }

        private struct RigidbodyState
        {
            public RigidbodyState(Rigidbody component)
            {
                Component = component;
                IsKinematic = component.isKinematic;
                DetectCollisions = component.detectCollisions;
            }

            public Rigidbody Component;
            public bool IsKinematic;
            public bool DetectCollisions;
        }
    }
}
