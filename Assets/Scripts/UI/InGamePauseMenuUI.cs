using System.Collections.Generic;
using Strategy.Core;
using Strategy.Maps;
using Strategy.Save;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Strategy.UI
{
    /// <summary>
    /// Керує внутрішньоігровим ESC-меню: пауза, quick save, список завантажень,
    /// повернення в головне меню та вихід із гри. Візуальна ієрархія задається в сцені/префабі.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGamePauseMenuUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private RectTransform _windowRect;
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _loadPanel;

        [Header("Main Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _openLoadButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _quitButton;

        [Header("Load")]
        [SerializeField] private Transform _saveListRoot;
        [SerializeField] private Button _saveRowTemplate;
        [SerializeField] private Button _loadSelectedButton;
        [SerializeField] private Button _backFromLoadButton;

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;

        [Header("Services")]
        [SerializeField] private SaveGameManager _saveGameManager;
        [SerializeField] private MapCatalog _mapCatalog;

        [Header("Scene")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";
        [SerializeField] private bool _pauseTimeScale = true;

        private readonly List<string> _saveFiles = new();
        private readonly List<Button> _saveRows = new();
        private int _selectedSaveIndex = -1;
        private bool _isOpen;
        private float _previousTimeScale = 1f;
        private Coroutine _statusCoroutine;

        private void Awake()
        {
            CacheReferences();
            ResolveDependencies();
            SetVisible(false, restoreTimeScale: false);
        }

        private void OnEnable()
        {
            Add(_resumeButton, Close);
            Add(_saveButton, Save);
            Add(_openLoadButton, OpenLoadPanel);
            Add(_loadSelectedButton, LoadSelectedSave);
            Add(_backFromLoadButton, OpenMainPanel);
            Add(_mainMenuButton, ReturnToMainMenu);
            Add(_quitButton, QuitGame);
            SaveGameManager.SaveStatusMessage += SetStatus;
        }

        private void OnDisable()
        {
            Remove(_resumeButton, Close);
            Remove(_saveButton, Save);
            Remove(_openLoadButton, OpenLoadPanel);
            Remove(_loadSelectedButton, LoadSelectedSave);
            Remove(_backFromLoadButton, OpenMainPanel);
            Remove(_mainMenuButton, ReturnToMainMenu);
            Remove(_quitButton, QuitGame);
            SaveGameManager.SaveStatusMessage -= SetStatus;

            if (_statusCoroutine != null)
                StopCoroutine(_statusCoroutine);

            if (_isOpen)
                ResumeTimeScale();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            if (_isOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            _previousTimeScale = Time.timeScale;
            if (_pauseTimeScale)
                Time.timeScale = 0f;

            SetVisible(true, restoreTimeScale: false);
            OpenMainPanel();
            RefreshSaveList();
        }

        public void Close()
        {
            SetVisible(false, restoreTimeScale: true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isOpen || eventData == null || _windowRect == null)
                return;

            if (!RectTransformUtility.RectangleContainsScreenPoint(_windowRect, eventData.position, eventData.pressEventCamera))
                Close();
        }

        private void Save()
        {
            if (_saveGameManager == null)
            {
                SetStatus("SaveGameManager не підключений");
                return;
            }

            _saveGameManager.SaveQuick();
            RefreshSaveList();
        }

        private void OpenMainPanel()
        {
            if (_mainPanel != null)
                _mainPanel.SetActive(true);

            if (_loadPanel != null)
                _loadPanel.SetActive(false);

            SetStatus(string.Empty);
        }

        private void OpenLoadPanel()
        {
            if (_mainPanel != null)
                _mainPanel.SetActive(false);

            if (_loadPanel != null)
                _loadPanel.SetActive(true);

            RefreshSaveList();
        }

        private void RefreshSaveList()
        {
            ResolveDependencies();
            ClearSaveRows();
            SaveGameFileIO.GetSaveFiles(_saveFiles);
            _selectedSaveIndex = _saveFiles.Count > 0 ? 0 : -1;

            if (_saveRowTemplate != null)
                _saveRowTemplate.gameObject.SetActive(false);

            if (_saveListRoot == null || _saveRowTemplate == null)
            {
                SetStatus("Save list UI не підключений");
                return;
            }

            for (int i = 0; i < _saveFiles.Count; i++)
                CreateSaveRow(i);

            UpdateLoadButtonState();
            UpdateSaveRowSelection();

            if (_saveFiles.Count == 0)
                SetStatus("Немає збережень");
            else if (_statusText != null && _statusText.text == "Немає збережень")
                SetStatus(string.Empty);
        }

        private void CreateSaveRow(int index)
        {
            Button row = Instantiate(_saveRowTemplate, _saveListRoot);
            row.gameObject.SetActive(true);

            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = SaveGameFileIO.GetDisplayName(_saveFiles[index], _mapCatalog, index);

            int capturedIndex = index;
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => SelectSave(capturedIndex));
            _saveRows.Add(row);
        }

        private void SelectSave(int index)
        {
            _selectedSaveIndex = Mathf.Clamp(index, 0, _saveFiles.Count - 1);
            UpdateLoadButtonState();
            UpdateSaveRowSelection();
        }

        private void LoadSelectedSave()
        {
            ResolveDependencies();

            if (_selectedSaveIndex < 0 || _selectedSaveIndex >= _saveFiles.Count)
            {
                SetStatus("Обери збереження");
                return;
            }

            string path = _saveFiles[_selectedSaveIndex];
            if (!SaveGameFileIO.TryRead(path, out SaveGameSnapshot snapshot))
            {
                SetStatus("Не вдалося прочитати збереження");
                return;
            }

            MapDefinition map = _mapCatalog != null ? _mapCatalog.FindById(snapshot.mapId) : null;
            if (map == null)
            {
                SetStatus("Карта збереження відсутня в MapCatalog");
                return;
            }

            ResumeTimeScale();
            MatchLaunchContext.SetPendingSaveLoad(path, snapshot.ToLaunchConfig(map));
            SceneManager.LoadSceneAsync(map.ScenePath, LoadSceneMode.Single);
        }

        private void ReturnToMainMenu()
        {
            ResumeTimeScale();
            MatchLaunchContext.Clear();
            SceneManager.LoadSceneAsync(_mainMenuSceneName, LoadSceneMode.Single);
        }

        private void QuitGame()
        {
            ResumeTimeScale();

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetVisible(bool visible, bool restoreTimeScale)
        {
            _isOpen = visible;

            if (_rootGroup != null)
            {
                if (!_rootGroup.gameObject.activeSelf)
                    _rootGroup.gameObject.SetActive(true);

                _rootGroup.alpha = visible ? 1f : 0f;
                _rootGroup.interactable = visible;
                _rootGroup.blocksRaycasts = visible;
            }
            else
            {
                enabled = true;
            }

            if (!visible)
            {
                if (_mainPanel != null)
                    _mainPanel.SetActive(false);

                if (_loadPanel != null)
                    _loadPanel.SetActive(false);
            }

            if (restoreTimeScale)
                ResumeTimeScale();
        }

        private void ResumeTimeScale()
        {
            if (!_pauseTimeScale)
                return;

            Time.timeScale = Mathf.Approximately(_previousTimeScale, 0f) ? 1f : _previousTimeScale;
        }

        private void UpdateLoadButtonState()
        {
            if (_loadSelectedButton != null)
                _loadSelectedButton.interactable = _selectedSaveIndex >= 0 && _selectedSaveIndex < _saveFiles.Count;
        }

        private void UpdateSaveRowSelection()
        {
            for (int i = 0; i < _saveRows.Count; i++)
            {
                Button row = _saveRows[i];
                if (row == null || row.targetGraphic == null)
                    continue;

                row.targetGraphic.color = i == _selectedSaveIndex
                    ? new Color(0.16f, 0.46f, 0.78f, 0.95f)
                    : new Color(0.06f, 0.18f, 0.28f, 0.88f);
            }
        }

        private void ClearSaveRows()
        {
            for (int i = 0; i < _saveRows.Count; i++)
            {
                if (_saveRows[i] != null)
                    Destroy(_saveRows[i].gameObject);
            }

            _saveRows.Clear();
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;

            if (_statusCoroutine != null)
            {
                StopCoroutine(_statusCoroutine);
                _statusCoroutine = null;
            }

            if (IsTransientStatus(message))
                _statusCoroutine = StartCoroutine(ClearStatusAfterDelay());
        }

        private static bool IsTransientStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.IndexOf("збережено", System.StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("завантажено", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private System.Collections.IEnumerator ClearStatusAfterDelay()
        {
            yield return new WaitForSecondsRealtime(2f);

            if (_statusText != null)
                _statusText.text = string.Empty;

            _statusCoroutine = null;
        }

        private void CacheReferences()
        {
            if (_rootGroup == null)
                _rootGroup = GetComponent<CanvasGroup>();

            if (_saveGameManager == null)
                _saveGameManager = FindFirstObjectByType<SaveGameManager>();
        }

        private void ResolveDependencies()
        {
            if (_mapCatalog == null)
                _mapCatalog = Resources.Load<MapCatalog>("MapCatalog");
        }

        private static void Add(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void Remove(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }
    }
}
