using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    public static class LocalPlayerContext
    {
        public static TeamType LocalTeam { get; private set; } = TeamType.Player;
        public static int LocalPlayerId { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            LocalTeam = TeamType.Player;
            LocalPlayerId = 0;
        }

        public static void SetLocalPlayer(TeamType team, int playerId = 0)
        {
            LocalTeam = team;
            LocalPlayerId = Mathf.Max(0, playerId);
        }

        public static bool IsLocalTeam(TeamType team)
        {
            return team == LocalTeam;
        }
    }
}
