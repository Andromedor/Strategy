using UnityEngine;

namespace Strategy.Core
{
    [CreateAssetMenu(menuName = "Strategy/Simulation/Game Tick Config", fileName = "GameTickConfig")]
    public class GameTickConfig : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _tickRate = 10f;
        [SerializeField, Min(1)] private int _maxCatchUpSteps = 2;

        public float TickRate => Mathf.Max(1f, _tickRate);
        public float TickDeltaTime => 1f / TickRate;
        public int MaxCatchUpSteps => Mathf.Max(1, _maxCatchUpSteps);
    }
}
