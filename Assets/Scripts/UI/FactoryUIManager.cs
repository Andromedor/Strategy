using System.Collections.Generic;
using UnityEngine;

public class FactoryUIManager : MonoBehaviour
{
   [System.Serializable]
   public class Panel
   {
       public PanelType type;
       public GameObject panelObject;
   }
   
   [SerializeField] private List<Panel> _panels;
   private Dictionary<PanelType, GameObject> _panelDictionary;
   
   private void Awake()
   {
       _panelDictionary = new Dictionary<PanelType, GameObject>();

       foreach (var panel in _panels)
       {
           _panelDictionary.Add(panel.type, panel.panelObject);
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

   private void OpenPanel(PanelType type)
   {
       foreach (var panel in _panelDictionary.Values)
           panel.SetActive(false);

       if (_panelDictionary.ContainsKey(type))
           _panelDictionary[type].SetActive(true);
   }
}

public enum PanelType
{
    MainMenu,
    Factory,
    Barracks,
}

