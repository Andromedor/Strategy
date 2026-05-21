using Data;
using UnityEngine;

namespace UI
{
    public class ProductionPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform _contentRoot;  // Куди спавнити кнопки.
        [SerializeField] private ProductionButtonUI _buttonPrefab;
        
        private BuildingProduction _currentFactory;
        
        private void OnEnable()
        {
            EventManager.OnFactorySelected += OpenFactory;
        }

        private void OnDisable()
        {
            EventManager.OnFactorySelected -= OpenFactory;
        }
        
        private void OpenFactory(BuildingProduction factory)
        {
            _currentFactory = factory;

            ClearButtons();

            foreach (ProductionItemData item in factory.Items)
            {
                ProductionButtonUI button =
                    Instantiate(_buttonPrefab, _contentRoot);

                button.Initialize(item, OnItemClicked);
            }
        }
        
        private void OnItemClicked(ProductionItemData item)
        {
            if (_currentFactory == null)
                return;

            _currentFactory.AddToQueue(item);
        }

        private void ClearButtons()
        {
            foreach (Transform child in _contentRoot)
            {
                Destroy(child.gameObject);
            }
        }
    }
}