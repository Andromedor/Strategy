using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Відстежує, чи обраний зараз цей юніт гравцем, та відповідно перемикає
    /// візуальний індикатор вибору (наприклад, кругову деталь). Підписується на події вибору EventManager.
    /// </summary>
    public class UnitSelectionState: MonoBehaviour
    {
        [Header("Selection Visual")]
        [SerializeField] private GameObject _selectionVisual;

        public bool IsSelected { get; private set; }

        private void OnEnable()
        {
            EventManager.OnUnitSelected += Select;
            EventManager.OnUnitDeselected += Deselect;
        }

        private void OnDisable()
        {
            EventManager.OnUnitSelected -= Select;
            EventManager.OnUnitDeselected -= Deselect;
        }

        /// <summary>
        /// Позначає цей юніт як обраний та активує візуальний індикатор вибору, коли подія стосується цього GameObject.
        /// </summary>
        private void Select(GameObject unit)
        {
            if (unit != gameObject)
                return;

            IsSelected = true;
            ShowSelection();
        }

        /// <summary>
        /// Позначає цей юніт як скасовано вибраний та приховує візуальний індикатор вибору, коли подія стосується цього GameObject.
        /// </summary>
        private void Deselect(GameObject unit)
        {
            if (unit != gameObject)
                return;

            IsSelected = false;
            HideSelection();
        }

        /// <summary>
        /// Активує GameObject візуального індикатора вибору, якщо він призначений.
        /// </summary>
        private void ShowSelection()
        {
            if (_selectionVisual != null)
                _selectionVisual.SetActive(true);
        }

        /// <summary>
        /// Деактивує GameObject візуального індикатора вибору, якщо він призначений.
        /// </summary>
        private void HideSelection()
        {
            if (_selectionVisual != null)
                _selectionVisual.SetActive(false);
        }
    }
}
