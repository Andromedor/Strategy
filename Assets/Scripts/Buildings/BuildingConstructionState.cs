using System;
using Strategy.Data;
using UnityEngine;

namespace Strategy.Buildings
{
    [DisallowMultipleComponent]
    public sealed class BuildingConstructionState : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _fallbackBuildTime = 10f;

        private BuildingProduction _production;
        private ConstructionCenter _constructionCenter;
        private BuildingSelectionState _selectionState;
        private BuildingHealth _health;
        private float _duration;
        private float _elapsed;
        private bool _isUnderConstruction;

        public event Action<BuildingConstructionState> ProgressChanged;
        public event Action<BuildingConstructionState> ConstructionCompleted;

        public bool IsUnderConstruction => _isUnderConstruction;
        public bool IsComplete => !_isUnderConstruction;
        public float Duration => _duration;
        public float ElapsedSeconds => _elapsed;
        public float RemainingSeconds => _isUnderConstruction ? Mathf.Max(0f, _duration - _elapsed) : 0f;
        public float Progress => _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);

        private void Awake()
        {
            CacheComponents();
        }

        private void Update()
        {
            if (!_isUnderConstruction)
                return;

            _elapsed = Mathf.Min(_duration, _elapsed + Time.deltaTime);
            SyncConstructionHealth();
            ProgressChanged?.Invoke(this);

            if (_elapsed >= _duration)
                CompleteConstruction();
        }

        public void Begin(BuildingData buildingData)
        {
            CacheComponents();
            _duration = ResolveDuration(buildingData);
            _elapsed = 0f;

            if (_duration <= 0f)
            {
                CompleteImmediately();
                return;
            }

            _isUnderConstruction = true;
            _health?.BeginConstructionHealth();
            SyncConstructionHealth();
            ApplyAvailability(false);
            ProgressChanged?.Invoke(this);
        }

        public void CompleteImmediately()
        {
            CacheComponents();
            bool wasUnderConstruction = _isUnderConstruction;
            _duration = 0f;
            _elapsed = 0f;
            _isUnderConstruction = false;
            if (wasUnderConstruction)
                _health?.CompleteConstructionHealth();
            ApplyAvailability(true);
            ProgressChanged?.Invoke(this);

            if (wasUnderConstruction)
                ConstructionCompleted?.Invoke(this);
        }

        public static bool IsConstructing(Component component)
        {
            if (component == null)
                return false;

            BuildingConstructionState state = component.GetComponentInParent<BuildingConstructionState>();
            return state != null && state.IsUnderConstruction;
        }

        public static bool IsConstructing(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            BuildingConstructionState state = gameObject.GetComponentInParent<BuildingConstructionState>();
            return state != null && state.IsUnderConstruction;
        }

        private void CompleteConstruction()
        {
            _isUnderConstruction = false;
            _elapsed = _duration;
            _health?.CompleteConstructionHealth();
            ApplyAvailability(true);
            ProgressChanged?.Invoke(this);
            ConstructionCompleted?.Invoke(this);
        }

        private void SyncConstructionHealth()
        {
            if (_health == null || !_isUnderConstruction)
                return;

            _health.UpdateConstructionHealth(Progress);
        }

        private void ApplyAvailability(bool available)
        {
            if (_production != null && _production.enabled != available)
                _production.enabled = available;

            if (_constructionCenter != null && _constructionCenter.enabled != available)
                _constructionCenter.enabled = available;

            if (_selectionState != null && _selectionState.enabled != available)
                _selectionState.enabled = available;
        }

        private float ResolveDuration(BuildingData buildingData)
        {
            if (buildingData != null && buildingData.BuildTime > 0f)
                return buildingData.BuildTime;

            return Mathf.Max(0f, _fallbackBuildTime);
        }

        private void CacheComponents()
        {
            if (_production == null)
                _production = GetComponent<BuildingProduction>();

            if (_constructionCenter == null)
                _constructionCenter = GetComponent<ConstructionCenter>();

            if (_selectionState == null)
                _selectionState = GetComponent<BuildingSelectionState>();

            if (_health == null)
                _health = GetComponent<BuildingHealth>();
        }
    }
}
