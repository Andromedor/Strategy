using System.Collections.Generic;
using UnityEngine;

using Strategy.Core;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using Strategy.UI;
namespace Strategy.Buildings
{
    /// <summary>
    /// Визначає кругову зону будівництва, де гравець може розміщувати будівлі.
    /// Реєструє себе у статичному списку, щоб BuildingPlacementManager міг опитувати всі активні центри.
    /// </summary>
    public class ConstructionCenter: MonoBehaviour
    {
        public static readonly List<ConstructionCenter> All = new();

        [Header("Build Area")]
        [SerializeField] private float _buildRadius = 25f;
        // Радіус, у межах якого дозволено будівництво.

        [SerializeField] private GameObject _buildAreaVisual;
        // Візуальний круг/диск зони будівництва.

        public Vector3 Position => transform.position;
        public float BuildRadius => _buildRadius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            All.Clear();
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);

            HideBuildArea();
            EventManager.RaiseConstructionCentersChanged();
        }

        private void OnDisable()
        {
            All.Remove(this);
            EventManager.RaiseConstructionCentersChanged();
        }

        private void Awake()
        {
            UpdateBuildAreaVisualSize();
            HideBuildArea();
        }

        /// <summary>Повертає true, якщо вказана світова позиція знаходиться в межах радіуса будівництва центру.</summary>
        public bool IsInsideBuildArea(Vector3 position)
        {
            float distanceSqr = (position - transform.position).sqrMagnitude;
            return distanceSqr <= _buildRadius * _buildRadius;
        }

        /// <summary>Масштабує візуальний диск зони будівництва відповідно до _buildRadius, щоб відображений оверлей збігався з реальною зоною.</summary>
        private void UpdateBuildAreaVisualSize()
        {
            if (_buildAreaVisual == null)
                return;

            float scale = (_buildRadius * 2f) / 10f;
            _buildAreaVisual.transform.localScale = new Vector3(scale, 1f, scale);
        }

        public void ShowBuildArea()
        {
            SetBuildAreaVisualVisible(true);
        }

        public void HideBuildArea()
        {
            SetBuildAreaVisualVisible(false);
        }

        /// <summary>Перемикає видимість візуалу зони будівництва, активуючи/деактивуючи GameObject або перемикаючи його Renderer, якщо він є коренем.</summary>
        private void SetBuildAreaVisualVisible(bool visible)
        {
            if (_buildAreaVisual == null)
                return;

            if (_buildAreaVisual == gameObject)
            {
                Renderer visualRenderer = _buildAreaVisual.GetComponent<Renderer>();

                if (visualRenderer != null)
                    visualRenderer.enabled = visible;

                return;
            }

            _buildAreaVisual.SetActive(visible);
        }
    }
}
