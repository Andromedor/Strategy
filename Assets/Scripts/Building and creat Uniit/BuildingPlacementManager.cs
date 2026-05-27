using System.Collections.Generic;
using DefaultNamespace;
using UnitController;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildingPlacementManager : MonoBehaviour
{
    [Header("Raycast")] 
    [SerializeField] private Camera _camera;
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
    
    private void SetConstructionCenter(ConstructionCenter constructionCenter)
    {
        _currentConstructionCenter = constructionCenter;
    }

    private void ClearConstructionCenter()
    {
        if (_currentConstructionCenter != null)
            _currentConstructionCenter.HideBuildArea();

        _currentConstructionCenter = null;
    }
    
    private void Update()
    {
        if (!IsPlacing || _previewObject == null)
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
    
    public void StartPlacement(BuildingData buildingData)
    {
        if (IsPlacing)
            return;

        if (buildingData == null || buildingData.prefab == null)
            return;

        if (ConstructionCenter.All.Count == 0)
            return;

        _currentBuildingData = buildingData;

        CreatePreviewObject(buildingData.prefab);

        IsPlacing = true;
        _canPlaceClick = false;
        
        ShowAllBuildAreas();

        _renderers = _previewObject.GetComponentsInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();

        PositionObject();
        CheckPlacement();
        UpdatePreviewColor();
    }

    private void CreatePreviewObject(GameObject prefab)
    {
        GameObject inactiveRoot = new GameObject("BuildingPlacementPreviewRoot");
        inactiveRoot.SetActive(false);

        _previewObject = Instantiate(prefab, inactiveRoot.transform);
        _previewObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        CacheAndDisablePreviewGameplay();

        _previewObject.transform.SetParent(null, true);
        _previewObject.SetActive(true);
        Destroy(inactiveRoot);
    }

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

    private void PositionObject()
    {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 1000,_groundMask))
        {
            _previewObject.transform.position = hit.point;
        }
    }

    private void HandleRotation()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            _previewObject.transform.Rotate(Vector3.up, -_rotationStep);
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            _previewObject.transform.Rotate(Vector3.up, _rotationStep);
        }
    }

    private void CheckPlacement()
    {
        _isValidPlacement = true;

        if (ConstructionCenter.All.Count == 0)
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
    
    private bool IsInsideAnyConstructionArea(Vector3 position)
    {
        foreach (ConstructionCenter center in ConstructionCenter.All)
        {
            if (center != null && center.IsInsideBuildArea(position))
                return true;
        }

        return false;
    }
    
    private void UpdatePreviewColor()
    {
        Color color = _isValidPlacement ? _validColor : _invalidColor;

        foreach (Renderer renderer in _renderers)
        {
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }
    
    private void ResetPreviewColor()
    {
        foreach (Renderer renderer in _renderers)
        {
            renderer.SetPropertyBlock(null);
        }
    }
    
    private void ConfirmPlacement()
    {
        ResetPreviewColor();

        TeamComponent teamComponent =
            _previewObject.GetComponent<TeamComponent>();

        if (teamComponent != null)
            teamComponent.SetTeam(_currentTeam);

        RestorePreviewGameplay();
        ClearPreviewState();

        _previewObject = null;
        IsPlacing = false;

        HideAllBuildAreas();
    }

    private void CancelPlacement()
    {
        Destroy(_previewObject);
        ClearPreviewState();

        _previewObject = null;
        IsPlacing = false;
        HideAllBuildAreas();
    }

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
        _currentBuildingData = null;
    }
    
    private void ShowAllBuildAreas()
    {
        foreach (ConstructionCenter center in ConstructionCenter.All)
        {
            if (center != null)
                center.ShowBuildArea();
        }
    }

    private void HideAllBuildAreas()
    {
        foreach (ConstructionCenter center in ConstructionCenter.All)
        {
            if (center != null)
                center.HideBuildArea();
        }
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
