namespace Strategy.Core
{
    public interface ISimulationTickable
    {
        void Tick(GameTickContext context);
    }
}
