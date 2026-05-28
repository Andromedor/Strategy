using System.Collections.Generic;
using Data;
using DefaultNamespace;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class ProductionPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private ProductionButtonUI _buttonPrefab;

        private readonly List<ProductionButtonUI> _buttons = new();
        private BuildingProduction _currentFactory;

        private void OnEnable()
        {
            EventManager.OnFactorySelected += OpenFactory;
            ResourceManager.OnResourceChanged += RefreshButtons;
        }

        private void OnDisable()
        {
            EventManager.OnFactorySelected -= OpenFactory;
            ResourceManager.OnResourceChanged -= RefreshButtons;
        }

        private void OpenFactory(BuildingProduction factory)
        {
            _currentFactory = factory;

            ClearButtons();

            int validItems = CountValidItems(factory);
            ApplyLayout(validItems);

            if (factory == null)
                return;

            foreach (ProductionItemData item in factory.Items)
            {
                if (item == null)
                    continue;

                ProductionButtonUI button =
                    Instantiate(_buttonPrefab, _contentRoot);

                button.Initialize(item, OnItemClicked);
                _buttons.Add(button);
            }

            RefreshButtons(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
        }

        private void OnItemClicked(ProductionItemData item)
        {
            if (_currentFactory == null)
                return;

            _currentFactory.AddToQueue(item);
            RefreshButtons(ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0);
        }

        private void RefreshButtons(int playerResource)
        {
            foreach (ProductionButtonUI button in _buttons)
            {
                if (button != null)
                    button.RefreshAvailability(playerResource);
            }
        }

        private void ClearButtons()
        {
            foreach (Transform child in _contentRoot)
            {
                Destroy(child.gameObject);
            }

            _buttons.Clear();
        }

        private void ApplyLayout(int itemCount)
        {
            RectTransform panelRect = transform as RectTransform;

            if (panelRect != null)
                panelRect.sizeDelta = new Vector2(Mathf.Max(210f, itemCount * 198f), 76f);

            RectTransform contentRect = _contentRoot as RectTransform;

            if (contentRect != null)
                contentRect.sizeDelta = new Vector2(Mathf.Max(210f, itemCount * 198f), 76f);

            ApplyHorizontalLayout(GetComponent<HorizontalLayoutGroup>());

            if (_contentRoot != null)
                ApplyHorizontalLayout(_contentRoot.GetComponent<HorizontalLayoutGroup>());
        }

        private static void ApplyHorizontalLayout(HorizontalLayoutGroup layout)
        {
            if (layout == null)
                return;

            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static int CountValidItems(BuildingProduction factory)
        {
            if (factory == null || factory.Items == null)
                return 0;

            int count = 0;

            foreach (ProductionItemData item in factory.Items)
            {
                if (item != null)
                    count++;
            }

            return count;
        }
    }
}
