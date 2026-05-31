using System;
using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Strategy.UI
{
    public class UIManager : MonoBehaviour
    {
        [Serializable]
        private class Panel
        {
            [SerializeField, FormerlySerializedAs("type")] private PanelType _type;
            [SerializeField, FormerlySerializedAs("panelObject")] private GameObject _panelObject;

            public PanelType Type => _type;
            public GameObject PanelObject => _panelObject;
        }

        [SerializeField] private List<Panel> _panels;

        private readonly Dictionary<PanelType, GameObject> _panelDictionary = new();

        private void Awake()
        {
            _panelDictionary.Clear();

            if (_panels == null)
                return;

            foreach (Panel panel in _panels)
            {
                if (panel == null || panel.PanelObject == null)
                    continue;

                _panelDictionary[panel.Type] = panel.PanelObject;
            }
        }

        private void OnEnable()
        {
            EventManager.OnOpenPanel += OpenPanel;
        }

        private void OnDisable()
        {
            EventManager.OnOpenPanel -= OpenPanel;
        }

        private void Start()
        {
            OpenPanel(PanelType.MainMenu);
        }

        public void OpenPanel(PanelType type)
        {
            foreach (GameObject panel in _panelDictionary.Values)
            {
                if (panel != null)
                    panel.SetActive(false);
            }

            if (_panelDictionary.TryGetValue(type, out GameObject panelObject) && panelObject != null)
                panelObject.SetActive(true);
        }
    }

    public enum PanelType
    {
        MainMenu,
        Factory,
        Barracks,
        Construction,
        Outpost
    }
}
