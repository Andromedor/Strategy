using Strategy.Core;
using UnityEngine;

namespace Strategy.UI
{
    /// <summary>
    /// HUD-смуга для control groups 1-9. Слухає тільки агреговану кількість, щоб UI не залежав
    /// від gameplay-об'єктів та не керував вибором напряму.
    /// </summary>
    public class ControlGroupBarUI : MonoBehaviour
    {
        [SerializeField] private ControlGroupSlotUI[] _slots;

        private void OnEnable()
        {
            EventManager.OnControlGroupUpdated += OnControlGroupUpdated;
            ResetSlots();
        }

        private void OnDisable()
        {
            EventManager.OnControlGroupUpdated -= OnControlGroupUpdated;
        }

        private void ResetSlots()
        {
            if (_slots == null)
                return;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                    _slots[i].SetData(i + 1, 0);
            }
        }

        private void OnControlGroupUpdated(int groupNumber, int unitCount, Sprite icon, string fallbackText)
        {
            if (_slots == null || groupNumber < 1 || groupNumber > _slots.Length)
                return;

            ControlGroupSlotUI slot = _slots[groupNumber - 1];
            if (slot != null)
                slot.SetData(groupNumber, unitCount, icon, fallbackText);
        }
    }
}
