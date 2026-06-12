using System;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    public static class CommandDispatcher
    {
        public static event Action<PlayerCommand> CommandSubmitted;
        public static event Action<PlayerCommand> CommandExecuted;
        public static event Action<PlayerCommand, string> CommandRejected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            CommandSubmitted = null;
            CommandExecuted = null;
            CommandRejected = null;
        }

        public static bool Dispatch(PlayerCommand command, Action<PlayerCommand> offlineExecutor)
        {
            CommandSubmitted?.Invoke(command);

            if (!Validate(command, out string reason))
            {
                CommandRejected?.Invoke(command, reason);
                return false;
            }

            offlineExecutor?.Invoke(command);
            CommandExecuted?.Invoke(command);
            return true;
        }

        private static bool Validate(PlayerCommand command, out string reason)
        {
            reason = string.Empty;

            if (command.Team == TeamType.Neutral)
            {
                reason = "Neutral team cannot issue player commands.";
                return false;
            }

            if (command.Type == PlayerCommandType.MoveUnits ||
                command.Type == PlayerCommandType.AttackTarget ||
                command.Type == PlayerCommandType.ProduceUnit)
            {
                if (command.Targets == null || command.Targets.Count == 0)
                {
                    reason = "Command has no targets.";
                    return false;
                }
            }

            if (command.Type == PlayerCommandType.AttackTarget && command.TargetTransform == null)
            {
                reason = "Attack command has no target.";
                return false;
            }

            if (command.Type == PlayerCommandType.BuildStructure && command.BuildingData == null)
            {
                reason = "Build command has no building data.";
                return false;
            }

            if (command.Type == PlayerCommandType.ProduceUnit && command.ProductionItem == null)
            {
                reason = "Produce command has no production item.";
                return false;
            }

            if (command.Type == PlayerCommandType.UpgradeOutpost && command.TargetTransform == null)
            {
                reason = "Upgrade outpost command has no target.";
                return false;
            }

            return true;
        }
    }
}
