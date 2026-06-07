namespace Strategy.Core
{
    public readonly struct GameTickContext
    {
        public GameTickContext(long tickIndex, float deltaTime, float simulationTime)
        {
            TickIndex = tickIndex;
            DeltaTime = deltaTime;
            SimulationTime = simulationTime;
        }

        public long TickIndex { get; }
        public float DeltaTime { get; }
        public float SimulationTime { get; }
    }
}
