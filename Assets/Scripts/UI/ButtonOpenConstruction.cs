using Strategy.Buildings;
using Strategy.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
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

        private void OpenConstruction()
        {
            EventManager.RaiseOpenPanel(PanelType.Construction);
        }

        private void Refresh()
        {
            if (_button != null)
                _button.interactable = ConstructionCenter.All.Count > 0;
        }
    }
}
