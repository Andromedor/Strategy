using Strategy.Buildings;
using Strategy.Core;
using Strategy.Units;
using TMPro;
using UnityEngine;

namespace Strategy.UI
{
    /// <summary>
    /// Оновлює лише числові значення верхньої панелі ресурсів.
    /// Статичні підписи та сама структура HUD мають бути створені в Unity UI до запуску гри.
    /// </summary>
    public class OutpostStatusUI : MonoBehaviour
    {
        [Header("Value Text References")]
        [SerializeField] private TMP_Text _zonesValueText;
        [SerializeField] private TMP_Text _moneyValueText;
        [SerializeField] private TMP_Text _incomeValueText;

        private void OnEnable()
        {
            ResourceManager.OnResourceChanged += OnResourceChanged;
            Outpost.OnStatsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            ResourceManager.OnResourceChanged -= OnResourceChanged;
            Outpost.OnStatsChanged -= Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void OnResourceChanged(int resource)
        {
            Refresh();
        }

        private void Refresh()
        {
            int playerResource = ResourceManager.Instance != null ? ResourceManager.Instance.Resource : 0;
            int capturedOutposts = Outpost.GetOwnedCount(TeamType.Player);
            int resourcePerMinute = Mathf.RoundToInt(Outpost.GetResourcePerMinute(TeamType.Player));

            SetText(_zonesValueText, capturedOutposts.ToString());
            SetText(_moneyValueText, playerResource.ToString());
            SetText(_incomeValueText, "+" + resourcePerMinute);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
