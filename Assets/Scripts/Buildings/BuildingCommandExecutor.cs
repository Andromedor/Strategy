using System.Collections.Generic;
using Strategy.Core;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Buildings
{
    public static class BuildingCommandExecutor
    {
        private static readonly List<Vector2Int> OccupiedCells = new();
        private static readonly List<Vector3> OccupiedCellCenters = new();

        public static bool TryExecuteBuildStructure(
            PlayerCommand command,
            BuildingPlacementGridConfig gridConfig,
            LayerMask blockMask,
            int searchRadiusCells,
            out GameObject building)
        {
            building = null;

            if (command.BuildingData == null || command.BuildingData.Prefab == null)
                return false;

            Vector2Int preferredCell = BuildingGridPlacementService.WorldToCell(command.TargetPosition, gridConfig);
            int rotationSteps = 0;

            if (!TryFindValidCell(
                    command.BuildingData,
                    preferredCell,
                    rotationSteps,
                    gridConfig,
                    command.Team,
                    blockMask,
                    searchRadiusCells,
                    out Vector2Int targetCell))
            {
                return false;
            }

            if (ResourceManager.Instance != null &&
                !ResourceManager.Instance.Spend(command.Team, command.BuildingData.EconomyCost))
            {
                return false;
            }

            Vector3 position = BuildingGridPlacementService.GetPlacementPosition(
                command.BuildingData,
                targetCell,
                rotationSteps,
                gridConfig);
            position.y = command.TargetPosition.y;

            building = Object.Instantiate(
                command.BuildingData.Prefab,
                position,
                BuildingGridPlacementService.RotationFromSteps(rotationSteps),
                RuntimeObjectContainer.Get("Buildings"));
            building.name = $"{command.BuildingData.BuildingName} ({command.Team})";

            TeamObjectSetup.AssignTeam(building, command.Team);

            BuildingGridOccupancy occupancy = building.GetComponent<BuildingGridOccupancy>();
            if (occupancy == null)
                occupancy = building.AddComponent<BuildingGridOccupancy>();

            occupancy.Initialize(command.BuildingData, gridConfig, targetCell, rotationSteps);
            BeginConstruction(building, command.BuildingData);
            return true;
        }

        private static void BeginConstruction(GameObject building, BuildingData buildingData)
        {
            if (building == null)
                return;

            BuildingConstructionState construction = building.GetComponent<BuildingConstructionState>();
            if (construction == null)
                construction = building.AddComponent<BuildingConstructionState>();

            construction.Begin(buildingData);
        }

        private static bool TryFindValidCell(
            BuildingData buildingData,
            Vector2Int preferredCell,
            int rotationSteps,
            BuildingPlacementGridConfig gridConfig,
            TeamType team,
            LayerMask blockMask,
            int searchRadiusCells,
            out Vector2Int targetCell)
        {
            targetCell = preferredCell;
            int radiusLimit = Mathf.Max(0, searchRadiusCells);

            for (int radius = 0; radius <= radiusLimit; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                            continue;

                        Vector2Int candidate = preferredCell + new Vector2Int(x, y);

                        if (BuildingGridPlacementService.CanPlace(
                                buildingData,
                                candidate,
                                rotationSteps,
                                gridConfig,
                                team,
                                blockMask,
                                null,
                                OccupiedCells,
                                OccupiedCellCenters))
                        {
                            targetCell = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
