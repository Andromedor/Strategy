using System;
using System.Collections.Generic;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    /// <summary>
    /// Синглтон, що відстежує окремі пули ресурсів для гравця та ворога.
    /// Аванпости додають ресурси через Add(); юніти та будівлі витрачають їх через Spend().
    /// Викидає OnResourceChanged щоразу, коли пул гравця змінюється.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public static event Action<int> OnResourceChanged;
        public static event Action<TeamType, int> OnTeamResourceChanged;

        [SerializeField] private int _startResource = 500;
        [SerializeField] private int _startEnemyResource = 500;
        [SerializeField] private int _defaultTeamResource = 500;
        [SerializeField] private List<TeamResourceAmount> _extraStartingResources = new();

        private readonly Dictionary<TeamType, int> _resources = new();

        public int Resource => GetResource(LocalPlayerContext.LocalTeam);
        public int EnemyResource => GetResource(TeamType.Enemy);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            OnResourceChanged = null;
            OnTeamResourceChanged = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            InitializeResources();

            OnResourceChanged?.Invoke(Resource);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Вираховує суму з пулу ресурсів гравця, якщо вистачає коштів; викидає OnResourceChanged та повертає true у разі успіху.</summary>
        public bool Spend(int amount)
        {
            return Spend(LocalPlayerContext.LocalTeam, amount);
        }

        public bool Spend(TeamType team, int amount)
        {
            if (amount <= 0)
                return true;

            int current = GetResource(team);
            if (current < amount)
                return false;

            SetResource(team, current - amount);
            return true;
        }

        /// <summary>Зручне перевантаження, що додає ресурси до пулу гравця.</summary>
        public void Add(int amount)
        {
            Add(LocalPlayerContext.LocalTeam, amount);
        }

        /// <summary>Додає ресурси до пулу вказаної команди; викидає OnResourceChanged лише для команди гравця.</summary>
        public void Add(TeamType team, int amount)
        {
            if (amount <= 0)
                return;

            SetResource(team, GetResource(team) + amount);
        }

        public int GetResource(TeamType team)
        {
            return _resources.TryGetValue(team, out int value) ? value : 0;
        }

        public void SetResource(TeamType team, int amount)
        {
            int clampedAmount = Mathf.Max(0, amount);
            _resources[team] = clampedAmount;
            OnTeamResourceChanged?.Invoke(team, clampedAmount);

            if (LocalPlayerContext.IsLocalTeam(team))
                OnResourceChanged?.Invoke(clampedAmount);
        }

        public void RestoreResources(IEnumerable<TeamResourceAmount> resources)
        {
            _resources.Clear();

            if (resources != null)
            {
                foreach (TeamResourceAmount resource in resources)
                {
                    if (resource.Team != TeamType.Neutral)
                        _resources[resource.Team] = Mathf.Max(0, resource.Amount);
                }
            }

            RaiseAllResourceEvents();
        }

        public void CopyResources(List<TeamResourceAmount> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (KeyValuePair<TeamType, int> pair in _resources)
                results.Add(new TeamResourceAmount(pair.Key, pair.Value));
        }

        private void InitializeResources()
        {
            _resources.Clear();

            MatchLaunchConfig launchConfig = MatchLaunchContext.CurrentConfig;
            if (launchConfig != null && launchConfig.Teams.Count > 0)
            {
                for (int i = 0; i < launchConfig.Teams.Count; i++)
                {
                    TeamLaunchSlot team = launchConfig.Teams[i];
                    if (team != null && team.Team != TeamType.Neutral)
                        _resources[team.Team] = team.StartingResources;
                }

                RaiseAllResourceEvents();
                return;
            }

            MatchTeamSettings matchSettings = MatchTeamSettings.Active;

            if (matchSettings != null && matchSettings.Teams.Count > 0)
            {
                for (int i = 0; i < matchSettings.Teams.Count; i++)
                {
                    TeamSlot team = matchSettings.Teams[i];

                    if (team.Team == TeamType.Neutral)
                        continue;

                    _resources[team.Team] = ResolveStartingResource(team.Team);
                }
            }
            else
            {
                _resources[TeamType.Player] = Mathf.Max(0, _startResource);
                _resources[TeamType.Enemy] = Mathf.Max(0, _startEnemyResource);
            }

            if (_extraStartingResources == null)
                return;

            for (int i = 0; i < _extraStartingResources.Count; i++)
            {
                TeamResourceAmount resource = _extraStartingResources[i];
                _resources[resource.Team] = Mathf.Max(0, resource.Amount);
            }

            RaiseAllResourceEvents();
        }

        private int ResolveStartingResource(TeamType team)
        {
            if (team == TeamType.Player)
                return Mathf.Max(0, _startResource);

            if (team == TeamType.Enemy)
                return Mathf.Max(0, _startEnemyResource);

            return Mathf.Max(0, _defaultTeamResource);
        }

        private void RaiseAllResourceEvents()
        {
            foreach (KeyValuePair<TeamType, int> pair in _resources)
                OnTeamResourceChanged?.Invoke(pair.Key, pair.Value);

            OnResourceChanged?.Invoke(Resource);
        }
    }

    [Serializable]
    public struct TeamResourceAmount
    {
        [SerializeField] private TeamType _team;
        [SerializeField] private int _amount;

        public TeamType Team => _team;
        public int Amount => _amount;

        public TeamResourceAmount(TeamType team, int amount)
        {
            _team = team;
            _amount = Mathf.Max(0, amount);
        }
    }
}
