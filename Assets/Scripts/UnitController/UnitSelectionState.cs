using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
    /// <summary>
    /// Tracks whether this unit is currently selected by the player and toggles a selection
    /// visual (e.g. circle decal) accordingly. Subscribes to EventManager selection events.
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
        /// Marks this unit as selected and activates the selection visual when the event targets this GameObject.
        /// </summary>
        private void Select(GameObject unit)
        {
            if (unit != gameObject)
                return;

            IsSelected = true;
            ShowSelection();
        }

        /// <summary>
        /// Marks this unit as deselected and hides the selection visual when the event targets this GameObject.
        /// </summary>
        private void Deselect(GameObject unit)
        {
            if (unit != gameObject)
                return;

            IsSelected = false;
            HideSelection();
        }

        /// <summary>
        /// Activates the selection visual GameObject if one is assigned.
        /// </summary>
        private void ShowSelection()
        {
            if (_selectionVisual != null)
                _selectionVisual.SetActive(true);
        }

        /// <summary>
        /// Deactivates the selection visual GameObject if one is assigned.
        /// </summary>
        private void HideSelection()
        {
            if (_selectionVisual != null)
                _selectionVisual.SetActive(false);
        }
    }
}
