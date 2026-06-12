using System.Collections.Generic;
using Strategy.Core;
using UnityEngine;

namespace Strategy.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkCommandBridge : MonoBehaviour
    {
        public void SubmitCommand(PlayerCommand command)
        {
            CommandDispatcher.Dispatch(command, PlayerCommandExecutor.Execute);
        }

        public void SubmitMove(PlayerCommand command)
        {
            SubmitCommand(command);
        }

        public void SubmitAttack(PlayerCommand command)
        {
            SubmitCommand(command);
        }

        public void SubmitProduction(PlayerCommand command)
        {
            SubmitCommand(command);
        }
    }
}
