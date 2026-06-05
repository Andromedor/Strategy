using System;
using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.UI;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    /// <summary>
    /// Статична шина міжсистемних подій для RTS-гри. Усі крос-системні комунікації мають проходити через
    /// цей клас замість прямих посилань на компоненти. Статичні події очищаються при кожному перезавантаженні домену
    /// через RuntimeInitializeOnLoadMethod, щоб запобігти застарілим підписникам між ігровими сесіями.
    /// </summary>
    public static class EventManager
    {
        public static event Action<PanelType> OnOpenPanel;
        public static event Action<GameObject> OnUnitSelected;
        public static event Action<GameObject> OnUnitDeselected;
        public static event Action<GameObject> OnBuildingSelected;
        public static event Action<GameObject> OnBuildingDeselected;
        public static event Action<IReadOnlyList<GameObject>> OnSelectionChanged;
        public static event Action<GameObject> OnUnitDestroyed;
        public static event Action<GameObject, Vector3> OnUnitMoveCommand;
        public static event Action<GameObject, Transform> OnUnitAttackTargetChanged;
        public static event Action<int, int, Sprite, string> OnControlGroupUpdated;
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
            OnBuildingSelected = null;
            OnBuildingDeselected = null;
            OnSelectionChanged = null;
            OnUnitDestroyed = null;
            OnUnitMoveCommand = null;
            OnUnitAttackTargetChanged = null;
            OnControlGroupUpdated = null;
            OnFactorySelected = null;
            OnConstructionCenterSelected = null;
            OnConstructionClosed = null;
            OnConstructionCentersChanged = null;
            OnOutpostSelected = null;
        }

        /// <summary>Запитує HUD відкрити вказаний тип панелі.</summary>
        public static void RaiseOpenPanel(PanelType type) =>
            OnOpenPanel?.Invoke(type);

        /// <summary>Сповіщає слухачів, що юніт додано до виділення гравця.</summary>
        public static void RaiseUnitSelected(GameObject unit) =>
            OnUnitSelected?.Invoke(unit);

        /// <summary>Сповіщає слухачів, що юніт видалено з виділення гравця.</summary>
        public static void RaiseUnitDeselected(GameObject unit) =>
            OnUnitDeselected?.Invoke(unit);

        public static void RaiseBuildingSelected(GameObject building) =>
            OnBuildingSelected?.Invoke(building);

        public static void RaiseBuildingDeselected(GameObject building) =>
            OnBuildingDeselected?.Invoke(building);

        public static void RaiseSelectionChanged(IReadOnlyList<GameObject> selection) =>
            OnSelectionChanged?.Invoke(selection);

        /// <summary>Сповіщає системи, що юніт знищений і має бути прибраний із довгоживучих списків на кшталт control groups.</summary>
        public static void RaiseUnitDestroyed(GameObject unit) =>
            OnUnitDestroyed?.Invoke(unit);

        /// <summary>Розсилає наказ переміщення юніта до вказаної точки у світових координатах.</summary>
        public static void RaiseUnitMoveCommand(GameObject unit, Vector3 destination) =>
            OnUnitMoveCommand?.Invoke(unit, destination);

        /// <summary>Розсилає повідомлення, що юніту задано ручну ціль атаки.</summary>
        public static void RaiseUnitAttackTargetChanged(GameObject unit, Transform target) =>
            OnUnitAttackTargetChanged?.Invoke(unit, target);

        /// <summary>Оновлює HUD-індикатор control group: номер групи та кількість живих юнітів у ній.</summary>
        public static void RaiseControlGroupUpdated(int groupNumber, int unitCount) =>
            RaiseControlGroupUpdated(groupNumber, unitCount, null, string.Empty);

        public static void RaiseControlGroupUpdated(int groupNumber, int unitCount, Sprite icon, string fallbackText) =>
            OnControlGroupUpdated?.Invoke(groupNumber, unitCount, icon, fallbackText);

        /// <summary>Сповіщає HUD, що обрано будівлю-завод.</summary>
        public static void RaiseFactorySelected(BuildingProduction factory) =>
            OnFactorySelected?.Invoke(factory);

        /// <summary>Сповіщає системи (включно з BuildingPlacementManager), що обрано центр будівництва.</summary>
        public static void RaiseConstructionCenterSelected(ConstructionCenter constructionCenter) =>
            OnConstructionCenterSelected?.Invoke(constructionCenter);

        /// <summary>Сигналізує, що панель будівництва закрита (наприклад, клік на порожню землю).</summary>
        public static void RaiseConstructionClosed() =>
            OnConstructionClosed?.Invoke();

        /// <summary>Викидається, коли ConstructionCenter вмикається або вимикається, щоб UI міг оновити доступні варіанти будівництва.</summary>
        public static void RaiseConstructionCentersChanged() =>
            OnConstructionCentersChanged?.Invoke();

        /// <summary>Сповіщає HUD, що обрано аванпост гравця, передаючи посилання на Outpost для панелі.</summary>
        public static void RaiseOutpostSelected(Outpost outpost) =>
            OnOutpostSelected?.Invoke(outpost);
    }
}
