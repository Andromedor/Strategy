using Strategy.Units;

namespace Strategy.Core
{
    public enum MatchResultKind
    {
        Victory,
        Defeat
    }

    public readonly struct MatchResult
    {
        public MatchResult(MatchResultKind kind, TeamType localTeam)
        {
            Kind = kind;
            LocalTeam = localTeam;
        }

        public MatchResultKind Kind { get; }
        public TeamType LocalTeam { get; }
        public bool IsVictory => Kind == MatchResultKind.Victory;
    }
}
