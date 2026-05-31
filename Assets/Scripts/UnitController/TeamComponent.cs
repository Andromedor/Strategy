using System;
using UnityEngine;

namespace Strategy.Units
{
    /// <summary>
    /// MonoBehaviour implementation of ITeam. Stores the unit's team assignment and fires a
    /// TeamChanged event whenever the team is switched at runtime (e.g. outpost capture or debug).
    /// </summary>
    public class TeamComponent : MonoBehaviour, ITeam
    {
        public event Action<TeamType> TeamChanged;

        [SerializeField] private TeamType _team;

        public TeamType Team => _team;

        /// <summary>
        /// Changes the unit's team and broadcasts TeamChanged to all subscribers (e.g. UnitCombat
        /// rebuilds its target layer mask). Does nothing if the new team equals the current one.
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
    /// Identifies which side a unit or building belongs to.
    /// </summary>
    public enum TeamType
    {
        Player,
        Enemy
    }
}
