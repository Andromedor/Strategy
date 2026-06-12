using System.Collections;
using Strategy.Save;
using TMPro;
using UnityEngine;

namespace Strategy.UI
{
    public sealed class SaveGameToastUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField, Min(0.2f)] private float _visibleSeconds = 2f;

        private Coroutine _hideCoroutine;

        private void Awake()
        {
            if (_rootGroup == null)
            {
                GameObject target = _root != null ? _root : gameObject;
                _rootGroup = target.GetComponent<CanvasGroup>();
            }

            SetVisible(false);
        }

        private void OnEnable()
        {
            SaveGameManager.SaveStatusMessage += Show;
        }

        private void OnDisable()
        {
            SaveGameManager.SaveStatusMessage -= Show;
        }

        public void Show(string message)
        {
            if (_messageText != null)
                _messageText.text = message;

            SetVisible(true);

            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);

            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_visibleSeconds);
            SetVisible(false);
            _hideCoroutine = null;
        }

        private void SetVisible(bool visible)
        {
            if (_rootGroup != null)
            {
                _rootGroup.alpha = visible ? 1f : 0f;
                _rootGroup.interactable = visible;
                _rootGroup.blocksRaycasts = visible;
                return;
            }

            GameObject target = _root != null ? _root : gameObject;
            if (target != gameObject)
                target.SetActive(visible);
        }
    }
}
