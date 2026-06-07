using System.Collections.Generic;
using Strategy.Data;
using Strategy.Units;
using UnityEngine;

namespace Strategy.Buildings
{
    public static class BuildingGridPlacementService
    {
        private const float DefaultCellSize = 5f;

        private static readonly Dictionary<Vector2Int, GameObject> CellOwners = new();
        private static readonly Dictionary<GameObject, List<Vector2Int>> OwnerCells = new();
        private static readonly List<Vector2Int> SharedCells = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            CellOwners.Clear();
            OwnerCells.Clear();
            SharedCells.Clear();
        }

        public static float ResolveCellSize(BuildingPlacementGridConfig config)
        {
            return config != null ? config.CellSize : DefaultCellSize;
        }

        public static Vector2Int WorldToCell(Vector3 worldPosition, BuildingPlacementGridConfig config)
        {
            float cellSize = ResolveCellSize(config);
            Vector3 origin = ResolveOrigin(config);
            return new Vector2Int(
                Mathf.RoundToInt((worldPosition.x - origin.x) / cellSize),
                Mathf.RoundToInt((worldPosition.z - origin.z) / cellSize));
        }

        public static Vector3 CellToWorld(Vector2Int cell, BuildingPlacementGridConfig config)
        {
            float cellSize = ResolveCellSize(config);
            Vector3 origin = ResolveOrigin(config);
            return new Vector3(
                origin.x + cell.x * cellSize,
                origin.y,
                origin.z + cell.y * cellSize);
        }

