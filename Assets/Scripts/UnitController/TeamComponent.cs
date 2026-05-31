using System;
using UnityEngine;

namespace Strategy.Units
{
    public class TeamComponent : MonoBehaviour, ITeam
    {
        public event Action<TeamType> TeamChanged;

        [SerializeField] private TeamType _team;

        public TeamType Team => _team;

        public void SetTeam(TeamType team)
        {
            if (_team == team)
                return;

            _team = team;
            TeamChanged?.Invoke(_team);
        }
    }

    public enum TeamType
    {
        Player,
        Enemy
    }
}
