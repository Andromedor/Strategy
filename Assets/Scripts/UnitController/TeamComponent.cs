using UnityEngine;

namespace UnitController
{
    public class TeamComponent: MonoBehaviour, ITeam
    {
        [SerializeField] private TeamType _team;

        public TeamType Team => _team;

        public void SetTeam(TeamType team)
        {
            _team = team;
        }
    }
    
    public enum TeamType
    {
        Player,
        Enemy
    }
}