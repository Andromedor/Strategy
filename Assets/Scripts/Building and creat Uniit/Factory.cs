using UnityEngine;

public class Factory : MonoBehaviour
{
    [SerializeField] private UnitData _unitData;
    [SerializeField] private PanelType _panelType;

    public PanelType PanelType => _panelType;
    public void OnClickUnity()
    {
        if (SelectionManager.SelectedFactory == null)
        {
            Debug.Log("SelectedFactory == null");
            return;
        }
           
        
        Debug.Log("OnClickUnity");
        SelectionManager.SelectedFactory.AddToQueue(_unitData);
    }
}

