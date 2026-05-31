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
    /// Manages the interactive building-placement workflow: creates a ghost preview object that
    /// follows the cursor, validates placement against construction areas and overlap checks,
    /// handles Q/E rotation, and confirms or cancels the placement on mouse click.
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
        private readonly List<BehaviourState> _previewBehaviourStates = new();
        private readonly List<ColliderState> _previewColliderStates = new();
        private readonly List<RigidbodyState> _previewRigidbodyStates = new();

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
        
        /// <summary>Stores the active ConstructionCenter when it belongs to the current team; clears it otherwise.</summary>
        private void SetConstructionCenter(ConstructionCenter constructionCenter)
        {
            _currentConstructionCenter = IsConstructionCenterForCurrentTeam(constructionCenter)
                ? constructionCenter
                : null;
        }

        /// <summary>Cancels any active placement, hides the build-area visual, and clears the stored ConstructionCenter reference.</summary>
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
        /// Begins a placement session for the given BuildingData: instantiates the preview object,
        /// disables its gameplay components, and shows all valid construction-area overlays.
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

        /// <summary>Instantiates the prefab inside a temporary inactive root so Awake is deferred, then reparents it to the preview container.</summary>
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

        /// <summary>Records original enabled/kinematic states of all Behaviours, Colliders, and Rigidbodies on the preview, then disables them.</summary>
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

        /// <summary>Raycasts from the mouse cursor against the ground mask and moves the preview object to the hit point.</summary>
        private void PositionObject()
        {
            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            if (_camera == null || Mouse.current == null)
                return;

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 1000,_groundMask))
            {
                _previewObject.transform.position = hit.point;
            }
        }

        /// <summary>Rotates the preview object by _rotationStep degrees around Y when Q or E is pressed.</summary>
        private void HandleRotation()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                _previewObject.transform.Rotate(Vector3.up, -_rotationStep);
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                _previewObject.transform.Rotate(Vector3.up, _rotationStep);
            }
        }

        /// <summary>
        /// Sets _isValidPlacement by verifying the preview is inside a valid construction area
        /// and that an OverlapBox at its position finds no blocking objects.
        /// </summary>
        private void CheckPlacement()
        {
            _isValidPlacement = true;

            if (!HasAvailableConstructionArea())
            {
                _isValidPlacement = false;
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
        
        /// <summary>Returns true if the given world position falls within the build radius of at least one active construction center for the current team.</summary>
        private bool IsInsideAnyConstructionArea(Vector3 position)
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center) && center.IsInsideBuildArea(position))
                    return true;
            }

            return false;
        }
        
        /// <summary>Tints all preview renderers green (valid) or red (invalid) via a MaterialPropertyBlock.</summary>
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
        }
        
        /// <summary>Removes the tint property block from all preview renderers, restoring their original material appearance.</summary>
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
        /// Spends the building cost, reparents the preview to the Buildings container,
        /// restores all gameplay components, and finalises placement.
        /// </summary>
        private void ConfirmPlacement()
        {
            if (!TrySpendPlacementCost())
                return;

            ResetPreviewColor();

            TeamComponent teamComponent =
                _previewObject.GetComponent<TeamComponent>();

            if (teamComponent != null)
                teamComponent.SetTeam(_currentTeam);

            _previewObject.transform.SetParent(RuntimeObjectContainer.Get("Buildings"), true);
            RestorePreviewGameplay();
            ClearPreviewState();

            _previewObject = null;
            IsPlacing = false;

            HideAllBuildAreas();
        }

        /// <summary>Destroys the preview object and exits placement mode, hiding all construction-area overlays.</summary>
        private void CancelPlacement()
        {
            if (_previewObject != null)
                Destroy(_previewObject);

            ClearPreviewState();

            _previewObject = null;
            IsPlacing = false;
            HideAllBuildAreas();
        }

        /// <summary>Re-enables all Behaviours, Colliders, and Rigidbodies on the confirmed building using the cached original states.</summary>
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
        }

        /// <summary>Deducts the building's economy cost from the player's resources; returns true if affordable (or free).</summary>
        private bool TrySpendPlacementCost()
        {
            if (_currentBuildingData == null || _currentTeam != TeamType.Player)
                return true;

            int cost = Mathf.Max(0, _currentBuildingData.EconomyCost);
            return cost == 0 ||
                   ResourceManager.Instance == null ||
                   ResourceManager.Instance.Spend(cost);
        }
        
        private void ShowAllBuildAreas()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center))
                    center.ShowBuildArea();
            }
        }

        private void HideAllBuildAreas()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center))
                    center.HideBuildArea();
            }
        }

        /// <summary>Returns true if at least one enabled ConstructionCenter belonging to the current team exists in the scene.</summary>
        private bool HasAvailableConstructionArea()
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (IsConstructionCenterForCurrentTeam(center))
                    return true;
            }

            return false;
        }

        /// <summary>Returns true when the center is active and either has no TeamComponent or its team matches _currentTeam.</summary>
        private bool IsConstructionCenterForCurrentTeam(ConstructionCenter center)
        {
            if (center == null || !center.isActiveAndEnabled)
                return false;

            TeamComponent teamComponent = center.GetComponentInParent<TeamComponent>();
            return teamComponent == null || teamComponent.Team == _currentTeam;
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
