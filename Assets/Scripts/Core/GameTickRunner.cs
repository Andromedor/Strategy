using System.Collections.Generic;
using UnityEngine;

namespace Strategy.Core
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public class GameTickRunner : MonoBehaviour
    {
        private const float DefaultTickRate = 10f;
        private const int DefaultMaxCatchUpSteps = 2;

        private static readonly List<TickEntry<ISimulationTickable>> PendingSimulationTickables = new();
        private static readonly List<TickEntry<ILateSimulationTickable>> PendingLateTickables = new();

        [SerializeField] private GameTickConfig _config;

        private readonly List<TickEntry<ISimulationTickable>> _simulationTickables = new();
        private readonly List<TickEntry<ILateSimulationTickable>> _lateTickables = new();
        private float _accumulator;
        private float _simulationTime;
        private long _tickIndex;

        public static GameTickRunner Active { get; private set; }

        public static float ActiveTickDeltaTime =>
            Active != null ? Active.TickDeltaTime : 1f / DefaultTickRate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Active = null;
            PendingSimulationTickables.Clear();
            PendingLateTickables.Clear();
        }

        public static void Register(ISimulationTickable tickable, float intervalSeconds = 0f, float phaseOffsetSeconds = 0f)
        {
            if (tickable == null)
                return;

            TickEntry<ISimulationTickable> entry = new(tickable, intervalSeconds, phaseOffsetSeconds);

            if (Active != null)
                Active.AddSimulationTickable(entry);
            else if (!Contains(PendingSimulationTickables, tickable))
                PendingSimulationTickables.Add(entry);
        }

        public static void Unregister(ISimulationTickable tickable)
        {
            if (tickable == null)
                return;

            Remove(PendingSimulationTickables, tickable);

            if (Active != null)
                Active.RemoveSimulationTickable(tickable);
        }

        public static void RegisterLate(ILateSimulationTickable tickable, float intervalSeconds = 0f, float phaseOffsetSeconds = 0f)
        {
            if (tickable == null)
                return;

            TickEntry<ILateSimulationTickable> entry = new(tickable, intervalSeconds, phaseOffsetSeconds);

            if (Active != null)
                Active.AddLateTickable(entry);
            else if (!Contains(PendingLateTickables, tickable))
                PendingLateTickables.Add(entry);
        }

        public static void UnregisterLate(ILateSimulationTickable tickable)
        {
            if (tickable == null)
                return;

            Remove(PendingLateTickables, tickable);

            if (Active != null)
                Active.RemoveLateTickable(tickable);
        }

        private float TickDeltaTime => _config != null ? _config.TickDeltaTime : 1f / DefaultTickRate;
        private int MaxCatchUpSteps => _config != null ? _config.MaxCatchUpSteps : DefaultMaxCatchUpSteps;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(gameObject);
                return;
            }

            Active = this;
            FlushPendingRegistrations();
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        private void Update()
        {
            float tickDeltaTime = TickDeltaTime;
            _accumulator += Time.deltaTime;

            int steps = 0;
            int maxSteps = MaxCatchUpSteps;

            while (_accumulator >= tickDeltaTime && steps < maxSteps)
            {
                _accumulator -= tickDeltaTime;
                _simulationTime += tickDeltaTime;
                _tickIndex++;

                GameTickContext context = new(_tickIndex, tickDeltaTime, _simulationTime);
                TickSimulation(context);
                TickLateSimulation(context);
                steps++;
            }

            if (steps == maxSteps && _accumulator >= tickDeltaTime)
                _accumulator = 0f;
        }

        private void FlushPendingRegistrations()
        {
            for (int i = 0; i < PendingSimulationTickables.Count; i++)
                AddSimulationTickable(PendingSimulationTickables[i]);

            for (int i = 0; i < PendingLateTickables.Count; i++)
                AddLateTickable(PendingLateTickables[i]);

            PendingSimulationTickables.Clear();
            PendingLateTickables.Clear();
        }

        private void AddSimulationTickable(TickEntry<ISimulationTickable> entry)
        {
            if (entry.Target == null || Contains(_simulationTickables, entry.Target))
                return;

            entry.ResetSchedule(_simulationTime);
            _simulationTickables.Add(entry);
        }

        private void RemoveSimulationTickable(ISimulationTickable tickable)
        {
            Remove(_simulationTickables, tickable);
        }

        private void AddLateTickable(TickEntry<ILateSimulationTickable> entry)
        {
            if (entry.Target == null || Contains(_lateTickables, entry.Target))
                return;

            entry.ResetSchedule(_simulationTime);
            _lateTickables.Add(entry);
        }

        private void RemoveLateTickable(ILateSimulationTickable tickable)
        {
            Remove(_lateTickables, tickable);
        }

        private void TickSimulation(GameTickContext context)
        {
            for (int i = _simulationTickables.Count - 1; i >= 0; i--)
            {
                TickEntry<ISimulationTickable> entry = _simulationTickables[i];

                if (entry.Target == null)
                {
                    _simulationTickables.RemoveAt(i);
                    continue;
                }

                if (!entry.ShouldTick(context.SimulationTime))
                {
                    _simulationTickables[i] = entry;
                    continue;
                }

                _simulationTickables[i] = entry;
                entry.Target.Tick(context);
            }
        }

        private void TickLateSimulation(GameTickContext context)
        {
            for (int i = _lateTickables.Count - 1; i >= 0; i--)
            {
                TickEntry<ILateSimulationTickable> entry = _lateTickables[i];

                if (entry.Target == null)
                {
                    _lateTickables.RemoveAt(i);
                    continue;
                }

                if (!entry.ShouldTick(context.SimulationTime))
                {
                    _lateTickables[i] = entry;
                    continue;
                }

                _lateTickables[i] = entry;
                entry.Target.LateTick(context);
            }
        }

        private static bool Contains<T>(List<TickEntry<T>> entries, T target)
            where T : class
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i].Target, target))
                    return true;
            }

            return false;
        }

        private static void Remove<T>(List<TickEntry<T>> entries, T target)
            where T : class
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(entries[i].Target, target))
                    entries.RemoveAt(i);
            }
        }

        private struct TickEntry<T>
            where T : class
        {
            private readonly float _intervalSeconds;
            private readonly float _phaseOffsetSeconds;
            private float _nextTickTime;

            public TickEntry(T target, float intervalSeconds, float phaseOffsetSeconds)
            {
                Target = target;
                _intervalSeconds = Mathf.Max(0f, intervalSeconds);
                _phaseOffsetSeconds = Mathf.Max(0f, phaseOffsetSeconds);
                _nextTickTime = 0f;
            }

            public T Target { get; }

            public void ResetSchedule(float currentSimulationTime)
            {
                _nextTickTime = currentSimulationTime + _phaseOffsetSeconds;
            }

            public bool ShouldTick(float simulationTime)
            {
                if (_intervalSeconds <= 0f)
                    return true;

                if (simulationTime + 0.0001f < _nextTickTime)
                    return false;

                do
                {
                    _nextTickTime += _intervalSeconds;
                }
                while (_nextTickTime <= simulationTime + 0.0001f);

                return true;
            }
        }
    }
}
