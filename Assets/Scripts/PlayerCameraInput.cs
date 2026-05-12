using UnityEngine;

public class PlayerCameraInput : MonoBehaviour
{
    private CameraAction m_moveCameraAction;
   [HideInInspector] public Vector2 M_moveInput;
   [HideInInspector] public Vector2 M_mouseDelta;
   [HideInInspector] public float M_mouseScroll;
   [HideInInspector] public float M_rotateInput;       
    

    private void Awake()
    {
        m_moveCameraAction = new CameraAction();
    }

    public void OnEnable()
    {
        m_moveCameraAction.Enable();
    }

    public void OnDisable()
    {
        m_moveCameraAction.Disable();
    }

    void Update()
    {
        M_moveInput = m_moveCameraAction.MoveCamera.Move.ReadValue<Vector2>();
        M_mouseDelta = m_moveCameraAction.MoveCamera.RotateMouse.ReadValue<Vector2>();
        M_rotateInput = m_moveCameraAction.MoveCamera.Rotate.ReadValue<float>();
        M_mouseScroll = m_moveCameraAction.MoveCamera.Zoom.ReadValue<Vector2>().y;
    }
}

