using System;
using System.Collections.Generic;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    [DisallowMultipleComponent]
    public class PlayerStartPoint : MonoBehaviour
    {
        public static readonly List<PlayerStartPoint> All = new();

        [SerializeField, Min(0)] private int _slotIndex;
        [SerializeField] private List<int> _enabledForPlayerCounts = new() { 2, 4 };
        [SerializeField] private TeamType _editorPreviewTeam = TeamType.Neutral;

        public int SlotIndex => Mathf.Max(0, _slotIndex);
        public TeamType EditorPreviewTeam => _editorPreviewTeam;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            All.Clear();
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        public bool IsEnabledForPlayerCount(int playerCount)
        {
            if (_enabledForPlayerCounts == null || _enabledForPlayerCounts.Count == 0)
                return true;

            return _enabledForPlayerCounts.Contains(playerCount);
        }

        private void OnValidate()
        {
            _slotIndex = Mathf.Max(0, _slotIndex);
            _enabledForPlayerCounts ??= new List<int>();

            for (int i = _enabledForPlayerCounts.Count - 1; i >= 0; i--)
            {
                if (_enabledForPlayerCounts[i] <= 0)
                    _enabledForPlayerCounts.RemoveAt(i);
            }
        }

        private void OnDrawGizmos()
        {
            Color color = _editorPreviewTeam switch
            {
                TeamType.Player => Color.green,
                TeamType.Enemy => Color.red,
                TeamType.Team3 => Color.yellow,
                TeamType.Team4 => new Color(0.6f, 0.25f, 1f, 1f),
                _ => Color.cyan
            };

            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 1.25f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 4f);
        }
    }

    [Serializable]
    public struct StartingUnitEntry
    {
        [SerializeField] private Strategy.Data.UnitData _unitData;
        [SerializeField, Min(0)] private int _count;
        [SerializeField] private Vector3 _localOffset;

        public Strategy.Data.UnitData UnitData => _unitData;
        public int Count => Mathf.Max(0, _count);
        public Vector3 LocalOffset => _localOffset;
    }
}
