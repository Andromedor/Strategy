using Strategy.Core;
using Strategy.Buildings;
using System;
using System.Collections.Generic;
using Strategy.Units;
using UnityEngine;

using Strategy.Data;
using Strategy.UI;
namespace Strategy.Buildings
{
    public class Outpost : MonoBehaviour
    {
        private const string BaseColorProperty = "_BaseColor";
        private const string ColorProperty = "_Color";

        [Header("Capture")]
        [SerializeField] private float _captureTime = 30f;

        [Header("Resources")]
        [SerializeField, Min(0.01f)] private float _resourceTicksPerMinute = 12f;
        [SerializeField] private int _baseResourcePerTick = 10;
        [SerializeField] private int _upgradedResourcePerTick = 25;

        [Header("Upgrade")]
        [SerializeField] private int _upgradeCost = 100;

        [Header("Visual")]
        [SerializeField] private Renderer _zoneRenderer;
        [SerializeField] private Color _neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.28f);
        [SerializeField] private Color _playerColor = new Color(0f, 1f, 0f, 0.28f);
        [SerializeField] private Color _enemyColor = new Color(1f, 0f, 0f, 0.28f);

        [Header("Build Area")]
        [SerializeField] private ConstructionCenter _extraBuildArea;

        private TeamType? _owner;
        private TeamType? _capturingTeam;
        private float _captureProgress;
        private bool _isUpgraded;
        private float _resourceTimer;
        private MaterialPropertyBlock _propertyBlock;

        private static readonly List<Outpost> AllOutposts = new();

    public static event Action OnStatsChanged;

        public TeamType? Owner => _owner;
        public TeamType? CapturingTeam => _capturingTeam;
        public float CaptureProgress =>
            _captureTime <= 0f ? 0f : Mathf.Clamp01(_captureProgress / _captureTime);
        public bool IsBeingCaptured => _capturingTeam != null && _captureProgress > 0f;
        public bool IsUpgraded => _isUpgraded;
        public int UpgradeCost => _upgradeCost;
        public int CurrentResourcePerTick =>
            _isUpgraded ? _upgradedResourcePerTick : _baseResourcePerTick;
        public float ResourceTicksPerMinute => _resourceTicksPerMinute;
        public float CurrentResourcePerMinute => CurrentResourcePerTick * _resourceTicksPerMinute;
        public bool CanUpgrade =>
            _owner == TeamType.Player &&
            !_isUpgraded &&
            ResourceManager.Instance != null &&
            ResourceManager.Instance.Resource >= _upgradeCost;
        public Color CurrentZoneColor
        {
            get
            {
                if (_owner == null)
                    return _neutralColor;

                return GetColorForTeam(_owner.Value);
            }
        }

    public Color GetColorForTeam(TeamType team)
    {
        return team == TeamType.Player ? _playerColor : _enemyColor;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        AllOutposts.Clear();
        OnStatsChanged = null;
    }

        public static int GetOwnedCount(TeamType team)
        {
            int count = 0;

            foreach (Outpost outpost in AllOutposts)
            {
                if (outpost != null && outpost.Owner == team)
                    count++;
            }

            return count;
        }

        public static float GetResourcePerMinute(TeamType team)
        {
            float resourcePerMinute = 0f;

            foreach (Outpost outpost in AllOutposts)
            {
                if (outpost != null && outpost.Owner == team)
                    resourcePerMinute += outpost.CurrentResourcePerMinute;
            }

            return resourcePerMinute;
        }

        public static float GetResourceTicksPerMinute(TeamType team)
        {
            float ticksPerMinute = 0f;

            foreach (Outpost outpost in AllOutposts)
            {
                if (outpost != null && outpost.Owner == team)
                    ticksPerMinute += outpost.ResourceTicksPerMinute;
            }

            return ticksPerMinute;
        }

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            SetBuildAreaActive(false, false);
            UpdateVisual();
        }

        private void OnEnable()
        {
            if (!AllOutposts.Contains(this))
                AllOutposts.Add(this);

            NotifyStatsChanged();
        }

        private void OnDisable()
        {
            AllOutposts.Remove(this);
            NotifyStatsChanged();
        }

        private void Update()
        {
            GenerateResource();
        }

        public void TickCapture(int playerUnits, int enemyUnits, bool hasBlockingBuildings, float deltaTime)
        {
            if (!CanCapture(playerUnits, enemyUnits, hasBlockingBuildings, out TeamType team))
            {
                ResetCaptureProgress();
                return;
            }

            if (_capturingTeam != team)
            {
                _capturingTeam = team;
                _captureProgress = 0f;
            }

            _captureProgress += deltaTime;

            if (_captureProgress >= _captureTime)
                Capture(team);
        }

        private bool CanCapture(
            int playerUnits,
            int enemyUnits,
            bool hasBlockingBuildings,
            out TeamType capturingTeam)
        {
            capturingTeam = default;

            if (hasBlockingBuildings)
                return false;

            if (playerUnits > 0 && enemyUnits > 0)
                return false;

            if (playerUnits == 0 && enemyUnits == 0)
                return false;

            capturingTeam = playerUnits > 0 ? TeamType.Player : TeamType.Enemy;

            if (_owner == capturingTeam)
                return false;

            if (_owner == TeamType.Player)
                return playerUnits == 0;

            if (_owner == TeamType.Enemy)
                return enemyUnits == 0;

            return true;
        }

        private void Capture(TeamType team)
        {
            bool ownerChanged = _owner != null && _owner != team;

            if (ownerChanged)
                ResetUpgrade();

            _owner = team;
            _capturingTeam = null;
            _captureProgress = 0f;
            _resourceTimer = 0f;

            UpdateVisual();
            NotifyStatsChanged();
        }

        private void ResetUpgrade()
        {
            _isUpgraded = false;
            SetBuildAreaActive(false, false);
        }

        private void ResetCaptureProgress()
        {
            _capturingTeam = null;
            _captureProgress = 0f;
        }

        public bool TryUpgrade()
        {
            if (!CanUpgrade)
                return false;

            if (!ResourceManager.Instance.Spend(_upgradeCost))
                return false;

            _isUpgraded = true;
            SetBuildAreaActive(true, true);
            NotifyStatsChanged();

            return true;
        }

        private void GenerateResource()
        {
            if (_owner == null)
                return;

            float resourceTickTime = 60f / Mathf.Max(0.01f, _resourceTicksPerMinute);

            _resourceTimer += Time.deltaTime;

            if (_resourceTimer < resourceTickTime)
                return;

            _resourceTimer -= resourceTickTime;

            if (ResourceManager.Instance != null)
                ResourceManager.Instance.Add(_owner.Value, CurrentResourcePerTick);
        }

        private void UpdateVisual()
        {
            if (_zoneRenderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            _zoneRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorProperty, CurrentZoneColor);
            _propertyBlock.SetColor(ColorProperty, CurrentZoneColor);
            _zoneRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void SetBuildAreaActive(bool active, bool showVisual)
        {
            if (_extraBuildArea == null)
                return;

            _extraBuildArea.enabled = active;

            if (!active)
            {
                _extraBuildArea.HideBuildArea();
                return;
            }

            if (showVisual)
                _extraBuildArea.ShowBuildArea();
            else
                _extraBuildArea.HideBuildArea();
        }

        private static void NotifyStatsChanged()
        {
            OnStatsChanged?.Invoke();
        }
    }

    public enum OutpostState
    {
        Neutral,
        PlayerOwned,
        EnemyOwned
    }
}
