using UnityEngine;
using System;
using DefaultNamespace;

public static class EventManager 
{
    public static Action<PanelType> OnOpenPanel;
    
    // Викликається, коли юніта обрали.
    public static Action<GameObject> OnUnitSelected;

    // Викликається, коли юніта зняли з вибору.
    public static Action<GameObject> OnUnitDeselected;

    // Викликається, коли юніту дали наказ руху.
    public static Action<GameObject, Vector3> OnUnitMoveCommand;

    // Викликається, коли юніт почав атакувати ціль.
    public static Action<GameObject, Transform> OnUnitAttackTargetChanged;
    
    public static Action<BuildingProduction> OnFactorySelected;
    
    public static Action<ConstructionCenter> OnConstructionCenterSelected;
    public static Action OnConstructionClosed;
    public static Action OnConstructionCentersChanged;
}
