using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
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

       if (_panels == null)
           return;

       foreach (var panel in _panels)
       {
           if (panel == null || panel.panelObject == null)
               continue;

           _panelDictionary[panel.type] = panel.panelObject;
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
       foreach (var panel in _panelDictionary.Values)
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

