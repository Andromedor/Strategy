using DefaultNamespace;
using UnityEngine;
using UnityEngine.UI;
    
namespace UI
{
    public class ButtonOpenConstruction: MonoBehaviour
    {
        [SerializeField] private Button _button;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OpenConstruction);
            EventManager.OnConstructionCentersChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OpenConstruction);
            EventManager.OnConstructionCentersChanged -= Refresh;
        }

        private void OpenConstruction()
        {
            EventManager.OnOpenPanel?.Invoke(PanelType.Construction);
        }
        
        private void Refresh()
        {
            _button.interactable = ConstructionCenter.All.Count > 0;
        }
    }
}