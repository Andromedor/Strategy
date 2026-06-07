using Strategy.Buildings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Strategy.Camera
{
    /// <summary>
    /// Керує RTS-камерою щокадру: панорамування за допомогою клавіатури та прокрутки по краях екрана, обертання
    /// клавішами або перетягуванням середньою кнопкою миші, та масштабування шляхом переміщення вперед/назад вздовж
    /// напрямку погляду камери в межах налаштованого діапазону висоти.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 10f;
        [SerializeField] private float _rotationSpeed = 100f;
        [SerializeField] private float _mouseRotationSpeed = 100f;
        [SerializeField] private float _zoomSpeed = 10f;
        [SerializeField] private float _minZoomHeight = 2f;
        [SerializeField] private float _maxZoomHeight = 20f;
        [SerializeField] private float _edgeScrollSpeed = 15f;
        [SerializeField] private float _edgeSize = 10f;
        [SerializeField] private bool _disableEdgeScrollInEditor = true;

        private PlayerCameraInput _input;

        private void Awake()
        {
            _input = GetComponent<PlayerCameraInput>();
        }

        private void Update()
        {
            if (_input == null || Mouse.current == null)
                return;

            MoveCamera();
            RotateByKeyboard();
            RotateByMouse();
            ZoomCamera();
        }

        /// <summary>Переміщує камеру, використовуючи введення WASD та прокрутку по краях екрана, ігноруючи компонент Y векторів вперед/вправо.</summary>
        private void MoveCamera()
        {
            Vector2 moveInput = _input.MoveInput;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 keyboardMove = forward * moveInput.y + right * moveInput.x;
            Vector3 edgeInput = GetEdgeScrollInput();
            Vector3 edgeMove = forward * edgeInput.z + right * edgeInput.x;

            Vector3 movement = keyboardMove * _moveSpeed + edgeMove * _edgeScrollSpeed;
            transform.position += movement * Time.deltaTime;
        }

        /// <summary>Обертає камеру навколо світової осі Y, використовуючи введення клавіатури; пригнічується під час розміщення будівель.</summary>
        private void RotateByKeyboard()
        {
            if (BuildingPlacementManager.IsPlacing)
                return;

            float rotation = _input.RotateInput;
            transform.Rotate(Vector3.up, rotation * _rotationSpeed * Time.deltaTime, Space.World);
        }

        /// <summary>Обертає камеру навколо світової осі Y, використовуючи горизонтальну дельту миші при утриманні середньої кнопки.</summary>
        private void RotateByMouse()
        {
            if (!Mouse.current.middleButton.isPressed)
                return;

            float mouseX = _input.MouseDelta.x;
            transform.Rotate(Vector3.up, mouseX * _mouseRotationSpeed * Time.deltaTime, Space.World);
        }

        /// <summary>Переміщує камеру вздовж вектора вперед пропорційно до введення колеса прокрутки, обмежуючи висоту в межах мін/макс.</summary>
        private void ZoomCamera()
        {
            float scroll = _input.MouseScroll;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            Vector3 newPosition = transform.position + transform.forward * scroll * _zoomSpeed * Time.deltaTime;

            if (newPosition.y >= _minZoomHeight && newPosition.y <= _maxZoomHeight)
                transform.position = newPosition;
        }

        /// <summary>Повертає спрямований Vector3 (компоненти x та z), що вказує, яких країв екрана торкається курсор.</summary>
        private Vector3 GetEdgeScrollInput()
        {
            if (_disableEdgeScrollInEditor && Application.isEditor)
                return Vector3.zero;

            Vector3 input = Vector3.zero;
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (mousePosition.x <= _edgeSize)
                input.x = -1f;
            else if (mousePosition.x >= Screen.width - _edgeSize)
                input.x = 1f;

            if (mousePosition.y <= _edgeSize)
                input.z = -1f;
            else if (mousePosition.y >= Screen.height - _edgeSize)
                input.z = 1f;

            return input;
        }
    }
}
