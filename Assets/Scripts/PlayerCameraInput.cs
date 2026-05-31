using UnityEngine;

namespace Strategy.Camera
{
    /// <summary>
    /// Зчитує необроблене введення із згенерованого асету Input Action CameraAction щокадру та
    /// надає оброблені значення (MoveInput, MouseDelta, MouseScroll, RotateInput) для використання CameraController.
    /// </summary>
    public class PlayerCameraInput : MonoBehaviour
    {
        private CameraAction _cameraActions;

        public Vector2 MoveInput { get; private set; }
        public Vector2 MouseDelta { get; private set; }
        public float MouseScroll { get; private set; }
        public float RotateInput { get; private set; }

        private void Awake()
        {
            _cameraActions = new CameraAction();
        }

        private void OnEnable()
        {
            _cameraActions ??= new CameraAction();
            _cameraActions.Enable();
        }

        private void OnDisable()
        {
            _cameraActions?.Disable();
        }

        private void OnDestroy()
        {
            _cameraActions?.Dispose();
            _cameraActions = null;
        }

        private void Update()
        {
            if (_cameraActions == null)
                return;

            MoveInput = _cameraActions.MoveCamera.Move.ReadValue<Vector2>();
            MouseDelta = _cameraActions.MoveCamera.RotateMouse.ReadValue<Vector2>();
            RotateInput = _cameraActions.MoveCamera.Rotate.ReadValue<float>();
            MouseScroll = _cameraActions.MoveCamera.Zoom.ReadValue<Vector2>().y;
        }
    }
}
