using Strategy.Units;
using UnityEngine;

namespace Strategy.Core
{
    public static class TeamObjectSetup
    {
        public static void AssignTeam(GameObject root, TeamType team)
        {
            if (root == null)
                return;

            TeamComponent teamComponent = root.GetComponent<TeamComponent>();
            if (teamComponent == null)
                teamComponent = root.AddComponent<TeamComponent>();

            teamComponent.SetTeam(team);
        }

        public static void AssignUnitLayer(GameObject root, TeamType team)
        {
            int layer = LayerMask.NameToLayer(
                LocalPlayerContext.IsLocalTeam(team) ? "PlayerUnit" : "EnemyUnit");

            if (layer >= 0)
                SetLayerRecursively(root.transform, layer);
        }

        public static void SetLayerRecursively(Transform target, int layer)
        {
            if (target == null || layer < 0)
                return;

            target.gameObject.layer = layer;

            foreach (Transform child in target)
                SetLayerRecursively(child, layer);
        }
    }
}
