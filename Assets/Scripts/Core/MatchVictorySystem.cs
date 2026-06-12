using System;
using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    [DisallowMultipleComponent]
    public class MatchVictorySystem : MonoBehaviour, ISimulationTickable
    {
        public static event Action<MatchResult> MatchEnded;

        [SerializeField, Min(0f)] private float _startEvaluationDelay = 1.5f;
        [SerializeField, Min(0.1f)] private float _evaluationInterval = 0.5f;

        private readonly HashSet<TeamType> _expectedTeams = new();
        private readonly HashSet<TeamType> _aliveTeams = new();
        private bool _isEnded;
        private bool _hasSeenCompleteStartingState;
        private float _startTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            MatchEnded = null;
        }

        private void OnEnable()
        {
            _startTime = Time.time;
            GameTickRunner.Register(this, _evaluationInterval);
            EventManager.OnBuildingDestroyed += OnBuildingDestroyed;
            MatchStartSpawner.StartingBasesSpawnCompleted += OnStartingBasesSpawnCompleted;
        }

        private void OnDisable()
        {
            GameTickRunner.Unregister(this);
            EventManager.OnBuildingDestroyed -= OnBuildingDestroyed;
            MatchStartSpawner.StartingBasesSpawnCompleted -= OnStartingBasesSpawnCompleted;
        }

        public void Tick(GameTickContext context)
        {
            Evaluate();
        }

        /// <summary>Перевіряє, чи одна зі сторін втратила всі бойові будівлі, але тільки після повного стартового спавну матчу.</summary>
        public void Evaluate()
        {
            if (_isEnded || Time.time < _startTime + _startEvaluationDelay)
                return;

            BuildExpectedTeams();
            if (_expectedTeams.Count <= 1)
                return;

            BuildAliveTeams();

            if (!_hasSeenCompleteStartingState)
            {
                // Перемогу не можна рахувати, доки всі активні команди ще не отримали стартові будівлі.
                // Інакше перший тік після завантаження може побачити лише базу гравця і хибно завершити матч.
                if (!HaveAllExpectedTeamsSpawned())
                    return;

                _hasSeenCompleteStartingState = true;
            }

            if (_aliveTeams.Count == 0)
                return;

            TeamType localTeam = LocalPlayerContext.LocalTeam;
            bool localAlive = false;
            bool hostileAlive = false;

            foreach (TeamType team in _expectedTeams)
            {
                bool isAlive = _aliveTeams.Contains(team);

                if (isAlive && TeamRelations.AreAllied(localTeam, team))
                    localAlive = true;
                else if (isAlive && TeamRelations.AreHostile(localTeam, team))
                    hostileAlive = true;
            }

            if (!localAlive)
                EndMatch(new MatchResult(MatchResultKind.Defeat, localTeam));
            else if (!hostileAlive)
                EndMatch(new MatchResult(MatchResultKind.Victory, localTeam));
        }

        private void OnBuildingDestroyed(GameObject building)
        {
            Evaluate();
        }

        private void OnStartingBasesSpawnCompleted(IReadOnlyList<GameObject> spawnedBases)
        {
            // Спавнер створює бази синхронно; цей сигнал фіксує, що victory-перевірки вже можуть
            // відрізняти реальне знищення команди від тимчасово неповного стартового стану сцени.
            BuildExpectedTeams();
            BuildAliveTeams();
            _hasSeenCompleteStartingState = _expectedTeams.Count > 1 && HaveAllExpectedTeamsSpawned();
        }

        /// <summary>Будує список команд, які мають брати участь у матчі згідно з налаштуваннями запуску.</summary>
        private void BuildExpectedTeams()
        {
            _expectedTeams.Clear();

            MatchTeamSettings matchSettings = MatchTeamSettings.Active;
            IReadOnlyList<TeamSlot> teams = matchSettings != null ? matchSettings.Teams : null;

            if (teams != null)
            {
                for (int i = 0; i < teams.Count; i++)
                {
                    TeamType team = teams[i].Team;
                    if (team != TeamType.Neutral)
                        _expectedTeams.Add(team);
                }
            }

            if (_expectedTeams.Count > 0)
                return;

            // Fallback для тестових сцен без MatchTeamSettings: тоді очікувані команди беремо з живих будівель.
            BuildAliveTeams();
            foreach (TeamType team in _aliveTeams)
                _expectedTeams.Add(team);
        }

        /// <summary>Повертає true, коли кожна активна команда вже має хоча б одну живу non-Outpost будівлю.</summary>
        private bool HaveAllExpectedTeamsSpawned()
        {
            foreach (TeamType team in _expectedTeams)
            {
                if (!_aliveTeams.Contains(team))
                    return false;
            }

            return true;
        }

        /// <summary>Сканує живі будівлі на сцені та збирає команди, які ще не вибиті з матчу.</summary>
        private void BuildAliveTeams()
        {
            _aliveTeams.Clear();

            for (int i = 0; i < BuildingHealth.All.Count; i++)
            {
                BuildingHealth building = BuildingHealth.All[i];
                if (building == null || building.IsDead || building.GetComponentInParent<Outpost>() != null)
                    continue;

                TeamComponent team = building.GetComponentInParent<TeamComponent>();
                if (team == null || team.Team == TeamType.Neutral)
                    continue;

                _aliveTeams.Add(team.Team);
            }
        }

        /// <summary>Завершує матч один раз і повідомляє UI/інші системи про результат.</summary>
        private void EndMatch(MatchResult result)
        {
            if (_isEnded)
                return;

            _isEnded = true;
            MatchEnded?.Invoke(result);
        }
    }
}
