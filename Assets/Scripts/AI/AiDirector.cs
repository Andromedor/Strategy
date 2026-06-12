using System;
using System.Collections.Generic;
using Strategy.Core;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;

namespace Strategy.AI
{
    [Serializable]
    public struct AiTeamProfileOverride
    {
        [SerializeField] private TeamType _team;
        [SerializeField] private AiDifficultyProfile _profile;

        public TeamType Team => _team;
        public AiDifficultyProfile Profile => _profile;
    }

    [DefaultExecutionOrder(-650)]
    [DisallowMultipleComponent]
    public class AiDirector : MonoBehaviour
    {
        [Header("Profiles")]
        [SerializeField] private AiDifficultyProfile _easyProfile;
        [SerializeField] private AiDifficultyProfile _mediumProfile;
        [SerializeField] private AiDifficultyProfile _hardProfile;
        [SerializeField] private AiDifficultyProfile _defaultProfile;
        [SerializeField] private List<AiTeamProfileOverride> _teamProfileOverrides = new();

        [Header("Build")]
        [SerializeField] private BuildingData _factoryData;
        [SerializeField] private BuildingPlacementGridConfig _gridConfig;
        [SerializeField] private LayerMask _buildingBlockMask;
        [SerializeField, Min(0)] private int _buildSearchRadiusCells = 8;

        private readonly List<AiController> _controllers = new();

        public IReadOnlyList<AiController> Controllers => _controllers;

        private void Start()
        {
            SpawnControllersForMatch();
        }

        public void SpawnControllersForMatch()
        {
            MatchTeamSettings matchSettings = MatchTeamSettings.Active;
            if (matchSettings == null)
                return;

            IReadOnlyList<TeamSlot> teams = matchSettings.Teams;
            for (int i = 0; i < teams.Count; i++)
            {
                TeamSlot slot = teams[i];
                if (slot.Team == TeamType.Neutral || slot.Controller != TeamControllerKind.AI)
                    continue;

                if (FindController(slot.Team) != null)
                    continue;

                GameObject controllerObject = new($"AI Controller ({slot.Team})");
                controllerObject.transform.SetParent(transform, false);
                AiController controller = controllerObject.AddComponent<AiController>();
                controller.Initialize(
                    slot.Team,
                    slot.PlayerId,
                    ResolveProfile(slot.Team),
                    _factoryData,
                    _gridConfig,
                    _buildingBlockMask,
                    _buildSearchRadiusCells);
                _controllers.Add(controller);
            }
        }

        private AiController FindController(TeamType team)
        {
            for (int i = 0; i < _controllers.Count; i++)
            {
                AiController controller = _controllers[i];
                if (controller != null && controller.Team == team)
                    return controller;
            }

            return null;
        }

        private AiDifficultyProfile ResolveProfile(TeamType team)
        {
            MatchLaunchConfig launchConfig = MatchLaunchContext.CurrentConfig;
            TeamLaunchSlot launchSlot = launchConfig != null ? launchConfig.FindTeam(team) : null;
            if (launchSlot != null)
            {
                AiDifficultyProfile launchProfile = GetProfile(launchSlot.AiDifficulty);
                if (launchProfile != null)
                    return launchProfile;
            }

            if (_teamProfileOverrides != null)
            {
                for (int i = 0; i < _teamProfileOverrides.Count; i++)
                {
                    AiTeamProfileOverride overrideEntry = _teamProfileOverrides[i];
                    if (overrideEntry.Team == team && overrideEntry.Profile != null)
                        return overrideEntry.Profile;
                }
            }

            if (_defaultProfile != null)
                return _defaultProfile;

            if (_mediumProfile != null)
                return _mediumProfile;

            return AiDifficultyProfile.CreateRuntimeDefault(AiDifficultyLevel.Medium);
        }

        public AiDifficultyProfile GetProfile(AiDifficultyLevel level)
        {
            return level switch
            {
                AiDifficultyLevel.Easy => _easyProfile,
                AiDifficultyLevel.Hard => _hardProfile,
                _ => _mediumProfile
            };
        }
    }
}
