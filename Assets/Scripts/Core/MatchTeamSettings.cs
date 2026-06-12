using System;
using System.Collections.Generic;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    [DefaultExecutionOrder(-1000)]
    public class MatchTeamSettings : MonoBehaviour
    {
        [SerializeField] private TeamType _localTeam = TeamType.Player;
        [SerializeField, Min(0)] private int _localPlayerId;
        [SerializeField] private List<TeamSlot> _teams = new()
        {
            new TeamSlot(TeamType.Player, 1, TeamControllerKind.LocalHuman, 0),
            new TeamSlot(TeamType.Enemy, 2, TeamControllerKind.AI, 1)
        };

        public IReadOnlyList<TeamSlot> Teams => _teams;
        public static MatchTeamSettings Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Active = null;
        }

        private void Awake()
        {
            Active = this;
            ApplyLaunchConfig(MatchLaunchContext.CurrentConfig);
            Apply();
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        private void OnValidate()
        {
            if (_teams == null)
                _teams = new List<TeamSlot>();
        }

        public void Apply()
        {
            LocalPlayerContext.SetLocalPlayer(_localTeam, _localPlayerId);
            TeamRelations.ClearAlliances();

            for (int i = 0; i < _teams.Count; i++)
            {
                TeamSlot first = _teams[i];

                for (int j = i + 1; j < _teams.Count; j++)
                {
                    TeamSlot second = _teams[j];

                    if (first.Team == second.Team ||
                        first.AllianceId <= 0 ||
                        first.AllianceId != second.AllianceId)
                    {
                        continue;
                    }

                    TeamRelations.SetAlliance(first.Team, second.Team, true);
                }
            }
        }

        public void ApplyLaunchConfig(MatchLaunchConfig config)
        {
            if (config == null)
                return;

            _localTeam = config.LocalTeam;
            _localPlayerId = config.LocalPlayerId;
            _teams.Clear();

            for (int i = 0; i < config.Teams.Count; i++)
            {
                TeamLaunchSlot slot = config.Teams[i];
                if (slot != null && slot.Team != TeamType.Neutral)
                    _teams.Add(slot.ToTeamSlot());
            }
        }
    }

    [Serializable]
    public struct TeamSlot
    {
        [SerializeField] private TeamType _team;
        [SerializeField] private int _allianceId;
        [SerializeField] private TeamControllerKind _controller;
        [SerializeField] private int _playerId;

        public TeamType Team => _team;
        public int AllianceId => _allianceId;
        public TeamControllerKind Controller => _controller;
        public int PlayerId => _playerId;

        public TeamSlot(
            TeamType team,
            int allianceId,
            TeamControllerKind controller,
            int playerId)
        {
            _team = team;
            _allianceId = allianceId;
            _controller = controller;
            _playerId = playerId;
        }
    }

    public enum TeamControllerKind
    {
        LocalHuman,
        RemoteHuman,
        AI
    }
}
