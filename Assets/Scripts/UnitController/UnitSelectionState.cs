using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Units
{
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

        private void Select(GameObject unit)
        {
            if (unit != gameObject)
                return;

            IsSelected = true;
            ShowSelection();
        }

        private void Deselect(GameObject unit)
        {
            if (unit != gameObject)
                return;

            IsSelected = false;
            HideSelection();
        }
        
        private void ShowSelection()
        {
            if (_selectionVisual != null)
                _selectionVisual.SetActive(true);
        }

        private void HideSelection()
        {
            if (_selectionVisual != null)
                _selectionVisual.SetActive(false);
        }
    }
}