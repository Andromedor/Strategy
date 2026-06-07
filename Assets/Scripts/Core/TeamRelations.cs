using System.Collections.Generic;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    public static class TeamRelations
    {
        private static readonly HashSet<TeamPair> AlliedPairs = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            AlliedPairs.Clear();
        }

        public static bool AreAllied(TeamType first, TeamType second)
        {
            if (first == second)
                return true;

            if (first == TeamType.Neutral || second == TeamType.Neutral)
                return false;

            return AlliedPairs.Contains(new TeamPair(first, second));
        }

        public static bool AreHostile(TeamType first, TeamType second)
        {
            if (first == second)
                return false;

            if (first == TeamType.Neutral || second == TeamType.Neutral)
                return false;

            return !AreAllied(first, second);
        }

        public static void SetAlliance(TeamType first, TeamType second, bool allied)
        {
            if (first == second)
                return;

            TeamPair pair = new TeamPair(first, second);

            if (allied)
                AlliedPairs.Add(pair);
            else
                AlliedPairs.Remove(pair);
        }

        public static void ClearAlliances()
        {
            AlliedPairs.Clear();
        }

        private readonly struct TeamPair
        {
            private readonly TeamType _first;
            private readonly TeamType _second;

            public TeamPair(TeamType first, TeamType second)
            {
                if ((int)first <= (int)second)
                {
                    _first = first;
                    _second = second;
                }
                else
                {
                    _first = second;
                    _second = first;
                }
            }

            public override int GetHashCode()
            {
                return ((int)_first * 397) ^ (int)_second;
            }

            public override bool Equals(object obj)
            {
                return obj is TeamPair other &&
                       _first == other._first &&
                       _second == other._second;
            }
        }
    }
}
