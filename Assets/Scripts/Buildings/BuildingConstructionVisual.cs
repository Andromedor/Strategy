using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategy.Buildings
{
    [DisallowMultipleComponent]
    public sealed class BuildingConstructionVisual : MonoBehaviour
    {
        [SerializeField] private BuildingConstructionState _construction;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Renderer[] _assemblyRenderers;
        [SerializeField] private GameObject _effectRoot;
        [SerializeField] private ParticleSystem[] _constructionParticles;
        [SerializeField, Min(0)] private int _initialVisiblePartCount = 1;
        [SerializeField, Min(1)] private int _maxStageCount = 60;
        [SerializeField] private Vector3 _partStartLocalOffset = new(0f, -1.15f, 0f);
        [SerializeField, Range(0.05f, 1f)] private float _partStartScale = 0.9f;
        [SerializeField, Range(0.25f, 3f)] private float _partRevealStageSpan = 1f;

        private readonly List<PartState> _parts = new();
        private BuildingHealthBarPresenter[] _healthBarPresenters;
        private bool _captured;

        private void Awake()
        {
            CacheReferences();
            CaptureParts();
            ApplyIdleState();
        }

        private void OnEnable()
        {
            CacheReferences();

            if (_construction != null)
            {
                _construction.ProgressChanged += OnProgressChanged;
                _construction.ConstructionCompleted += OnConstructionCompleted;
            }

            ApplyIdleState();
        }

        private void OnDisable()
        {
            if (_construction != null)
            {
                _construction.ProgressChanged -= OnProgressChanged;
                _construction.ConstructionCompleted -= OnConstructionCompleted;
            }
        }

        private void Update()
        {
            if (_construction == null || !_construction.IsUnderConstruction)
                return;

            ApplyProgress(_construction.Progress);
        }

        private void OnProgressChanged(BuildingConstructionState state)
        {
            if (state == null)
                return;

            ApplyProgress(state.Progress);
        }

        private void OnConstructionCompleted(BuildingConstructionState state)
        {
            RestoreCompletedState();
        }

        private void CacheReferences()
        {
            if (_construction == null)
                _construction = GetComponent<BuildingConstructionState>();

            if (_visualRoot == null)
                _visualRoot = transform;

            _healthBarPresenters ??= GetComponents<BuildingHealthBarPresenter>();
        }

        private void CaptureParts()
        {
            if (_captured)
                return;

            CacheReferences();

            Renderer[] source = _assemblyRenderers != null && _assemblyRenderers.Length > 0
                ? _assemblyRenderers
                : _visualRoot.GetComponentsInChildren<Renderer>(true);

            _parts.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                Renderer renderer = source[i];
                if (renderer == null || IsIgnoredRenderer(renderer))
                    continue;

                Transform partTransform = renderer.transform;
                _parts.Add(new PartState(
                    renderer,
                    partTransform,
                    partTransform.localPosition,
                    partTransform.localScale,
                    renderer.enabled,
                    ResolveSortHeight(renderer)));
            }

            _parts.Sort((first, second) => first.SortHeight.CompareTo(second.SortHeight));
            _captured = true;
        }

        private void ApplyIdleState()
        {
            if (_construction != null && _construction.IsUnderConstruction)
            {
                ApplyProgress(_construction.Progress);
                return;
            }

            RestoreCompletedState();
        }

        private void ApplyProgress(float progress)
        {
            CaptureParts();

            bool constructing = _construction != null && _construction.IsUnderConstruction;
            SetEffectsActive(constructing);

            if (_parts.Count == 0)
                return;

            if (!constructing)
            {
                RestoreCompletedState();
                return;
            }

            for (int i = 0; i < _parts.Count; i++)
            {
                PartState part = _parts[i];
                float reveal = ResolvePartReveal(i, progress);
                bool visible = part.WasEnabled && reveal > 0f;
                part.Renderer.enabled = visible;

                if (!visible)
                {
                    part.Transform.localPosition = part.LocalPosition;
                    part.Transform.localScale = part.LocalScale;
                    continue;
                }

                float easedReveal = Mathf.SmoothStep(0f, 1f, reveal);
                part.Transform.localPosition = Vector3.Lerp(
                    part.LocalPosition + _partStartLocalOffset,
                    part.LocalPosition,
                    easedReveal);
                part.Transform.localScale = Vector3.Lerp(
                    part.LocalScale * Mathf.Clamp01(_partStartScale),
                    part.LocalScale,
                    easedReveal);
            }
        }

        private void RestoreCompletedState()
        {
            CaptureParts();
            SetEffectsActive(false);

            for (int i = 0; i < _parts.Count; i++)
            {
                PartState part = _parts[i];
                part.Renderer.enabled = part.WasEnabled;
                part.Transform.localPosition = part.LocalPosition;
                part.Transform.localScale = part.LocalScale;
            }
        }

        private float ResolvePartReveal(int partIndex, float progress)
        {
            if (_parts.Count == 0)
                return 0f;

            if (progress >= 1f)
                return 1f;

            int initialCount = Mathf.Clamp(_initialVisiblePartCount, 0, _parts.Count);
            if (partIndex < initialCount)
                return 1f;

            int animatedPartCount = Mathf.Max(1, _parts.Count - initialCount);
            int stageCount = ResolveStageCount();
            int animatedIndex = Mathf.Clamp(partIndex - initialCount, 0, animatedPartCount - 1);
            float startStage = Mathf.Lerp(0f, stageCount - 1f, animatedIndex / (float)animatedPartCount);
            float startProgress = startStage / stageCount;
            float endProgress = Mathf.Min(1f, (startStage + Mathf.Max(0.25f, _partRevealStageSpan)) / stageCount);

            if (endProgress <= startProgress)
                return progress >= startProgress ? 1f : 0f;

            return Mathf.Clamp01(Mathf.InverseLerp(startProgress, endProgress, Mathf.Clamp01(progress)));
        }

        private int ResolveStageCount()
        {
            float duration = _construction != null ? _construction.Duration : 0f;
            int stagesFromSeconds = Mathf.Max(1, Mathf.CeilToInt(duration));
            return Mathf.Clamp(stagesFromSeconds, 1, Mathf.Max(1, _maxStageCount));
        }

        private void SetEffectsActive(bool active)
        {
            if (_effectRoot != null && _effectRoot.activeSelf != active)
                _effectRoot.SetActive(active);

            if (_constructionParticles == null)
                return;

            for (int i = 0; i < _constructionParticles.Length; i++)
            {
                ParticleSystem particles = _constructionParticles[i];
                if (particles == null)
                    continue;

                if (active && !particles.isPlaying)
                    particles.Play(true);
                else if (!active && particles.isPlaying)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private bool IsIgnoredRenderer(Renderer renderer)
        {
            if (renderer is ParticleSystemRenderer)
                return true;

            if (renderer.GetComponentInParent<Canvas>() != null)
                return true;

            if (IsHealthBarRenderer(renderer))
                return true;

            string objectName = renderer.name;
            return objectName.Contains("StatusBar", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("HealthBar", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("ProductionStatus", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("ConstructionAssemblyFx", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsHealthBarRenderer(Renderer renderer)
        {
            if (_healthBarPresenters == null || renderer == null)
                return false;

            for (int i = 0; i < _healthBarPresenters.Length; i++)
            {
                BuildingHealthBarPresenter presenter = _healthBarPresenters[i];
                if (presenter != null && presenter.OwnsRenderer(renderer))
                    return true;
            }

            return false;
        }

        private static float ResolveSortHeight(Renderer renderer)
        {
            return renderer != null ? renderer.bounds.center.y : 0f;
        }

        [Serializable]
        private readonly struct PartState
        {
            public readonly Renderer Renderer;
            public readonly Transform Transform;
            public readonly Vector3 LocalPosition;
            public readonly Vector3 LocalScale;
            public readonly bool WasEnabled;
            public readonly float SortHeight;

            public PartState(
                Renderer renderer,
                Transform transform,
                Vector3 localPosition,
                Vector3 localScale,
                bool wasEnabled,
                float sortHeight)
            {
                Renderer = renderer;
                Transform = transform;
                LocalPosition = localPosition;
                LocalScale = localScale;
                WasEnabled = wasEnabled;
                SortHeight = sortHeight;
            }
        }
    }
}
