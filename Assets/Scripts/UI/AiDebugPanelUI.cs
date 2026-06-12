using Strategy.AI;
using Strategy.Core;
using Strategy.Maps;
using Strategy.Units;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.UI
{
    public class AiDebugPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private Button _spawnMatchButton;
        [SerializeField] private TMP_Dropdown _teamModeDropdown;
        [SerializeField] private TMP_Dropdown _difficultyDropdown;
        [SerializeField] private TMP_InputField _startingResourcesInput;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private MapCatalog _mapCatalog;
        [SerializeField] private bool _showInDevelopmentBuild;

        private readonly List<TeamResourceAmount> _resourceBuffer = new();

        private void Awake()
        {
            bool shouldShow = Application.isEditor || _showInDevelopmentBuild && Debug.isDebugBuild;
            GameObject root = _root != null ? _root : gameObject;
            root.SetActive(shouldShow);
            PopulateDropdowns();
            RefreshLabel(AiRuntimeSettings.IsAiEnabled);
        }

        private void OnEnable()
        {
            if (_toggleButton != null)
                _toggleButton.onClick.AddListener(ToggleAi);
            if (_spawnMatchButton != null)
                _spawnMatchButton.onClick.AddListener(SpawnMatch);

            AiRuntimeSettings.AiEnabledChanged += RefreshLabel;
        }

        private void OnDisable()
        {
            if (_toggleButton != null)
                _toggleButton.onClick.RemoveListener(ToggleAi);
            if (_spawnMatchButton != null)
                _spawnMatchButton.onClick.RemoveListener(SpawnMatch);

            AiRuntimeSettings.AiEnabledChanged -= RefreshLabel;
        }

        private void ToggleAi()
        {
            AiRuntimeSettings.SetAllAiEnabled(!AiRuntimeSettings.IsAiEnabled);
        }

        private void SpawnMatch()
        {
            MatchStartSpawner spawner = FindFirstObjectByType<MatchStartSpawner>();
            if (spawner == null)
            {
                SetStatus("MatchStartSpawner not found.");
                return;
            }

            MapDefinition map = _mapCatalog != null && _mapCatalog.Maps.Count > 0 ? _mapCatalog.Maps[0] : null;
            MatchLaunchConfig config = MatchLaunchConfig.CreateDefault(
                map,
                SelectedTeamMode,
                SelectedDifficulty,
                SelectedStartingResources);
            MatchLaunchContext.SetConfig(config);

            if (MatchTeamSettings.Active != null)
            {
                MatchTeamSettings.Active.ApplyLaunchConfig(config);
                MatchTeamSettings.Active.Apply();
            }

            if (ResourceManager.Instance != null)
            {
                _resourceBuffer.Clear();
                for (int i = 0; i < config.Teams.Count; i++)
                {
                    TeamLaunchSlot slot = config.Teams[i];
                    _resourceBuffer.Add(new TeamResourceAmount(slot.Team, slot.StartingResources));
                }

                ResourceManager.Instance.RestoreResources(_resourceBuffer);
            }

            bool spawned = spawner.SpawnAll();
            FindFirstObjectByType<AiDirector>()?.SpawnControllersForMatch();
            SetStatus(spawned ? "Match spawned." : "Match already spawned or invalid.");
        }

        private void PopulateDropdowns()
        {
            if (_teamModeDropdown != null && _teamModeDropdown.options.Count == 0)
            {
                _teamModeDropdown.AddOptions(new List<string> { "1 vs 1", "3 players", "2 vs 2" });
            }

            if (_difficultyDropdown != null && _difficultyDropdown.options.Count == 0)
            {
                _difficultyDropdown.AddOptions(new List<string> { "Easy", "Medium", "Hard" });
                _difficultyDropdown.value = 1;
            }
        }

        private void RefreshLabel(bool enabled)
        {
            if (_labelText != null)
                _labelText.text = enabled ? "AI: ON" : "AI: OFF";
        }

        private SkirmishTeamMode SelectedTeamMode =>
            _teamModeDropdown != null
                ? _teamModeDropdown.value switch
                {
                    1 => SkirmishTeamMode.ThreePlayer,
                    2 => SkirmishTeamMode.TwoVsTwo,
                    _ => SkirmishTeamMode.OneVsOne
                }
                : SkirmishTeamMode.OneVsOne;

        private AiDifficultyLevel SelectedDifficulty =>
            _difficultyDropdown != null
                ? _difficultyDropdown.value switch
                {
                    0 => AiDifficultyLevel.Easy,
                    2 => AiDifficultyLevel.Hard,
                    _ => AiDifficultyLevel.Medium
                }
                : AiDifficultyLevel.Medium;

        private int SelectedStartingResources =>
            _startingResourcesInput != null && int.TryParse(_startingResourcesInput.text, out int value)
                ? Mathf.Max(0, value)
                : 500;

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }
    }
}
