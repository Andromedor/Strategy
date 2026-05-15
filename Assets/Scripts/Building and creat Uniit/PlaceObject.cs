using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlaceObject : MonoBehaviour
{
    [Header("Raycast")] 
    [SerializeField] private LayerMask _groundMask;

    [Header("Placement check")]
    [SerializeField] private LayerMask _blockMask;
    [SerializeField] private Vector3 _checkBoxSize = new Vector3(4f, 2f, 4f);
    [SerializeField] private Vector3 _checkBoxOffset = new Vector3(0f, 1f, 0f);

    [Header("Rotation")]
    [SerializeField] private float _rotationStep = 90f;
    
    [Header("Preview Colors")]
    [SerializeField] private Color _validColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);
    
    public static bool IsPlacing { get; private set; }
    private bool _isValidPlacement;
    private bool _canPlaceClick;
    
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;

    private void Start()
    {
        PositionObject();
        CheckPlacement();
        UpdatePreviewColor();
    }
    
    private void Awake()
    {
        IsPlacing = true;
        _canPlaceClick = false;

        _renderers = GetComponentsInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
    }
    
    private void Update()
    {
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
    
    private void PositionObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 1000,_groundMask))
        {
            transform.position = hit.point;
        }
    }

    private void HandleRotation()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            transform.Rotate(Vector3.up, -_rotationStep);
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            transform.Rotate(Vector3.up, _rotationStep);
        }
    }

    private void CheckPlacement()
    {
        Vector3 center = transform.position + transform.rotation * _checkBoxOffset;
        
        Collider[] hits = Physics.OverlapBox(
            center,
            _checkBoxSize / 2f,
            transform.rotation,
            _blockMask
        );
        
        _isValidPlacement =true;
        
        foreach (Collider hit in hits)
        {
            if(hit.transform.IsChildOf(transform))
                continue;
            
            _isValidPlacement = false;
            break;
        }
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
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isValidPlacement ? Color.green : Color.red;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position + transform.rotation * _checkBoxOffset,
            transform.rotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(Vector3.zero, _checkBoxSize);

        Gizmos.matrix = oldMatrix;
    }
    
    private void ConfirmPlacement()
    {
        IsPlacing = false;
        ResetPreviewColor();
        Destroy(this);
    }

    private void CancelPlacement()
    {
        IsPlacing = false;
        Destroy(gameObject);
    }
    
    private void OnDestroy()
    {
        if (IsPlacing)
            IsPlacing = false;
    }
}
