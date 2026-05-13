using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlaceObject : MonoBehaviour
{
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private float _rotateSpeed= 90;
    
    public static bool IsPlacing { get; private set; }
    private bool _canPlace;

    private void Start()
    {
        PositionObject();
    }
    
    private void Awake()
    {

        IsPlacing = true;
        _canPlace = false;
    }
    
    private void Update()
    {
        PositionObject();
        
        if (!_canPlace)
        {
            if (!Mouse.current.leftButton.isPressed)
                _canPlace = true;

            return;
        }
        
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            ConfirmPlacement();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
        }

        if (Mouse.current.middleButton.isPressed)
        {
            transform.Rotate(Vector3.up * (_rotateSpeed * Time.deltaTime));
        }
    }
    
    private void ConfirmPlacement()
    {
        IsPlacing = false;
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
    
    private void PositionObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 1000,_layerMask))
        {
            transform.position = hit.point;
        }
    }
}
