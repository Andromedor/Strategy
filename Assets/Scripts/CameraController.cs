using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class CameraController : MonoBehaviour
{
  [SerializeField] private float _moveSpeed = 10f;
  [SerializeField] private float _rotationSpeed = 100f;
  [SerializeField] private float _mouseRotationSpeed = 100f;
  [SerializeField] private float _zoomSpeed = 10f;
  [SerializeField] private float _minZoomHeight = 2f;
  [SerializeField] private float _maxZoomHeight = 20f;
  [SerializeField] private float _edgeScrollSpeed = 15f;
  [SerializeField] private float _edgeSize = 10f; // ????? ???? ??? ???? ??? ????? ?? ???? ??????

    private PlayerCameraInput _input;

    private void Awake()
    {
        _input = GetComponent<PlayerCameraInput>();
    }

    private void Update()
    {
        MoveCamera();
        RotateByKeyboard();
        RotateByMouse();
        ZoomCamera();
    }

    private void MoveCamera()
    {
        Vector2 moveInput = _input.M_moveInput;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();
        
        Vector3 move = forward * moveInput.y + right * moveInput.x;
        
        transform.position += move * _moveSpeed * Time.deltaTime;
        
        Vector3 edgeInput = GetEdgeScrollInput();
        Vector3 edgeMove = forward * edgeInput.z + right * edgeInput.x;

        Vector3 edgeMovement = move * _moveSpeed + edgeMove * _edgeScrollSpeed;
        transform.position += edgeMovement * Time.deltaTime;
    }

    private void RotateByKeyboard()
    { 
        float rotation = _input.M_rotateInput;
        transform.Rotate(Vector3.up, rotation * _rotationSpeed * Time.deltaTime, Space.World);
    }

    private void RotateByMouse()
    {
        if (Mouse.current.middleButton.isPressed)
        {
            float mouseX = _input.M_mouseDelta.x;
            transform.Rotate(Vector3.up, mouseX * _mouseRotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void ZoomCamera()
    {
        float scroll = _input.M_mouseScroll;
        if (Mathf.Abs(scroll) < 0.01f) return;

        Vector3 forward = transform.forward;
        Vector3 zoom = forward * scroll * _zoomSpeed * Time.deltaTime;

        Vector3 newPosition = transform.position + zoom;

        if(newPosition.y >= _minZoomHeight && newPosition.y <= _maxZoomHeight)
        {
           transform.position = newPosition;
        }
    }

    private Vector3 GetEdgeScrollInput()
    {
       Vector3 input = Vector3.zero;

       Vector3 mousePosition = Mouse.current.position.ReadValue();
       
        if (mousePosition.x <= _edgeSize)
        {
            input.x = -1;
        }
        else if(mousePosition.x >= Screen.width - _edgeSize)
        {
            input.x = 1;
        }

        if (mousePosition.y <= _edgeSize)
        {
            input.z = -1;
        }
        else if(mousePosition.y >= Screen.height - _edgeSize)
        {      
            input.z = 1;
        }

        return input;
    }
}
