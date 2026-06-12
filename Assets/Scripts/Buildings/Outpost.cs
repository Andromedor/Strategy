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
    /// <summary>
    /// Захоплюваний об'єкт на карті, що генерує ресурси для власника з плином часу.
    /// Відстежує власника, прогрес захоплення та необов'язкове одноразове покращення, яке
    /// подвоює дохід і активує додаткову зону будівництва ConstructionCenter.
    /// </summary>
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
            CanUpgradeFor(LocalPlayerContext.LocalTeam);
        public Color CurrentZoneColor
        {
            get
            {
                if (_owner == null)
                    return _neutralColor;

                return GetColorForTeam(_owner.Value);
            }
        }

    /// <summary>Повертає колір зони, призначений для вказаної команди (гравець = зелений, ворог = червоний).</summary>
    public Color GetColorForTeam(TeamType team)
    {
        return team switch
        {
            TeamType.Player => _playerColor,
            TeamType.Enemy => _enemyColor,
            TeamType.Team3 => new Color(0.95f, 0.8f, 0.05f, 0.28f),
            TeamType.Team4 => new Color(0.55f, 0.25f, 1f, 0.28f),
            TeamType.Team5 => new Color(0f, 0.75f, 1f, 0.28f),
            TeamType.Team6 => new Color(1f, 0.45f, 0f, 0.28f),
            TeamType.Team7 => new Color(0.85f, 0.25f, 0.75f, 0.28f),
            TeamType.Team8 => new Color(0.3f, 1f, 0.45f, 0.28f),
            _ => _neutralColor
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        AllOutposts.Clear();
        OnStatsChanged = null;
    }

        /// <summary>Повертає кількість аванпостів, якими зараз володіє вказана команда.</summary>
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

        /// <summary>Підсумовує поточний дохід ресурсів на хвилину з усіх аванпостів, якими володіє вказана команда.</summary>
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

        /// <summary>Підсумовує частоту тіків ресурсів (тіків на хвилину) з усіх аванпостів, якими володіє вказана команда.</summary>
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

        public static IReadOnlyList<Outpost> All => AllOutposts;

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

        /// <summary>
        /// Викликається щокадру з OutpostCaptureZone із поточними лічильниками окупації.
        /// Збільшує або скидає прогрес захоплення та запускає передачу власності після завершення.
        /// </summary>
        public void TickCapture(int playerUnits, int enemyUnits, bool hasBlockingBuildings, float deltaTime)
        {
            Dictionary<TeamType, int> unitCounts = new()
            {
                [TeamType.Player] = Mathf.Max(0, playerUnits),
                [TeamType.Enemy] = Mathf.Max(0, enemyUnits)
            };

            TickCapture(unitCounts, hasBlockingBuildings, deltaTime);
        }

        public void TickCapture(
            IReadOnlyDictionary<TeamType, int> unitCountsByTeam,
            bool hasBlockingBuildings,
            float deltaTime)
        {
            if (!CanCapture(unitCountsByTeam, hasBlockingBuildings, out TeamType team))
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

        /// <summary>
        /// Визначає, чи є спроба захоплення дійсною з урахуванням поточної кількості юнітів та блокуючих будівель.
        /// Повертає false і встановлює capturingTeam у значення за замовчуванням, якщо захоплення не дозволено.
        /// </summary>
        private bool CanCapture(
            IReadOnlyDictionary<TeamType, int> unitCountsByTeam,
            bool hasBlockingBuildings,
            out TeamType capturingTeam)
        {
            capturingTeam = default;

            if (hasBlockingBuildings)
                return false;

            if (unitCountsByTeam == null || unitCountsByTeam.Count == 0)
                return false;

            bool foundCandidate = false;
            int presentHostileSides = 0;

            foreach (KeyValuePair<TeamType, int> pair in unitCountsByTeam)
            {
                TeamType team = pair.Key;
                int count = pair.Value;

                if (team == TeamType.Neutral || count <= 0)
                    continue;

                if (!foundCandidate)
                {
                    capturingTeam = team;
                    foundCandidate = true;
                    presentHostileSides = 1;
                    continue;
                }

                if (!TeamRelations.AreAllied(capturingTeam, team))
                    presentHostileSides++;
            }

            if (!foundCandidate || presentHostileSides != 1)
                return false;

            if (_owner == capturingTeam)
                return false;

            if (_owner != null && TeamRelations.AreAllied(_owner.Value, capturingTeam))
                return false;

            return true;
        }

        /// <summary>Завершує передачу власності команді-захопнику, скидає таймери та оновлює візуал і статистику.</summary>
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

        /// <summary>Скасовує стан покращення та вимикає додаткову зону будівництва, коли аванпост змінює власника.</summary>
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

        /// <summary>
        /// Намагається придбати покращення аванпосту, витрачаючи ресурси.
        /// У разі успіху подвоює дохід ресурсів та активує додатковий ConstructionCenter.
        /// </summary>
        public bool CanUpgradeFor(TeamType team)
        {
            return _owner == team &&
                   !_isUpgraded &&
                   ResourceManager.Instance != null &&
                   ResourceManager.Instance.GetResource(team) >= _upgradeCost;
        }

        public bool TryUpgrade()
        {
            return TryUpgrade(LocalPlayerContext.LocalTeam);
        }

        public bool TryUpgrade(TeamType team)
        {
            if (!CanUpgradeFor(team))
                return false;

            if (!ResourceManager.Instance.Spend(team, _upgradeCost))
                return false;

            _isUpgraded = true;
            SetBuildAreaActive(true, true);
            NotifyStatsChanged();

            return true;
        }

        public OutpostSaveState CaptureState()
        {
            return new OutpostSaveState(
                _owner != null,
                _owner.GetValueOrDefault(),
                _capturingTeam != null,
                _capturingTeam.GetValueOrDefault(),
                _captureProgress,
                _isUpgraded,
                _resourceTimer);
        }

        public void RestoreState(OutpostSaveState state)
        {
            _owner = state.HasOwner ? state.Owner : null;
            _capturingTeam = state.HasCapturingTeam ? state.CapturingTeam : null;
            _captureProgress = Mathf.Max(0f, state.CaptureProgressSeconds);
            _isUpgraded = state.IsUpgraded;
            _resourceTimer = Mathf.Max(0f, state.ResourceTimer);
            SetBuildAreaActive(_isUpgraded, false);
            UpdateVisual();
            NotifyStatsChanged();
        }

        /// <summary>
        /// Викликається щокадру; накопичує час, що минув, і викликає ResourceManager.Add, коли закінчується інтервал тіку.
        /// Нічого не робить, якщо аванпост не має власника.
        /// </summary>
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

        /// <summary>Застосовує колір зони залежно від власника до рендерера зони через MaterialPropertyBlock.</summary>
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

        /// <summary>Вмикає або вимикає необов'язковий додатковий ConstructionCenter і керує його візуальним індикатором.</summary>
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

    [Serializable]
    public struct OutpostSaveState
    {
        [SerializeField] private bool _hasOwner;
        [SerializeField] private TeamType _owner;
        [SerializeField] private bool _hasCapturingTeam;
        [SerializeField] private TeamType _capturingTeam;
        [SerializeField] private float _captureProgressSeconds;
        [SerializeField] private bool _isUpgraded;
        [SerializeField] private float _resourceTimer;

        public bool HasOwner => _hasOwner;
        public TeamType Owner => _owner;
        public bool HasCapturingTeam => _hasCapturingTeam;
        public TeamType CapturingTeam => _capturingTeam;
        public float CaptureProgressSeconds => _captureProgressSeconds;
        public bool IsUpgraded => _isUpgraded;
        public float ResourceTimer => _resourceTimer;

        public OutpostSaveState(
            bool hasOwner,
            TeamType owner,
            bool hasCapturingTeam,
            TeamType capturingTeam,
            float captureProgressSeconds,
            bool isUpgraded,
            float resourceTimer)
        {
            _hasOwner = hasOwner;
            _owner = owner;
            _hasCapturingTeam = hasCapturingTeam;
            _capturingTeam = capturingTeam;
            _captureProgressSeconds = Mathf.Max(0f, captureProgressSeconds);
            _isUpgraded = isUpgraded;
            _resourceTimer = Mathf.Max(0f, resourceTimer);
        }
    }
}
