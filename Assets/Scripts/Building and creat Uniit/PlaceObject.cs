using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlaceObject : MonoBehaviour
{
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private float _rotateSpeed= 90;

    private void Start()
    {
        PositionObject();
    }
    
    private void Update()
    {
        PositionObject();

        if (Mouse.current.leftButton.isPressed)
        {
            gameObject.gameObject.GetComponent<UnitCreat>().enabled = true;
            Destroy(gameObject.GetComponent<PlaceObject>());
        }
           
        if (Mouse.current.rightButton.isPressed)
            Destroy(gameObject);
        if (Mouse.current.middleButton.isPressed)
        {
            transform.Rotate(Vector3.up * (Time.deltaTime * _rotateSpeed));
        }
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
