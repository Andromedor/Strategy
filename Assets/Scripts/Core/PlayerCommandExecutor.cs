using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;
using UnityEngine.AI;

namespace Strategy.Core
{
    public static class PlayerCommandExecutor
    {
        private static readonly List<GameObject> CommandUnits = new();
        private static readonly List<BuildingProduction> CommandFactories = new();

        public static void Execute(PlayerCommand command)
        {
            switch (command.Type)
            {
                case PlayerCommandType.MoveUnits:
                    ExecuteMoveUnits(command);
                    break;

                case PlayerCommandType.AttackTarget:
                    ExecuteAttackTarget(command);
                    break;

                case PlayerCommandType.ProduceUnit:
                    ExecuteProduceUnit(command, out _);
                    break;

                case PlayerCommandType.UpgradeOutpost:
                    ExecuteUpgradeOutpost(command);
                    break;
            }
        }

        public static void ExecuteMoveUnits(PlayerCommand command)
        {
            BuildCommandUnits(command);

            if (CommandUnits.Count == 0)
                return;

            Vector3 anchor = command.TargetPosition;
            Quaternion rotation = ResolveFormationRotation(CommandUnits[0], anchor);
            float spacing = ResolveFormationSpacing(CommandUnits);

            for (int i = 0; i < CommandUnits.Count; i++)
            {
                GameObject unit = CommandUnits[i];
                if (unit == null)
                    continue;

                Vector3 destination = i == 0
                    ? anchor
                    : anchor + rotation * ResolveFormationOffset(i, spacing);

                UnitSpawnActivator spawnActivator = unit.GetComponent<UnitSpawnActivator>();
                NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

                if (TrySampleNavMesh(destination, agent, out Vector3 sampledDestination))
                    destination = sampledDestination;

                EventManager.RaiseUnitMoveCommand(unit, destination);

                if (spawnActivator != null && spawnActivator.IsSpawning)
                {
                    spawnActivator.QueueMoveAfterSpawn(destination);
                    continue;
                }

                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.SetDestination(destination);
            }
        }

        public static void ExecuteAttackTarget(PlayerCommand command)
        {
            if (command.TargetTransform == null)
                return;

            BuildCommandUnits(command);

            for (int i = 0; i < CommandUnits.Count; i++)
            {
                GameObject unit = CommandUnits[i];
                UnitCombat combat = unit != null ? unit.GetComponent<UnitCombat>() : null;

                if (combat == null || !IsHostileTarget(command.Team, command.TargetTransform))
                    continue;

                combat.SetManualAttackTarget(command.TargetTransform);
                EventManager.RaiseUnitAttackTargetChanged(unit, command.TargetTransform);
            }
        }

        public static bool ExecuteProduceUnit(PlayerCommand command, out BuildingProduction targetFactory)
        {
            targetFactory = null;

            if (command.ProductionItem == null || command.Targets == null)
                return false;

            CommandFactories.Clear();
            for (int i = 0; i < command.Targets.Count; i++)
            {
                GameObject target = command.Targets[i];
                BuildingProduction factory = target != null ? target.GetComponent<BuildingProduction>() : null;

                if (factory == null ||
                    !factory.isActiveAndEnabled ||
                    BuildingConstructionState.IsConstructing(factory) ||
                    !BelongsToTeam(factory.gameObject, command.Team))
                {
                    continue;
                }

                CommandFactories.Add(factory);
            }

            return FactoryProductionDistributor.TryQueueLeastLoaded(
                CommandFactories,
                command.ProductionItem,
                out targetFactory);
        }

        public static bool ExecuteUpgradeOutpost(PlayerCommand command)
        {
            Outpost outpost = command.TargetTransform != null
                ? command.TargetTransform.GetComponentInParent<Outpost>()
                : null;

            return outpost != null && outpost.TryUpgrade(command.Team);
        }

        private static void BuildCommandUnits(PlayerCommand command)
        {
            CommandUnits.Clear();

            if (command.Targets == null)
                return;

            for (int i = 0; i < command.Targets.Count; i++)
            {
                GameObject target = command.Targets[i];
                if (target == null || !BelongsToTeam(target, command.Team))
                    continue;

                if (target.GetComponent<UnitCombat>() == null &&
                    target.GetComponent<NavMeshAgent>() == null &&
                    target.GetComponent<UnitSpawnActivator>() == null)
                {
                    continue;
                }

                CommandUnits.Add(target);
            }
        }

        private static bool BelongsToTeam(GameObject target, TeamType team)
        {
            TeamComponent teamComponent = target != null ? target.GetComponentInParent<TeamComponent>() : null;
            return teamComponent != null && teamComponent.Team == team;
        }

        private static bool IsHostileTarget(TeamType sourceTeam, Transform target)
        {
            ITeam targetTeam = target.GetComponentInParent<ITeam>();
            return targetTeam != null && TeamRelations.AreHostile(sourceTeam, targetTeam.Team);
        }

        private static Quaternion ResolveFormationRotation(GameObject firstUnit, Vector3 targetPosition)
        {
            if (firstUnit == null)
                return Quaternion.identity;

            Vector3 direction = targetPosition - firstUnit.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
                direction = firstUnit.transform.forward;

            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(direction.normalized)
                : Quaternion.identity;
        }

        private static float ResolveFormationSpacing(IReadOnlyList<GameObject> units)
        {
            float spacing = 4f;

            for (int i = 0; i < units.Count; i++)
            {
                UnitCombat combat = units[i] != null ? units[i].GetComponent<UnitCombat>() : null;
                if (combat != null && combat.UnitData != null)
                    spacing = Mathf.Max(spacing, combat.UnitData.FormationSpacing);
            }

            return spacing;
        }

        private static Vector3 ResolveFormationOffset(int index, float spacing)
        {
            int row = index / 3;
            int column = index % 3;
            float x = (column - 1) * spacing;
            float z = -row * spacing;
            return new Vector3(x, 0f, z);
        }

        private static bool TrySampleNavMesh(Vector3 position, NavMeshAgent agent, out Vector3 sampledPosition)
        {
            sampledPosition = position;
            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;

            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, areaMask))
                return false;

            sampledPosition = hit.position;
            return true;
        }
    }
}
