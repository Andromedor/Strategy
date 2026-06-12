using System;
using System.Collections.Generic;
using Strategy.Data;
using UnityEngine;

namespace Strategy.AI
{
    public enum AiDifficultyLevel
    {
        Easy,
        Medium,
        Hard
    }

    [Serializable]
    public struct AiProductionWeight
    {
        [SerializeField] private ProductionItemData _item;
        [SerializeField, Min(0f)] private float _weight;

        public ProductionItemData Item => _item;
        public float Weight => Mathf.Max(0f, _weight);
    }

    [CreateAssetMenu(fileName = "AiDifficultyProfile", menuName = "RTS/AI/Difficulty Profile")]
    public class AiDifficultyProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private AiDifficultyLevel _difficulty = AiDifficultyLevel.Medium;

        [Header("Decision Pace")]
        [SerializeField, Min(0.1f)] private float _decisionInterval = 1.5f;
        [SerializeField, Min(0.5f)] private float _attackCooldown = 18f;
        [SerializeField, Min(0.5f)] private float _captureCooldown = 8f;
        [SerializeField, Min(0.5f)] private float _buildCooldown = 12f;

        [Header("Economy")]
        [SerializeField, Min(0)] private int _minimumOwnedOutposts = 1;
        [SerializeField, Min(0)] private int _resourceReserve = 80;
        [SerializeField, Min(1)] private int _desiredFactoryCount = 1;
        [SerializeField, Min(0f)] private float _outpostUpgradeResourceMultiplier = 2.5f;

        [Header("Army")]
        [SerializeField, Min(1)] private int _captureSquadSize = 2;
        [SerializeField, Min(1)] private int _attackGroupSize = 6;
        [SerializeField, Min(0f)] private float _defenseRadius = 42f;
        [SerializeField, Min(1)] private int _maxPendingWorkPerFactory = 2;
        [SerializeField] private bool _focusHighValueBuildings;

        [Header("Production Weights")]
        [SerializeField] private List<AiProductionWeight> _productionWeights = new();

        public AiDifficultyLevel Difficulty => _difficulty;
        public float DecisionInterval => Mathf.Max(0.1f, _decisionInterval);
        public float AttackCooldown => Mathf.Max(0.5f, _attackCooldown);
        public float CaptureCooldown => Mathf.Max(0.5f, _captureCooldown);
        public float BuildCooldown => Mathf.Max(0.5f, _buildCooldown);
        public int MinimumOwnedOutposts => Mathf.Max(0, _minimumOwnedOutposts);
        public int ResourceReserve => Mathf.Max(0, _resourceReserve);
        public int DesiredFactoryCount => Mathf.Max(1, _desiredFactoryCount);
        public float OutpostUpgradeResourceMultiplier => Mathf.Max(0f, _outpostUpgradeResourceMultiplier);
        public int CaptureSquadSize => Mathf.Max(1, _captureSquadSize);
        public int AttackGroupSize => Mathf.Max(1, _attackGroupSize);
        public float DefenseRadius => Mathf.Max(0f, _defenseRadius);
        public int MaxPendingWorkPerFactory => Mathf.Max(1, _maxPendingWorkPerFactory);
        public bool FocusHighValueBuildings => _focusHighValueBuildings;
        public IReadOnlyList<AiProductionWeight> ProductionWeights => _productionWeights;

        public ProductionItemData SelectProductionItem(IReadOnlyList<ProductionItemData> availableItems)
        {
            if (availableItems == null || availableItems.Count == 0)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < availableItems.Count; i++)
                totalWeight += ResolveWeight(availableItems[i]);

            if (totalWeight <= 0f)
                return availableItems[0];

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            for (int i = 0; i < availableItems.Count; i++)
            {
                ProductionItemData item = availableItems[i];
                roll -= ResolveWeight(item);

                if (roll <= 0f)
                    return item;
            }

            return availableItems[availableItems.Count - 1];
        }

        public static AiDifficultyProfile CreateRuntimeDefault(AiDifficultyLevel difficulty)
        {
            AiDifficultyProfile profile = CreateInstance<AiDifficultyProfile>();
            profile.name = difficulty + " Runtime AI Profile";
            profile.ApplyDefault(difficulty);
            return profile;
        }

        private float ResolveWeight(ProductionItemData item)
        {
            if (item == null)
                return 0f;

            if (_productionWeights == null)
                return 1f;

            for (int i = 0; i < _productionWeights.Count; i++)
            {
                AiProductionWeight weight = _productionWeights[i];
                if (weight.Item == item ||
                    weight.Item != null &&
                    item.UnitData != null &&
                    weight.Item.UnitData == item.UnitData)
                {
                    return weight.Weight;
                }
            }

            return 1f;
        }

        private void ApplyDefault(AiDifficultyLevel difficulty)
        {
            _difficulty = difficulty;

            switch (difficulty)
            {
                case AiDifficultyLevel.Easy:
                    _decisionInterval = 2.5f;
                    _attackCooldown = 28f;
                    _captureCooldown = 12f;
                    _buildCooldown = 18f;
                    _minimumOwnedOutposts = 1;
                    _resourceReserve = 120;
                    _desiredFactoryCount = 1;
                    _captureSquadSize = 1;
                    _attackGroupSize = 4;
                    _defenseRadius = 34f;
                    _maxPendingWorkPerFactory = 1;
                    _focusHighValueBuildings = false;
                    break;

                case AiDifficultyLevel.Hard:
                    _decisionInterval = 0.75f;
                    _attackCooldown = 12f;
                    _captureCooldown = 5f;
                    _buildCooldown = 8f;
                    _minimumOwnedOutposts = 2;
                    _resourceReserve = 60;
                    _desiredFactoryCount = 2;
                    _captureSquadSize = 3;
                    _attackGroupSize = 8;
                    _defenseRadius = 55f;
                    _maxPendingWorkPerFactory = 3;
                    _focusHighValueBuildings = true;
                    break;

                default:
                    _decisionInterval = 1.5f;
                    _attackCooldown = 18f;
                    _captureCooldown = 8f;
                    _buildCooldown = 12f;
                    _minimumOwnedOutposts = 1;
                    _resourceReserve = 80;
                    _desiredFactoryCount = 1;
                    _captureSquadSize = 2;
                    _attackGroupSize = 6;
                    _defenseRadius = 42f;
                    _maxPendingWorkPerFactory = 2;
                    _focusHighValueBuildings = false;
                    break;
            }
        }

        private void OnValidate()
        {
            _decisionInterval = Mathf.Max(0.1f, _decisionInterval);
            _attackCooldown = Mathf.Max(0.5f, _attackCooldown);
            _captureCooldown = Mathf.Max(0.5f, _captureCooldown);
            _buildCooldown = Mathf.Max(0.5f, _buildCooldown);
            _minimumOwnedOutposts = Mathf.Max(0, _minimumOwnedOutposts);
            _resourceReserve = Mathf.Max(0, _resourceReserve);
            _desiredFactoryCount = Mathf.Max(1, _desiredFactoryCount);
            _captureSquadSize = Mathf.Max(1, _captureSquadSize);
            _attackGroupSize = Mathf.Max(1, _attackGroupSize);
            _defenseRadius = Mathf.Max(0f, _defenseRadius);
            _maxPendingWorkPerFactory = Mathf.Max(1, _maxPendingWorkPerFactory);
        }
    }
}
