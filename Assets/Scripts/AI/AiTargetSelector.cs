using Strategy.Buildings;
using Strategy.Core;
using Strategy.Units;
using UnityEngine;

namespace Strategy.AI
{
    public static class AiTargetSelector
    {
        public static BuildingHealth FindBestBuildingTarget(TeamType sourceTeam, bool focusHighValueBuildings)
        {
            BuildingHealth bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < BuildingHealth.All.Count; i++)
            {
                BuildingHealth candidate = BuildingHealth.All[i];
                if (candidate == null || candidate.IsDead || candidate.GetComponentInParent<Outpost>() != null)
                    continue;

                TeamComponent targetTeam = candidate.GetComponentInParent<TeamComponent>();
                if (targetTeam == null || !TeamRelations.AreHostile(sourceTeam, targetTeam.Team))
                    continue;

                float score = ResolveBuildingScore(candidate, focusHighValueBuildings);
                if (score <= bestScore)
                    continue;

                bestTarget = candidate;
                bestScore = score;
            }

            return bestTarget;
        }

        private static float ResolveBuildingScore(BuildingHealth building, bool focusHighValueBuildings)
        {
            float lowHealthBonus = 1f - building.NormalizedHealth;
            float value = building.MaxHealth * 0.01f;

            if (building.GetComponent<ConstructionCenter>() != null)
                value += focusHighValueBuildings ? 100f : 35f;

            if (building.GetComponent<BuildingProduction>() != null)
                value += focusHighValueBuildings ? 65f : 25f;

            return value + lowHealthBonus * 30f;
        }
    }
}