        public static Vector2Int WorldToPlacementOriginCell(
            BuildingData buildingData,
            Vector3 worldPosition,
            int rotationSteps,
            BuildingPlacementGridConfig config)
        {
            Vector2Int nearestCell = WorldToCell(worldPosition, config);

            if (buildingData == null)
                return nearestCell;

            Vector2Int bestCell = nearestCell;
            float bestDistanceSqr = float.MaxValue;

            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    Vector2Int candidateCell = nearestCell + new Vector2Int(x, y);
                    Vector3 candidatePosition = GetPlacementPosition(buildingData, candidateCell, rotationSteps, config);
                    float distanceSqr = (candidatePosition - worldPosition).sqrMagnitude;

                    if (distanceSqr >= bestDistanceSqr)
                        continue;

                    bestDistanceSqr = distanceSqr;
                    bestCell = candidateCell;
                }
            }

            return bestCell;
        }

        public static Quaternion RotationFromSteps(int rotationSteps)
        {
            return Quaternion.Euler(0f, NormalizeRotationSteps(rotationSteps) * 90f, 0f);
        }

        public static int NormalizeRotationSteps(int rotationSteps)
        {
            int normalized = rotationSteps % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        public static Vector2Int ResolveRotatedFootprint(
            BuildingData buildingData,
            BuildingPlacementGridConfig config,
            int rotationSteps)
        {
            Vector2Int footprint = ResolveFootprint(buildingData, config);

            if (NormalizeRotationSteps(rotationSteps) % 2 == 0)
                return footprint;

            return new Vector2Int(footprint.y, footprint.x);
        }

        public static Vector3 GetPlacementPosition(
            BuildingData buildingData,
            Vector2Int originCell,
            int rotationSteps,
            BuildingPlacementGridConfig config)
        {
            SharedCells.Clear();
            GetOccupiedCells(buildingData, originCell, rotationSteps, config, SharedCells);

            if (SharedCells.Count == 0)
                return CellToWorld(originCell, config);

            Vector3 sum = Vector3.zero;

            foreach (Vector2Int cell in SharedCells)
                sum += CellToWorld(cell, config);

            return sum / SharedCells.Count;
        }

        public static void GetOccupiedCells(
            BuildingData buildingData,
            Vector2Int originCell,
            int rotationSteps,
            BuildingPlacementGridConfig config,
            List<Vector2Int> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();

            if (buildingData == null)
                return;

            Vector2Int footprint = ResolveFootprint(buildingData, config);
            Vector2Int pivotOffset = ResolvePivotOffset(footprint, buildingData.GridPivot);
            int normalizedRotation = NormalizeRotationSteps(rotationSteps);

            for (int x = 0; x < footprint.x; x++)
            {
                for (int y = 0; y < footprint.y; y++)
                {
                    Vector2Int localOffset = new Vector2Int(x, y) - pivotOffset;
                    Vector2Int rotatedOffset = RotateCellOffset(localOffset, normalizedRotation);
                    buffer.Add(originCell + rotatedOffset);
                }
            }
        }

        public static void GetOccupiedCellCenters(
            BuildingData buildingData,
            Vector2Int originCell,
            int rotationSteps,
            BuildingPlacementGridConfig config,
            List<Vector3> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            SharedCells.Clear();
            GetOccupiedCells(buildingData, originCell, rotationSteps, config, SharedCells);

            foreach (Vector2Int cell in SharedCells)
                buffer.Add(CellToWorld(cell, config));
        }

        public static bool CanPlace(
            BuildingData buildingData,
            Vector2Int originCell,
            int rotationSteps,
            BuildingPlacementGridConfig config,
            TeamType team,
            LayerMask blockMask,
            Transform ignoredPreviewRoot,
            List<Vector2Int> occupiedCells,
            List<Vector3> occupiedCellCenters)
        {
            return EvaluatePlacement(
                buildingData,
                originCell,
                rotationSteps,
                config,
                team,
                blockMask,
                ignoredPreviewRoot,
                occupiedCells,
                occupiedCellCenters,
                null);
        }

        public static bool EvaluatePlacement(
            BuildingData buildingData,
            Vector2Int originCell,
            int rotationSteps,
            BuildingPlacementGridConfig config,
            TeamType team,
            LayerMask blockMask,
            Transform ignoredPreviewRoot,
            List<Vector2Int> occupiedCells,
            List<Vector3> occupiedCellCenters,
            List<Vector2Int> invalidCells)
        {
            if (buildingData == null)
                return false;

            invalidCells?.Clear();
            GetOccupiedCells(buildingData, originCell, rotationSteps, config, occupiedCells);
            GetOccupiedCellCenters(buildingData, originCell, rotationSteps, config, occupiedCellCenters);

            if (occupiedCells == null || occupiedCellCenters == null || occupiedCells.Count == 0)
                return false;

            Vector3 placementPosition = GetPlacementPosition(buildingData, originCell, rotationSteps, config);
            Quaternion rotation = RotationFromSteps(rotationSteps);
            Vector3 boxCenter = placementPosition + rotation * buildingData.CheckBoxOffset;
            GameObject ignoredOwner = ignoredPreviewRoot != null ? ignoredPreviewRoot.gameObject : null;
            bool isValidPlacement = true;

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i];
                Vector3 cellCenter = i < occupiedCellCenters.Count
                    ? occupiedCellCenters[i]
                    : CellToWorld(cell, config);

                bool isInvalid =
                    IsReservedByOther(cell, ignoredOwner) ||
                    !IsInsideFriendlyBuildArea(cellCenter, team) ||
                    IsCellBlocked(
                        cellCenter,
                        boxCenter,
                        buildingData,
                        config,
                        blockMask,
                        ignoredPreviewRoot);

                if (isInvalid)
                {
                    isValidPlacement = false;
                    AddUnique(invalidCells, cell);
                }
            }

            return isValidPlacement;
        }

        public static bool IsCellBlocked(
            Vector3 cellCenter,
            BuildingPlacementGridConfig config,
            LayerMask blockMask,
            Transform ignoredRoot = null)
        {
            float cellSize = ResolveCellSize(config);
            float halfHeight = Mathf.Max(4f, cellSize);
            Vector3 center = new Vector3(cellCenter.x, halfHeight, cellCenter.z);
            Vector3 halfExtents = new Vector3(
                Mathf.Max(0.05f, cellSize * 0.48f),
                halfHeight,
                Mathf.Max(0.05f, cellSize * 0.48f));

            return HasBlockingCollider(center, halfExtents, blockMask, ignoredRoot);
        }

        public static bool Reserve(
            BuildingData buildingData,
            Vector2Int originCell,
            int rotationSteps,
            BuildingPlacementGridConfig config,
            GameObject owner)
        {
            if (buildingData == null || owner == null)
                return false;

            Release(owner);

            SharedCells.Clear();
            GetOccupiedCells(buildingData, originCell, rotationSteps, config, SharedCells);

            foreach (Vector2Int cell in SharedCells)
            {
                if (IsReservedByOther(cell, owner))
                    return false;
            }

            List<Vector2Int> reservedCells = new(SharedCells.Count);

            foreach (Vector2Int cell in SharedCells)
            {
                reservedCells.Add(cell);
                CellOwners[cell] = owner;
            }

            OwnerCells[owner] = reservedCells;
            return true;
        }

        public static void Release(GameObject owner)
        {
            if (owner == null || !OwnerCells.TryGetValue(owner, out List<Vector2Int> cells))
                return;

            foreach (Vector2Int cell in cells)
            {
                if (CellOwners.TryGetValue(cell, out GameObject existingOwner) && existingOwner == owner)
                    CellOwners.Remove(cell);
            }

            OwnerCells.Remove(owner);
        }

        public static bool IsReservedByOther(Vector2Int cell, GameObject owner)
        {
            return CellOwners.TryGetValue(cell, out GameObject existingOwner) &&
                   existingOwner != null &&
                   existingOwner != owner;
        }

        private static Vector3 ResolveOrigin(BuildingPlacementGridConfig config)
        {
            return config != null ? config.Origin : Vector3.zero;
        }

        private static Vector2Int ResolveFootprint(BuildingData buildingData, BuildingPlacementGridConfig config)
        {
            return buildingData != null
                ? buildingData.ResolveGridFootprint(ResolveCellSize(config))
                : Vector2Int.one;
        }

        private static Vector2Int ResolvePivotOffset(Vector2Int footprint, BuildingGridPivot pivot)
        {
            int left = 0;
            int centerX = footprint.x / 2;
            int right = Mathf.Max(0, footprint.x - 1);
            int bottom = 0;
            int centerY = footprint.y / 2;
            int top = Mathf.Max(0, footprint.y - 1);

            return pivot switch
            {
                BuildingGridPivot.BottomLeft => new Vector2Int(left, bottom),
                BuildingGridPivot.BottomCenter => new Vector2Int(centerX, bottom),
                BuildingGridPivot.BottomRight => new Vector2Int(right, bottom),
                BuildingGridPivot.CenterLeft => new Vector2Int(left, centerY),
                BuildingGridPivot.CenterRight => new Vector2Int(right, centerY),
                BuildingGridPivot.TopLeft => new Vector2Int(left, top),
                BuildingGridPivot.TopCenter => new Vector2Int(centerX, top),
                BuildingGridPivot.TopRight => new Vector2Int(right, top),
                _ => new Vector2Int(centerX, centerY)
            };
        }

        private static Vector2Int RotateCellOffset(Vector2Int offset, int rotationSteps)
        {
            return rotationSteps switch
            {
                1 => new Vector2Int(offset.y, -offset.x),
                2 => new Vector2Int(-offset.x, -offset.y),
                3 => new Vector2Int(-offset.y, offset.x),
                _ => offset
            };
        }

        private static bool IsCellBlocked(
            Vector3 cellCenter,
            Vector3 buildingBoxCenter,
            BuildingData buildingData,
            BuildingPlacementGridConfig config,
            LayerMask blockMask,
            Transform ignoredPreviewRoot)
        {
            float cellSize = ResolveCellSize(config);
            float halfHeight = Mathf.Max(0.05f, Mathf.Abs(buildingData.CheckBoxSize.y) * 0.5f);
            Vector3 center = new Vector3(cellCenter.x, buildingBoxCenter.y, cellCenter.z);
            Vector3 halfExtents = new Vector3(
                Mathf.Max(0.05f, cellSize * 0.48f),
                halfHeight,
                Mathf.Max(0.05f, cellSize * 0.48f));

            return HasBlockingCollider(center, halfExtents, blockMask, ignoredPreviewRoot);
        }

        private static bool HasBlockingCollider(
            Vector3 center,
            Vector3 halfExtents,
            LayerMask blockMask,
            Transform ignoredRoot)
        {
            Collider[] hits = Physics.OverlapBox(
                center,
                halfExtents,
                Quaternion.identity,
                blockMask,
                QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                if (ignoredRoot != null && hit.transform.IsChildOf(ignoredRoot))
                    continue;

                return true;
            }

            return false;
        }

        private static void AddUnique(List<Vector2Int> cells, Vector2Int cell)
        {
            if (cells == null || cells.Contains(cell))
                return;

            cells.Add(cell);
        }

        private static bool IsInsideFriendlyBuildArea(Vector3 position, TeamType team)
        {
            foreach (ConstructionCenter center in ConstructionCenter.All)
            {
                if (center == null || !center.isActiveAndEnabled)
                    continue;

                TeamComponent teamComponent = center.GetComponentInParent<TeamComponent>();

                if (teamComponent != null && teamComponent.Team != team)
                    continue;

                if (center.IsInsideBuildArea(position))
                    return true;
            }

            return false;
        }
    }
}
