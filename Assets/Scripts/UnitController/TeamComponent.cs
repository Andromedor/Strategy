using System;
using UnityEngine;

namespace Strategy.Units
{
    /// <summary>
    /// Реалізація MonoBehaviour для ITeam. Зберігає призначення команди юніта та викидає подію
    /// TeamChanged щоразу, коли команда змінюється під час виконання (наприклад, захоплення опорного пункту або налагодження).
    /// </summary>
    public class TeamComponent : MonoBehaviour, ITeam
    {
        public event Action<TeamType> TeamChanged;

        [SerializeField] private TeamType _team;

        public TeamType Team => _team;

        /// <summary>
        /// Змінює команду юніта та транслює TeamChanged всім підписникам (наприклад, UnitCombat
        /// перебудовує маску шару цілі). Нічого не робить, якщо нова команда збігається з поточною.
        /// </summary>
        public void SetTeam(TeamType team)
        {
            if (_team == team)
                return;

            _team = team;
            TeamChanged?.Invoke(_team);
        }
    }

    /// <summary>
    /// Визначає, до якої сторони належить юніт або будівля.
    /// </summary>
    public enum TeamType
    {
        Player,
        Enemy,
        Neutral,
        Team3,
        Team4,
        Team5,
        Team6,
        Team7,
        Team8
    }
}
