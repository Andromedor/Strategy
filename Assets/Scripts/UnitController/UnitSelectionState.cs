using UnityEngine;

namespace UnitController
{
    public class UnitSelectionState: MonoBehaviour
    {
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
        }

        private void Deselect(GameObject unit)
        {
            if (unit != gameObject)
                return;

            IsSelected = false;
        }
    }
}