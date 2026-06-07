namespace Strategy.Core
{
    public interface ILateSimulationTickable
    {
        void LateTick(GameTickContext context);
    }
}
