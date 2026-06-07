using System.Collections.Generic;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    public readonly struct PlayerCommand
    {
        private PlayerCommand(
            PlayerCommandType type,
            TeamType team,
            int playerId,
            IReadOnlyList<GameObject> targets,
            Vector3 targetPosition,
            Transform targetTransform,
            BuildingData buildingData,
            ProductionItemData productionItem)
        {
            Type = type;
            Team = team;
            PlayerId = Mathf.Max(0, playerId);
            Targets = targets;
            TargetPosition = targetPosition;
            TargetTransform = targetTransform;
            BuildingData = buildingData;
            ProductionItem = productionItem;
        }

        public PlayerCommandType Type { get; }
        public TeamType Team { get; }
        public int PlayerId { get; }
        public IReadOnlyList<GameObject> Targets { get; }
        public Vector3 TargetPosition { get; }
        public Transform TargetTransform { get; }
        public BuildingData BuildingData { get; }
        public ProductionItemData ProductionItem { get; }

        public static PlayerCommand MoveUnits(
            TeamType team,
            int playerId,
            IReadOnlyList<GameObject> units,
            Vector3 targetPosition)
        {
            return new PlayerCommand(
                PlayerCommandType.MoveUnits,
                team,
                playerId,
                units,
                targetPosition,
                null,
                null,
                null);
        }

        public static PlayerCommand AttackTarget(
            TeamType team,
            int playerId,
            IReadOnlyList<GameObject> units,
            Transform target)
        {
            return new PlayerCommand(
                PlayerCommandType.AttackTarget,
                team,
                playerId,
                units,
                Vector3.zero,
                target,
                null,
                null);
        }

        public static PlayerCommand BuildStructure(
            TeamType team,
            int playerId,
            BuildingData buildingData,
            Vector3 targetPosition)
        {
            return new PlayerCommand(
                PlayerCommandType.BuildStructure,
                team,
                playerId,
                null,
                targetPosition,
                null,
                buildingData,
                null);
        }

        public static PlayerCommand ProduceUnit(
            TeamType team,
            int playerId,
            IReadOnlyList<GameObject> factories,
            ProductionItemData productionItem)
        {
            return new PlayerCommand(
                PlayerCommandType.ProduceUnit,
                team,
                playerId,
                factories,
                Vector3.zero,
                null,
                null,
                productionItem);
        }
    }

    public enum PlayerCommandType
    {
        MoveUnits,
        AttackTarget,
        BuildStructure,
        ProduceUnit
    }
}
