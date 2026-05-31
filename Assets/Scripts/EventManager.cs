using System;
using Strategy.Buildings;
using Strategy.UI;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    public static class EventManager
    {
        public static event Action<PanelType> OnOpenPanel;
        public static event Action<GameObject> OnUnitSelected;
        public static event Action<GameObject> OnUnitDeselected;
        public static event Action<GameObject, Vector3> OnUnitMoveCommand;
        public static event Action<GameObject, Transform> OnUnitAttackTargetChanged;
        public static event Action<BuildingProduction> OnFactorySelected;
        public static event Action<ConstructionCenter> OnConstructionCenterSelected;
        public static event Action OnConstructionClosed;
        public static event Action OnConstructionCentersChanged;
        public static event Action<Outpost> OnOutpostSelected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            OnOpenPanel = null;
            OnUnitSelected = null;
            OnUnitDeselected = null;
            OnUnitMoveCommand = null;
            OnUnitAttackTargetChanged = null;
            OnFactorySelected = null;
            OnConstructionCenterSelected = null;
            OnConstructionClosed = null;
            OnConstructionCentersChanged = null;
            OnOutpostSelected = null;
        }

        public static void RaiseOpenPanel(PanelType type) =>
            OnOpenPanel?.Invoke(type);

        public static void RaiseUnitSelected(GameObject unit) =>
            OnUnitSelected?.Invoke(unit);

        public static void RaiseUnitDeselected(GameObject unit) =>
            OnUnitDeselected?.Invoke(unit);

        public static void RaiseUnitMoveCommand(GameObject unit, Vector3 destination) =>
            OnUnitMoveCommand?.Invoke(unit, destination);

        public static void RaiseUnitAttackTargetChanged(GameObject unit, Transform target) =>
            OnUnitAttackTargetChanged?.Invoke(unit, target);

        public static void RaiseFactorySelected(BuildingProduction factory) =>
            OnFactorySelected?.Invoke(factory);

        public static void RaiseConstructionCenterSelected(ConstructionCenter constructionCenter) =>
            OnConstructionCenterSelected?.Invoke(constructionCenter);

        public static void RaiseConstructionClosed() =>
            OnConstructionClosed?.Invoke();

        public static void RaiseConstructionCentersChanged() =>
            OnConstructionCentersChanged?.Invoke();

        public static void RaiseOutpostSelected(Outpost outpost) =>
            OnOutpostSelected?.Invoke(outpost);
    }
}
