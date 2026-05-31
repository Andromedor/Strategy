using Strategy.Buildings;
using Strategy.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
    /// <summary>
    /// HUD button that opens the Construction panel via <see cref="EventManager.RaiseOpenPanel"/>.
    /// Automatically disables itself when no <see cref="ConstructionCenter"/> is active in the scene.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonOpenConstruction : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(OpenConstruction);

            EventManager.OnConstructionCentersChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OpenConstruction);

            EventManager.OnConstructionCentersChanged -= Refresh;
        }

        /// <summary>Raises the open-panel event to switch the HUD to the Construction panel.</summary>
        private void OpenConstruction()
        {
            EventManager.RaiseOpenPanel(PanelType.Construction);
        }

        /// <summary>Enables the button only when at least one active construction center exists.</summary>
        private void Refresh()
        {
            if (_button != null)
                _button.interactable = ConstructionCenter.All.Count > 0;
        }
    }
}
