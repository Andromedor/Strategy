using System.Collections.Generic;
using Strategy.AI;
using Strategy.Core;
using Strategy.Maps;
using Strategy.Networking;
using Strategy.Save;
using Strategy.Units;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Strategy.Menu
{
    public sealed class GameMenuController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapCatalog _mapCatalog;

        [Header("Panels")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _skirmishModePanel;
        [SerializeField] private GameObject _skirmishPanel;
        [SerializeField] private GameObject _onlinePanel;
        [SerializeField] private GameObject _loadPanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("Main Buttons")]
        [SerializeField] private Button _skirmishButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitButton;

        [Header("Mode Buttons")]
        [SerializeField] private Button _offlineBotsButton;
        [SerializeField] private Button _onlineButton;
        [SerializeField] private Button _openOnlineHostSetupButton;
        [SerializeField] private Button _backFromModeButton;

        [Header("Match Setup")]
        [SerializeField] private TMP_Dropdown _mapDropdown;
        [SerializeField] private TMP_Dropdown _teamModeDropdown;
        [SerializeField] private TMP_Dropdown _difficultyDropdown;
        [SerializeField] private TMP_Dropdown _resourcesDropdown;
        [SerializeField] private TMP_InputField _customResourcesInput;
        [SerializeField] private TMP_Dropdown _localSpawnDropdown;
        [SerializeField] private TMP_Text _slotSummaryText;
        [SerializeField] private Button _startOfflineButton;
        [SerializeField] private Button _hostOnlineButton;
        [SerializeField] private Button _backFromSkirmishButton;

        [Header("Editable Slot Rows")]
        [SerializeField] private List<Button> _mapButtons = new();
        [SerializeField] private List<TMP_Text> _mapButtonLabels = new();
        [SerializeField] private List<TMP_Text> _slotSpawnLabels = new();
        [SerializeField] private List<TMP_Dropdown> _slotControllerDropdowns = new();
        [SerializeField] private List<TMP_Dropdown> _slotTeamDropdowns = new();
        [SerializeField] private List<TMP_Dropdown> _slotDifficultyDropdowns = new();

        [Header("Map Preview")]
        [SerializeField] private Image _mapPreviewImage;
        [SerializeField] private Sprite _fallbackMapPreview;
        [SerializeField] private TMP_Text _mapPreviewTitleText;
        [SerializeField] private TMP_Text _mapPreviewDetailsText;

        [Header("Online Join")]
        [SerializeField] private TMP_InputField _joinCodeInput;
        [SerializeField] private Button _joinOnlineButton;
        [SerializeField] private Button _backFromOnlineButton;

        [Header("Load")]
        [SerializeField] private TMP_Dropdown _saveDropdown;
        [SerializeField] private Button _loadSaveButton;
        [SerializeField] private Button _backFromLoadButton;

        [Header("Settings")]
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private Button _applyResolutionButton;
        [SerializeField] private Button _backFromSettingsButton;

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;

        [Header("Editable Match Options")]
        [SerializeField] private List<SkirmishTeamMode> _availableTeamModes = new()
        {
            SkirmishTeamMode.OneVsOne,
            SkirmishTeamMode.ThreePlayer,
            SkirmishTeamMode.TwoVsTwo
        };
        [SerializeField] private List<AiDifficultyLevel> _availableDifficulties = new()
        {
            AiDifficultyLevel.Easy,
            AiDifficultyLevel.Medium,
            AiDifficultyLevel.Hard
        };
        [SerializeField] private int _defaultDifficultyIndex = 1;
        [SerializeField] private List<int> _startingResourcePresets = new()
        {
            500,
            1000,
            2000,
            5000
        };
        [SerializeField] private bool _allowCustomStartingResources = true;

        private readonly List<MapDefinition> _visibleMaps = new();
        private readonly List<string> _saveFiles = new();
        private readonly List<Resolution> _resolutions = new();
        private MatchLaunchMode _activeSetupMode = MatchLaunchMode.OfflineBots;
        private int _selectedMapIndex;
        private string _selectedMapId;
        private bool _hasManualMapSelection;
        private MatchLaunchConfig _pendingOnlineHostConfig;
        private string _createdLobbyCode;

        private void Awake()
        {
            EnsureEditableOptions();
            BindButtons();
            PopulateModeOptions();
            PopulateDifficultyOptions();
            PopulateResourceOptions();
            PopulateSlotOptions();
            PopulateResolutions();
            ShowPanel(_mainPanel);
            RefreshMaps();
            RefreshSaves();
        }

        private void OnValidate()
        {
            EnsureEditableOptions();
            _defaultDifficultyIndex = Mathf.Clamp(_defaultDifficultyIndex, 0, Mathf.Max(0, _availableDifficulties.Count - 1));

            for (int i = 0; i < _startingResourcePresets.Count; i++)
                _startingResourcePresets[i] = Mathf.Max(0, _startingResourcePresets[i]);
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void BindButtons()
        {
            Add(_skirmishButton, () => ShowPanel(_skirmishModePanel));
            Add(_loadButton, OpenLoadPanel);
            Add(_settingsButton, () => ShowPanel(_settingsPanel));
            Add(_exitButton, ExitGame);
            Add(_offlineBotsButton, OpenOfflineSetup);
            Add(_onlineButton, () => ShowPanel(_onlinePanel));
            Add(_openOnlineHostSetupButton, OpenOnlineHostSetup);
            Add(_backFromModeButton, () => ShowPanel(_mainPanel));
            Add(_startOfflineButton, StartOfflineMatch);
            Add(_hostOnlineButton, HostOnlineMatch);
            Add(_joinOnlineButton, JoinOnlineMatch);
            Add(_backFromSkirmishButton, () => ShowPanel(_skirmishModePanel));
            Add(_backFromOnlineButton, () => ShowPanel(_skirmishModePanel));
            Add(_loadSaveButton, LoadSelectedSave);
            Add(_backFromLoadButton, () => ShowPanel(_mainPanel));
            Add(_applyResolutionButton, ApplySelectedResolution);
            Add(_backFromSettingsButton, () => ShowPanel(_mainPanel));

            if (_teamModeDropdown != null)
                _teamModeDropdown.onValueChanged.AddListener(_ =>
                {
                    ResetSlotTeamsForMode();
                    RefreshMaps();
                });
            if (_mapDropdown != null)
                _mapDropdown.onValueChanged.AddListener(SelectMap);
            if (_localSpawnDropdown != null)
                _localSpawnDropdown.onValueChanged.AddListener(_ => RefreshSlotSummary());
            if (_difficultyDropdown != null)
                _difficultyDropdown.onValueChanged.AddListener(_ => RefreshSlotRows());

            for (int i = 0; i < _mapButtons.Count; i++)
            {
                int mapIndex = i;
                Add(_mapButtons[i], () => SelectMap(mapIndex));
            }

            for (int i = 0; i < _slotControllerDropdowns.Count; i++)
                AddDropdownListener(_slotControllerDropdowns[i], RefreshSlotRows);

            for (int i = 0; i < _slotTeamDropdowns.Count; i++)
                AddDropdownListener(_slotTeamDropdowns[i], RefreshSlotSummary);

            for (int i = 0; i < _slotDifficultyDropdowns.Count; i++)
                AddDropdownListener(_slotDifficultyDropdowns[i], RefreshSlotSummary);
        }

        private void UnbindButtons()
        {
            Remove(_skirmishButton);
            Remove(_loadButton);
            Remove(_settingsButton);
            Remove(_exitButton);
            Remove(_offlineBotsButton);
            Remove(_onlineButton);
            Remove(_openOnlineHostSetupButton);
            Remove(_backFromModeButton);
            Remove(_startOfflineButton);
            Remove(_hostOnlineButton);
            Remove(_joinOnlineButton);
            Remove(_backFromSkirmishButton);
            Remove(_backFromOnlineButton);
            Remove(_loadSaveButton);
            Remove(_backFromLoadButton);
            Remove(_applyResolutionButton);
            Remove(_backFromSettingsButton);

            for (int i = 0; i < _mapButtons.Count; i++)
                Remove(_mapButtons[i]);
        }

        private void OpenOfflineSetup()
        {
            _activeSetupMode = MatchLaunchMode.OfflineBots;
            ShowPanel(_skirmishPanel);
            RefreshMaps();
        }

        private void OpenOnlineHostSetup()
        {
            _activeSetupMode = MatchLaunchMode.OnlineHost;
            ClearPendingOnlineLobby();
            ShowPanel(_skirmishPanel);
            RefreshMaps();
        }

        private void OpenLoadPanel()
        {
            RefreshSaves();
            ShowPanel(_loadPanel);
        }

        private void PopulateModeOptions()
        {
            if (_teamModeDropdown == null)
                return;

            EnsureEditableOptions();
            _teamModeDropdown.ClearOptions();
            List<string> options = new();
            for (int i = 0; i < _availableTeamModes.Count; i++)
                options.Add(FormatMode(_availableTeamModes[i]));
            _teamModeDropdown.AddOptions(options);
            _teamModeDropdown.value = 0;
        }

        private void PopulateDifficultyOptions()
        {
            if (_difficultyDropdown == null)
                return;

            EnsureEditableOptions();
            _difficultyDropdown.ClearOptions();
            List<string> options = new();
            for (int i = 0; i < _availableDifficulties.Count; i++)
                options.Add(_availableDifficulties[i].ToString());
            _difficultyDropdown.AddOptions(options);
            _difficultyDropdown.value = Mathf.Clamp(_defaultDifficultyIndex, 0, Mathf.Max(0, options.Count - 1));
        }

        private void PopulateResourceOptions()
        {
            if (_resourcesDropdown == null)
                return;

            EnsureEditableOptions();
            _resourcesDropdown.ClearOptions();
            List<string> options = new();
            for (int i = 0; i < _startingResourcePresets.Count; i++)
                options.Add(Mathf.Max(0, _startingResourcePresets[i]).ToString());
            if (_allowCustomStartingResources)
                options.Add("Custom");
            _resourcesDropdown.AddOptions(options);
        }

        private void PopulateSlotOptions()
        {
            List<string> controllerOptions = new() { "Гравець", "Бот", "Відкрито" };
            List<string> teamOptions = new() { "Команда 1", "Команда 2", "Команда 3", "Команда 4" };
            List<string> difficultyOptions = new();

            EnsureEditableOptions();
            for (int i = 0; i < _availableDifficulties.Count; i++)
                difficultyOptions.Add(_availableDifficulties[i].ToString());

            for (int i = 0; i < _slotControllerDropdowns.Count; i++)
                SetDropdownOptions(_slotControllerDropdowns[i], controllerOptions, ResolveDefaultControllerIndex(i));

            for (int i = 0; i < _slotTeamDropdowns.Count; i++)
                SetDropdownOptions(_slotTeamDropdowns[i], teamOptions, ResolveDefaultAllianceIndex(i, SelectedTeamMode));

            for (int i = 0; i < _slotDifficultyDropdowns.Count; i++)
                SetDropdownOptions(_slotDifficultyDropdowns[i], difficultyOptions, _defaultDifficultyIndex);
        }

        private void RefreshMaps()
        {
            _visibleMaps.Clear();
            if (_mapCatalog != null && UsesMapDrivenMode)
                AddAllMaps(_visibleMaps);
            else if (_mapCatalog != null)
                _mapCatalog.GetMapsForMode(SelectedTeamMode, _visibleMaps);

            if (_hasManualMapSelection && !string.IsNullOrWhiteSpace(_selectedMapId))
            {
                int rememberedIndex = FindMapIndexById(_selectedMapId);
                _selectedMapIndex = rememberedIndex >= 0
                    ? rememberedIndex
                    : Mathf.Clamp(_selectedMapIndex, 0, Mathf.Max(0, _visibleMaps.Count - 1));
            }
            else
            {
                _selectedMapIndex = FindPreferredDefaultMapIndex();
                _selectedMapId = SelectedMap != null ? SelectedMap.MapId : null;
            }

            if (_mapDropdown != null)
            {
                _mapDropdown.ClearOptions();
                List<string> options = new();
                for (int i = 0; i < _visibleMaps.Count; i++)
                    options.Add(_visibleMaps[i].DisplayName);
                _mapDropdown.AddOptions(options);
                _mapDropdown.interactable = options.Count > 0;
                _mapDropdown.SetValueWithoutNotify(_selectedMapIndex);
            }

            RefreshMapButtons();
            RefreshSpawnOptions();
        }

        private void SelectMap(int index)
        {
            if (_visibleMaps.Count == 0)
            {
                _selectedMapIndex = 0;
                RefreshSpawnOptions();
                return;
            }

            _selectedMapIndex = Mathf.Clamp(index, 0, _visibleMaps.Count - 1);
            _selectedMapId = SelectedMap != null ? SelectedMap.MapId : null;
            _hasManualMapSelection = true;

            if (_mapDropdown != null)
                _mapDropdown.SetValueWithoutNotify(_selectedMapIndex);

            RefreshMapButtons();
            RefreshSpawnOptions();
        }

        private int FindPreferredDefaultMapIndex()
        {
            for (int i = 0; i < _visibleMaps.Count; i++)
            {
                if (_visibleMaps[i] != null && _visibleMaps[i].MapId == "main_scene")
                    return i;
            }

            return 0;
        }

        private int FindMapIndexById(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                return -1;

            for (int i = 0; i < _visibleMaps.Count; i++)
            {
                if (_visibleMaps[i] != null && _visibleMaps[i].MapId == mapId)
                    return i;
            }

            return -1;
        }

        private void RefreshMapButtons()
        {
            int count = Mathf.Max(_mapButtons.Count, _mapButtonLabels.Count);
            for (int i = 0; i < count; i++)
            {
                Button button = i < _mapButtons.Count ? _mapButtons[i] : null;
                TMP_Text label = i < _mapButtonLabels.Count ? _mapButtonLabels[i] : null;
                bool visible = i < _visibleMaps.Count;

                if (button != null)
                {
                    button.gameObject.SetActive(visible);
                    Image image = button.GetComponent<Image>();
                    if (image != null)
                    {
                        image.color = i == _selectedMapIndex
                            ? new Color(0.12f, 0.48f, 0.90f, 0.94f)
                            : new Color(0.06f, 0.18f, 0.31f, 0.86f);
                    }
                }

                if (label != null)
                {
                    label.gameObject.SetActive(visible);
                    if (visible)
                    {
                        MapDefinition map = _visibleMaps[i];
                        string marker = i == _selectedMapIndex ? "> " : string.Empty;
                        label.text = $"{marker}{map.DisplayName} - {FormatPlayerCount(map.MaxPlayers)}";
                    }
                }
            }
        }

        private void RefreshSpawnOptions()
        {
            MapDefinition map = SelectedMap;
            if (_localSpawnDropdown != null)
            {
                _localSpawnDropdown.ClearOptions();
                List<string> options = new();
                int count = map != null ? map.MaxPlayers : 0;
                for (int i = 0; i < count; i++)
                    options.Add("Spawn " + (i + 1));
                _localSpawnDropdown.AddOptions(options);
                _localSpawnDropdown.interactable = options.Count > 0;
            }

            RefreshMapPreview();
            RefreshSlotRows();
        }

        private void RefreshMapPreview()
        {
            MapDefinition map = SelectedMap;
            if (_mapPreviewTitleText != null)
                _mapPreviewTitleText.text = map != null ? map.DisplayName : "Карта не вибрана";

            if (_mapPreviewDetailsText != null)
            {
                _mapPreviewDetailsText.text = map != null
                    ? $"Гравців: {map.MaxPlayers}\nРежими: {FormatSupportedModes(map)}"
                    : "Додай MapDefinition у MapCatalog.";
            }

            if (_mapPreviewImage != null)
            {
                Sprite preview = map != null && map.Preview != null ? map.Preview : _fallbackMapPreview;
                _mapPreviewImage.sprite = preview;
                _mapPreviewImage.enabled = preview != null;
            }
        }

        private void RefreshSlotRows()
        {
            MapDefinition map = SelectedMap;
            int visibleSlots = Mathf.Clamp(ResolveVisibleSlotCount(map), 0, MaxSlotRowCount);

            for (int i = 0; i < MaxSlotRowCount; i++)
            {
                bool visible = i < visibleSlots;

                SetSlotRowActive(i, visible);
                SetActive(i < _slotSpawnLabels.Count ? _slotSpawnLabels[i]?.gameObject : null, visible);
                SetActive(i < _slotControllerDropdowns.Count ? _slotControllerDropdowns[i]?.gameObject : null, visible);

                if (i < _slotSpawnLabels.Count && _slotSpawnLabels[i] != null)
                    _slotSpawnLabels[i].text = "Spawn " + (i + 1);

                TMP_Dropdown controller = i < _slotControllerDropdowns.Count ? _slotControllerDropdowns[i] : null;
                TMP_Dropdown team = i < _slotTeamDropdowns.Count ? _slotTeamDropdowns[i] : null;
                TMP_Dropdown difficulty = i < _slotDifficultyDropdowns.Count ? _slotDifficultyDropdowns[i] : null;

                if (controller != null)
                {
                    int defaultController = visible ? ResolveDefaultControllerIndex(i) : 2;
                    if (!visible)
                        controller.SetValueWithoutNotify(defaultController);
                    controller.interactable = visible && i != 0;
                }

                bool isOpen = visible && IsSlotOpen(i);
                bool isAi = visible && !isOpen && ResolveControllerKind(i, _activeSetupMode) == TeamControllerKind.AI;
                bool isPlayableSlot = visible && !isOpen;

                SetActive(team != null ? team.gameObject : null, isPlayableSlot);
                SetActive(difficulty != null ? difficulty.gameObject : null, isAi);

                if (team != null)
                {
                    if (team.options.Count > 0 && team.value < 0)
                        team.SetValueWithoutNotify(ResolveDefaultAllianceIndex(i, SelectedTeamMode));
                    team.interactable = isPlayableSlot;
                }

                if (difficulty != null)
                    difficulty.interactable = isAi;
            }

            RefreshSlotSummary();
        }

        private void RefreshSlotSummary()
        {
            if (_slotSummaryText == null)
                return;

            MatchLaunchConfig config = BuildConfig(_activeSetupMode);
            if (config == null)
            {
                _slotSummaryText.text = "No valid map selected.";
                return;
            }

            List<string> lines = new();
            for (int i = 0; i < config.Teams.Count; i++)
            {
                TeamLaunchSlot slot = config.Teams[i];
                string controller = slot.Controller == TeamControllerKind.AI
                    ? "Bot " + slot.AiDifficulty
                    : slot.Controller == TeamControllerKind.RemoteHuman ? "Remote Player" : "Player";
                lines.Add($"Spawn {slot.SpawnSlotIndex + 1}: {slot.PlayerName} ({controller})");
            }

            _slotSummaryText.text = string.Join("\n", lines);
        }

        private void StartOfflineMatch()
        {
            MatchLaunchConfig config = BuildConfig(MatchLaunchMode.OfflineBots);
            if (!ValidateConfig(config))
                return;

            MatchLaunchContext.SetConfig(config);
            LoadMap(config.Map);
        }

        private async void HostOnlineMatch()
        {
            if (_pendingOnlineHostConfig != null && !string.IsNullOrWhiteSpace(_createdLobbyCode))
            {
                MatchLaunchContext.SetConfig(_pendingOnlineHostConfig);
                LoadMap(_pendingOnlineHostConfig.Map);
                return;
            }

            MatchLaunchConfig config = BuildConfig(MatchLaunchMode.OnlineHost);
            if (!ValidateConfig(config))
                return;

            SetStatus("Creating online lobby...");
            NetworkSessionResult result = await NetworkSessionService.HostLobbyAsync(config);
            if (!result.Success)
            {
                SetStatus(result.Message);
                return;
            }

            _pendingOnlineHostConfig = config;
            _createdLobbyCode = result.JoinCode;
            GUIUtility.systemCopyBuffer = result.JoinCode;
            SetStatus("Код lobby: " + result.JoinCode + " (скопійовано). Натисни запуск, коли другий гравець підключиться.");
            SetButtonLabel(_hostOnlineButton, "ЗАПУСТИТИ МАТЧ");
        }

        private async void JoinOnlineMatch()
        {
            string joinCode = _joinCodeInput != null ? _joinCodeInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                SetStatus("Enter join code.");
                return;
            }

            SetStatus("Joining lobby...");
            NetworkSessionResult result = await NetworkSessionService.JoinLobbyAsync(joinCode);
            SetStatus(result.Success ? "Connected. Waiting for host..." : result.Message);
        }

        private void LoadSelectedSave()
        {
            if (_saveDropdown == null || _saveDropdown.value < 0 || _saveDropdown.value >= _saveFiles.Count)
            {
                SetStatus("No save selected.");
                return;
            }

            string path = _saveFiles[_saveDropdown.value];
            if (!SaveGameFileIO.TryRead(path, out SaveGameSnapshot snapshot))
            {
                SetStatus("Failed to read save.");
                return;
            }

            MapDefinition map = _mapCatalog != null ? _mapCatalog.FindById(snapshot.mapId) : null;
            if (map == null)
            {
                SetStatus("Save map is missing in catalog.");
                return;
            }

            MatchLaunchContext.SetPendingSaveLoad(path, snapshot.ToLaunchConfig(map));
            LoadMap(map);
        }

        private void RefreshSaves()
        {
            SaveGameFileIO.GetSaveFiles(_saveFiles);

            if (_saveDropdown == null)
                return;

            _saveDropdown.ClearOptions();
            List<string> options = new();
            for (int i = 0; i < _saveFiles.Count; i++)
                options.Add(SaveGameFileIO.GetDisplayName(_saveFiles[i], _mapCatalog, i));

            if (options.Count == 0)
                options.Add("Немає збережень");

            _saveDropdown.AddOptions(options);
            _saveDropdown.interactable = _saveFiles.Count > 0;

            if (_loadSaveButton != null)
                _loadSaveButton.interactable = _saveFiles.Count > 0;
        }

        private void PopulateResolutions()
        {
            if (_resolutionDropdown == null)
                return;

            _resolutions.Clear();
            _resolutions.AddRange(Screen.resolutions);
            _resolutionDropdown.ClearOptions();

            List<string> options = new();
            int selectedIndex = 0;
            int savedWidth = PlayerPrefs.GetInt("Settings.ResolutionWidth", Screen.width);
            int savedHeight = PlayerPrefs.GetInt("Settings.ResolutionHeight", Screen.height);

            for (int i = 0; i < _resolutions.Count; i++)
            {
                Resolution resolution = _resolutions[i];
                options.Add($"{resolution.width} x {resolution.height}");
                if (resolution.width == savedWidth && resolution.height == savedHeight)
                    selectedIndex = i;
            }

            if (options.Count == 0)
            {
                _resolutions.Add(new Resolution { width = Screen.width, height = Screen.height });
                options.Add($"{Screen.width} x {Screen.height}");
            }

            _resolutionDropdown.AddOptions(options);
            _resolutionDropdown.value = selectedIndex;
        }

        private void ApplySelectedResolution()
        {
            if (_resolutionDropdown == null || _resolutionDropdown.value < 0 || _resolutionDropdown.value >= _resolutions.Count)
                return;

            Resolution resolution = _resolutions[_resolutionDropdown.value];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
            PlayerPrefs.SetInt("Settings.ResolutionWidth", resolution.width);
            PlayerPrefs.SetInt("Settings.ResolutionHeight", resolution.height);
            PlayerPrefs.Save();
            SetStatus("Resolution applied.");
        }

        private MatchLaunchConfig BuildConfig(MatchLaunchMode mode)
        {
            MapDefinition map = SelectedMap;
            if (map == null)
                return null;

            int resources = SelectedResources;
            int slotCount = ResolveVisibleSlotCount(map);
            List<TeamLaunchSlot> teams = new();
            int botNumber = 1;

            for (int i = 0; i < slotCount; i++)
            {
                if (i != 0 && IsSlotOpen(i))
                    continue;

                TeamControllerKind controller = i == 0 ? TeamControllerKind.LocalHuman : ResolveControllerKind(i, mode);
                AiDifficultyLevel difficulty = ResolveSlotDifficulty(i);
                string playerName = controller == TeamControllerKind.AI
                    ? "Bot " + botNumber++
                    : i == 0 ? "Player" : "Player " + (i + 1);

                teams.Add(new TeamLaunchSlot(
                    ResolveTeamType(i),
                    ResolveAllianceId(i, SelectedTeamMode),
                    controller,
                    i,
                    i,
                    playerName,
                    difficulty,
                    resources));
            }

            SkirmishTeamMode teamMode = ResolveTeamModeForConfig(map, teams.Count);
            return new MatchLaunchConfig(mode, teamMode, map, TeamType.Player, 0, teams);
        }

        private bool ValidateConfig(MatchLaunchConfig config)
        {
            if (config == null || config.Map == null)
            {
                SetStatus("Select a valid map.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.Map.ScenePath))
            {
                SetStatus("Selected map has no scene path.");
                return false;
            }

            if (config.Teams.Count < 2)
            {
                SetStatus("Додай хоча б одного бота або гравця.");
                return false;
            }

            return true;
        }

        private void LoadMap(MapDefinition map)
        {
            SetStatus("Loading " + map.DisplayName + "...");
            SceneManager.LoadSceneAsync(map.ScenePath, LoadSceneMode.Single);
        }

        private MapDefinition SelectedMap =>
            _selectedMapIndex >= 0 && _selectedMapIndex < _visibleMaps.Count
                ? _visibleMaps[_selectedMapIndex]
                : _visibleMaps.Count > 0 ? _visibleMaps[0] : null;

        private SkirmishTeamMode SelectedTeamMode =>
            UsesMapDrivenMode
                ? ResolveModeForSelectedMap()
                : _availableTeamModes.Count > 0
                    ? _availableTeamModes[Mathf.Clamp(_teamModeDropdown != null ? _teamModeDropdown.value : 0, 0, _availableTeamModes.Count - 1)]
                    : SkirmishTeamMode.OneVsOne;

        private bool UsesMapDrivenMode => _teamModeDropdown == null || !_teamModeDropdown.gameObject.activeInHierarchy;

        private void AddAllMaps(List<MapDefinition> results)
        {
            results.Clear();

            if (_mapCatalog?.Maps == null)
                return;

            for (int i = 0; i < _mapCatalog.Maps.Count; i++)
            {
                MapDefinition map = _mapCatalog.Maps[i];
                if (map != null)
                    results.Add(map);
            }

            results.Sort((first, second) =>
            {
                int playerCompare = first.MaxPlayers.CompareTo(second.MaxPlayers);
                return playerCompare != 0
                    ? playerCompare
                    : string.Compare(first.DisplayName, second.DisplayName, System.StringComparison.Ordinal);
            });
        }

        private SkirmishTeamMode ResolveModeForSelectedMap()
        {
            MapDefinition map = SelectedMap;

            if (map?.SupportedModes == null || map.SupportedModes.Count == 0)
                return SkirmishTeamMode.OneVsOne;

            if (map.SupportsMode(SkirmishTeamMode.OneVsOne))
                return SkirmishTeamMode.OneVsOne;

            return map.SupportedModes[0];
        }

        private AiDifficultyLevel SelectedDifficulty =>
            _availableDifficulties.Count > 0
                ? _availableDifficulties[Mathf.Clamp(_difficultyDropdown != null ? _difficultyDropdown.value : _defaultDifficultyIndex, 0, _availableDifficulties.Count - 1)]
                : AiDifficultyLevel.Medium;

        private int SelectedResources
        {
            get
            {
                if (_resourcesDropdown == null)
                    return SelectedMap != null ? SelectedMap.DefaultStartingResources : 500;

                int index = _resourcesDropdown.value;
                if (_startingResourcePresets != null && index >= 0 && index < _startingResourcePresets.Count)
                    return Mathf.Max(0, _startingResourcePresets[index]);

                if (_allowCustomStartingResources)
                {
                    return _customResourcesInput != null && int.TryParse(_customResourcesInput.text, out int custom)
                        ? Mathf.Max(0, custom)
                        : 500;
                }

                return SelectedMap != null ? SelectedMap.DefaultStartingResources : 500;
            }
        }

        private void EnsureEditableOptions()
        {
            if (_availableTeamModes == null)
                _availableTeamModes = new List<SkirmishTeamMode>();
            if (_availableTeamModes.Count == 0)
                _availableTeamModes.Add(SkirmishTeamMode.OneVsOne);

            if (_availableDifficulties == null)
                _availableDifficulties = new List<AiDifficultyLevel>();
            if (_availableDifficulties.Count == 0)
                _availableDifficulties.Add(AiDifficultyLevel.Medium);

            if (_startingResourcePresets == null)
                _startingResourcePresets = new List<int>();
            if (_startingResourcePresets.Count == 0)
                _startingResourcePresets.Add(500);
        }

        private static string FormatMode(SkirmishTeamMode mode)
        {
            return mode switch
            {
                SkirmishTeamMode.TwoVsTwo => "2 vs 2",
                SkirmishTeamMode.ThreePlayer => "3 гравці",
                _ => "1 vs 1"
            };
        }

        private static string FormatSupportedModes(MapDefinition map)
        {
            if (map == null || map.SupportedModes == null || map.SupportedModes.Count == 0)
                return "немає";

            List<string> modes = new();
            for (int i = 0; i < map.SupportedModes.Count; i++)
                modes.Add(FormatMode(map.SupportedModes[i]));
            return string.Join(", ", modes);
        }

        private static string FormatPlayerCount(int count)
        {
            return count == 1 ? "1 гравець" : count + " гравці";
        }

        private int MaxSlotRowCount => Mathf.Max(
            _slotSpawnLabels?.Count ?? 0,
            _slotControllerDropdowns?.Count ?? 0,
            _slotTeamDropdowns?.Count ?? 0,
            _slotDifficultyDropdowns?.Count ?? 0,
            4);

        private static int ResolveActiveSlotCount(MapDefinition map, SkirmishTeamMode mode)
        {
            int requested = Mathf.Max(2, (int)mode);
            return map != null ? Mathf.Clamp(requested, 2, map.MaxPlayers) : requested;
        }

        private static int ResolveVisibleSlotCount(MapDefinition map)
        {
            return map != null ? Mathf.Clamp(map.MaxPlayers, 2, 8) : 2;
        }

        private static TeamType ResolveTeamType(int slotIndex)
        {
            return slotIndex switch
            {
                0 => TeamType.Player,
                1 => TeamType.Enemy,
                2 => TeamType.Team3,
                3 => TeamType.Team4,
                4 => TeamType.Team5,
                5 => TeamType.Team6,
                6 => TeamType.Team7,
                _ => TeamType.Team8
            };
        }

        private int ResolveAllianceId(int slotIndex, SkirmishTeamMode mode)
        {
            TMP_Dropdown dropdown = slotIndex >= 0 && slotIndex < _slotTeamDropdowns.Count
                ? _slotTeamDropdowns[slotIndex]
                : null;

            return dropdown != null && dropdown.options.Count > 0
                ? Mathf.Clamp(dropdown.value + 1, 1, dropdown.options.Count)
                : ResolveDefaultAllianceIndex(slotIndex, mode) + 1;
        }

        private TeamControllerKind ResolveControllerKind(int slotIndex, MatchLaunchMode mode)
        {
            if (slotIndex == 0)
                return TeamControllerKind.LocalHuman;

            TMP_Dropdown dropdown = slotIndex >= 0 && slotIndex < _slotControllerDropdowns.Count
                ? _slotControllerDropdowns[slotIndex]
                : null;

            if (dropdown != null && dropdown.value == 0)
                return mode == MatchLaunchMode.OnlineHost ? TeamControllerKind.RemoteHuman : TeamControllerKind.LocalHuman;

            return TeamControllerKind.AI;
        }

        private AiDifficultyLevel ResolveSlotDifficulty(int slotIndex)
        {
            TMP_Dropdown dropdown = slotIndex >= 0 && slotIndex < _slotDifficultyDropdowns.Count
                ? _slotDifficultyDropdowns[slotIndex]
                : null;

            int index = dropdown != null ? dropdown.value : _defaultDifficultyIndex;
            return _availableDifficulties.Count > 0
                ? _availableDifficulties[Mathf.Clamp(index, 0, _availableDifficulties.Count - 1)]
                : AiDifficultyLevel.Medium;
        }

        private static int ResolveDefaultAllianceIndex(int slotIndex, SkirmishTeamMode mode)
        {
            if (mode == SkirmishTeamMode.TwoVsTwo)
                return slotIndex == 0 || slotIndex == 2 ? 0 : 1;

            return Mathf.Clamp(slotIndex, 0, 3);
        }

        private static int ResolveDefaultControllerIndex(int slotIndex)
        {
            if (slotIndex == 0)
                return 0;

            return slotIndex == 1 ? 1 : 2;
        }

        private bool IsSlotOpen(int slotIndex)
        {
            if (slotIndex <= 0)
                return false;

            TMP_Dropdown dropdown = slotIndex >= 0 && slotIndex < _slotControllerDropdowns.Count
                ? _slotControllerDropdowns[slotIndex]
                : null;

            return dropdown != null && dropdown.value == 2;
        }

        private void SetSlotRowActive(int slotIndex, bool active)
        {
            Transform row = null;
            if (slotIndex >= 0 && slotIndex < _slotSpawnLabels.Count && _slotSpawnLabels[slotIndex] != null)
                row = _slotSpawnLabels[slotIndex].transform.parent;
            else if (slotIndex >= 0 && slotIndex < _slotControllerDropdowns.Count && _slotControllerDropdowns[slotIndex] != null)
                row = _slotControllerDropdowns[slotIndex].transform.parent;

            if (row != null && row.gameObject.activeSelf != active)
                row.gameObject.SetActive(active);
        }

        private static SkirmishTeamMode ResolveTeamModeForConfig(MapDefinition map, int activeTeamCount)
        {
            if (activeTeamCount >= 4 && map != null && map.SupportsMode(SkirmishTeamMode.TwoVsTwo))
                return SkirmishTeamMode.TwoVsTwo;

            if (activeTeamCount >= 3 && map != null && map.SupportsMode(SkirmishTeamMode.ThreePlayer))
                return SkirmishTeamMode.ThreePlayer;

            return SkirmishTeamMode.OneVsOne;
        }

        private void ResetSlotTeamsForMode()
        {
            for (int i = 0; i < _slotTeamDropdowns.Count; i++)
            {
                TMP_Dropdown dropdown = _slotTeamDropdowns[i];
                if (dropdown != null && dropdown.options.Count > 0)
                    dropdown.SetValueWithoutNotify(ResolveDefaultAllianceIndex(i, SelectedTeamMode));
            }
        }

        private static void SetDropdownOptions(TMP_Dropdown dropdown, List<string> options, int value)
        {
            if (dropdown == null)
                return;

            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1)));
        }

        private void ShowPanel(GameObject panel)
        {
            if (panel != _skirmishPanel || _activeSetupMode != MatchLaunchMode.OnlineHost)
                ClearPendingOnlineLobby();

            SetActive(_mainPanel, panel == _mainPanel);
            SetActive(_skirmishModePanel, panel == _skirmishModePanel);
            SetActive(_skirmishPanel, panel == _skirmishPanel);
            SetActive(_onlinePanel, panel == _onlinePanel);
            SetActive(_loadPanel, panel == _loadPanel);
            SetActive(_settingsPanel, panel == _settingsPanel);
            SetActive(_startOfflineButton != null ? _startOfflineButton.gameObject : null,
                panel == _skirmishPanel && _activeSetupMode == MatchLaunchMode.OfflineBots);
            SetActive(_hostOnlineButton != null ? _hostOnlineButton.gameObject : null,
                panel == _skirmishPanel && _activeSetupMode == MatchLaunchMode.OnlineHost);
            SetStatus(string.Empty);
        }

        private void ClearPendingOnlineLobby()
        {
            _pendingOnlineHostConfig = null;
            _createdLobbyCode = string.Empty;
            SetButtonLabel(_hostOnlineButton, "СТВОРИТИ LOBBY");
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private static void Add(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void Remove(Button button)
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }

        private static void AddDropdownListener(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction action)
        {
            if (dropdown != null)
                dropdown.onValueChanged.AddListener(_ => action());
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
