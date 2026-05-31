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
    /// Defines a circular build area where the player may place buildings.
    /// Registers itself in a static list so BuildingPlacementManager can query all active centers.
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

        /// <summary>Returns true if the given world position is within the construction center's build radius.</summary>
        public bool IsInsideBuildArea(Vector3 position)
        {
            float distanceSqr = (position - transform.position).sqrMagnitude;
            return distanceSqr <= _buildRadius * _buildRadius;
        }
        
        /// <summary>Scales the build-area visual disc to match _buildRadius so the rendered overlay matches the actual zone.</summary>
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

        /// <summary>Toggles the build-area visual by activating/deactivating the GameObject or toggling its Renderer if it is the root.</summary>
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
