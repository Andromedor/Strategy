using System;
using Strategy.Buildings;
using Strategy.UI;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    /// <summary>
    /// Static inter-system event bus for the RTS game. All cross-system communication should go through
    /// this class instead of direct component references. Static events are cleared on each domain reload
    /// via RuntimeInitializeOnLoadMethod to prevent stale subscribers between play sessions.
    /// </summary>
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

        /// <summary>Requests the HUD to open the specified panel type.</summary>
        public static void RaiseOpenPanel(PanelType type) =>
            OnOpenPanel?.Invoke(type);

        /// <summary>Notifies listeners that a unit has been added to the player's selection.</summary>
        public static void RaiseUnitSelected(GameObject unit) =>
            OnUnitSelected?.Invoke(unit);

        /// <summary>Notifies listeners that a unit has been removed from the player's selection.</summary>
        public static void RaiseUnitDeselected(GameObject unit) =>
            OnUnitDeselected?.Invoke(unit);

        /// <summary>Broadcasts a move order for a unit to the given world-space destination.</summary>
        public static void RaiseUnitMoveCommand(GameObject unit, Vector3 destination) =>
            OnUnitMoveCommand?.Invoke(unit, destination);

        /// <summary>Broadcasts that a unit has been given a manual attack target.</summary>
        public static void RaiseUnitAttackTargetChanged(GameObject unit, Transform target) =>
            OnUnitAttackTargetChanged?.Invoke(unit, target);

        /// <summary>Notifies the HUD that a factory building has been selected.</summary>
        public static void RaiseFactorySelected(BuildingProduction factory) =>
            OnFactorySelected?.Invoke(factory);

        /// <summary>Notifies systems (including BuildingPlacementManager) that a construction center has been selected.</summary>
        public static void RaiseConstructionCenterSelected(ConstructionCenter constructionCenter) =>
            OnConstructionCenterSelected?.Invoke(constructionCenter);

        /// <summary>Signals that the construction panel has been dismissed (e.g., click on empty ground).</summary>
        public static void RaiseConstructionClosed() =>
            OnConstructionClosed?.Invoke();

        /// <summary>Fired when a ConstructionCenter is enabled or disabled, so the UI can refresh available build options.</summary>
        public static void RaiseConstructionCentersChanged() =>
            OnConstructionCentersChanged?.Invoke();

        /// <summary>Notifies the HUD that a player-owned outpost has been selected, passing the Outpost reference for the panel.</summary>
        public static void RaiseOutpostSelected(Outpost outpost) =>
            OnOutpostSelected?.Invoke(outpost);
    }
}
