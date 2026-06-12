using Strategy.Core;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Strategy.UI
{
    public class MatchResultPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _subtitleText;
        [SerializeField] private string _victoryTitle = "Перемога";
        [SerializeField] private string _defeatTitle = "Поразка";
        [SerializeField] private bool _returnToMainMenuAfterResult = true;
        [SerializeField, Min(0f)] private float _returnDelaySeconds = 4f;
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        private CanvasGroup _canvasGroup;
        private Coroutine _returnRoutine;

        private void Awake()
        {
            CacheCanvasGroup();
            SetVisible(false);
        }

        private void OnEnable()
        {
            MatchVictorySystem.MatchEnded += OnMatchEnded;
        }

        private void OnDisable()
        {
            MatchVictorySystem.MatchEnded -= OnMatchEnded;
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (_returnRoutine != null)
                StopCoroutine(_returnRoutine);

            if (_titleText != null)
                _titleText.text = result.IsVictory ? _victoryTitle : _defeatTitle;

            if (_subtitleText != null)
            {
                _subtitleText.text = result.IsVictory
                    ? "Усі ворожі будівлі знищено."
                    : "Усі союзні будівлі знищено.";
            }

            SetVisible(true);

            if (_returnToMainMenuAfterResult)
                _returnRoutine = StartCoroutine(ReturnToMainMenuAfterDelay());
        }

        private void SetVisible(bool visible)
        {
            GameObject target = _root != null ? _root : gameObject;
            if (target == gameObject)
            {
                CacheCanvasGroup();
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
                return;
            }

            target.SetActive(visible);
        }

        private IEnumerator ReturnToMainMenuAfterDelay()
        {
            if (_returnDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(_returnDelaySeconds);

            MatchLaunchContext.Clear();
            SceneManager.LoadSceneAsync(_mainMenuSceneName, LoadSceneMode.Single);
        }

        private void CacheCanvasGroup()
        {
            if (_canvasGroup != null)
                return;

            GameObject target = _root != null ? _root : gameObject;
            _canvasGroup = target.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = target.AddComponent<CanvasGroup>();
        }
    }
}
