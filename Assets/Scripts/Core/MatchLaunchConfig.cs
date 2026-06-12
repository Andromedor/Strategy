using System;
using System.Collections.Generic;
using Strategy.AI;
using Strategy.Maps;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    public enum MatchLaunchMode
    {
        OfflineBots,
        OnlineHost,
        OnlineClient
    }

    public enum SkirmishTeamMode
    {
        OneVsOne = 2,
        ThreePlayer = 3,
        TwoVsTwo = 4
    }

    [Serializable]
    public sealed class TeamLaunchSlot
    {
        [SerializeField] private TeamType _team;
        [SerializeField] private int _allianceId;
        [SerializeField] private TeamControllerKind _controller;
        [SerializeField] private int _playerId;
        [SerializeField] private int _spawnSlotIndex;
        [SerializeField] private string _playerName;
        [SerializeField] private AiDifficultyLevel _aiDifficulty;
        [SerializeField] private int _startingResources;

        public TeamType Team => _team;
        public int AllianceId => _allianceId;
        public TeamControllerKind Controller => _controller;
        public int PlayerId => _playerId;
        public int SpawnSlotIndex => Mathf.Max(0, _spawnSlotIndex);
        public string PlayerName => string.IsNullOrWhiteSpace(_playerName) ? _team.ToString() : _playerName;
        public AiDifficultyLevel AiDifficulty => _aiDifficulty;
        public int StartingResources => Mathf.Max(0, _startingResources);

        public TeamLaunchSlot(
            TeamType team,
            int allianceId,
            TeamControllerKind controller,
            int playerId,
            int spawnSlotIndex,
            string playerName,
            AiDifficultyLevel aiDifficulty,
            int startingResources)
        {
            _team = team;
            _allianceId = allianceId;
            _controller = controller;
            _playerId = Mathf.Max(0, playerId);
            _spawnSlotIndex = Mathf.Max(0, spawnSlotIndex);
            _playerName = playerName;
            _aiDifficulty = aiDifficulty;
            _startingResources = Mathf.Max(0, startingResources);
        }

        public TeamSlot ToTeamSlot()
        {
            return new TeamSlot(_team, _allianceId, _controller, _playerId);
        }
    }

    [Serializable]
    public sealed class MatchLaunchConfig
    {
        [SerializeField] private MatchLaunchMode _mode;
        [SerializeField] private SkirmishTeamMode _teamMode;
        [SerializeField] private MapDefinition _map;
        [SerializeField] private string _mapId;
        [SerializeField] private int _localPlayerId;
        [SerializeField] private TeamType _localTeam = TeamType.Player;
        [SerializeField] private List<TeamLaunchSlot> _teams = new();

        public MatchLaunchMode Mode => _mode;
        public SkirmishTeamMode TeamMode => _teamMode;
        public MapDefinition Map => _map;
        public string MapId => !string.IsNullOrWhiteSpace(_mapId) ? _mapId : _map != null ? _map.MapId : string.Empty;
        public int LocalPlayerId => Mathf.Max(0, _localPlayerId);
        public TeamType LocalTeam => _localTeam;
        public IReadOnlyList<TeamLaunchSlot> Teams => _teams;
        public bool IsOnline => _mode == MatchLaunchMode.OnlineHost || _mode == MatchLaunchMode.OnlineClient;
        public bool AllowsSaving => _mode == MatchLaunchMode.OfflineBots;

        public MatchLaunchConfig(
            MatchLaunchMode mode,
            SkirmishTeamMode teamMode,
            MapDefinition map,
            TeamType localTeam,
            int localPlayerId,
            IEnumerable<TeamLaunchSlot> teams)
        {
            _mode = mode;
            _teamMode = teamMode;
            _map = map;
            _mapId = map != null ? map.MapId : string.Empty;
            _localTeam = localTeam;
            _localPlayerId = Mathf.Max(0, localPlayerId);
            _teams = teams != null ? new List<TeamLaunchSlot>(teams) : new List<TeamLaunchSlot>();
        }

        public TeamLaunchSlot FindTeam(TeamType team)
        {
            for (int i = 0; i < _teams.Count; i++)
            {
                if (_teams[i] != null && _teams[i].Team == team)
                    return _teams[i];
            }

            return null;
        }

        public static MatchLaunchConfig CreateDefault(
            MapDefinition map,
            SkirmishTeamMode teamMode,
            AiDifficultyLevel aiDifficulty,
            int startingResources,
            MatchLaunchMode mode = MatchLaunchMode.OfflineBots)
        {
            int resources = Mathf.Max(0, startingResources);
            List<TeamLaunchSlot> teams = new()
            {
                new TeamLaunchSlot(TeamType.Player, 1, TeamControllerKind.LocalHuman, 0, 0, "Player", aiDifficulty, resources),
                new TeamLaunchSlot(TeamType.Enemy, 2, TeamControllerKind.AI, 1, 1, "Bot 1", aiDifficulty, resources)
            };

            if (teamMode == SkirmishTeamMode.TwoVsTwo)
            {
                teams.Add(new TeamLaunchSlot(TeamType.Team3, 1, TeamControllerKind.AI, 2, 2, "Ally Bot", aiDifficulty, resources));
                teams.Add(new TeamLaunchSlot(TeamType.Team4, 2, TeamControllerKind.AI, 3, 3, "Bot 2", aiDifficulty, resources));
            }
            else if (teamMode == SkirmishTeamMode.ThreePlayer)
            {
                teams.Add(new TeamLaunchSlot(TeamType.Team3, 3, TeamControllerKind.AI, 2, 2, "Bot 2", aiDifficulty, resources));
            }

            return new MatchLaunchConfig(mode, teamMode, map, TeamType.Player, 0, teams);
        }
    }
}
