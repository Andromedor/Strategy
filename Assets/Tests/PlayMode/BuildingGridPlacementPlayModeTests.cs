using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Strategy.Tests
{
    public class BuildingGridPlacementPlayModeTests
    {
        private const string BuildingDataTypeName = "Strategy.Data.BuildingData, Assembly-CSharp";
        private const string BuildingPlacementGridConfigTypeName = "Strategy.Data.BuildingPlacementGridConfig, Assembly-CSharp";
        private const string BuildingGridPlacementServiceTypeName = "Strategy.Buildings.BuildingGridPlacementService, Assembly-CSharp";
        private const string BuildingGridOccupancyTypeName = "Strategy.Buildings.BuildingGridOccupancy, Assembly-CSharp";
        private const string ConstructionCenterTypeName = "Strategy.Buildings.ConstructionCenter, Assembly-CSharp";
        private const string TeamTypeName = "Strategy.Units.TeamType, Assembly-CSharp";

        private readonly Type _buildingDataType = Type.GetType(BuildingDataTypeName);
        private readonly Type _gridConfigType = Type.GetType(BuildingPlacementGridConfigTypeName);
        private readonly Type _gridServiceType = Type.GetType(BuildingGridPlacementServiceTypeName);
        private readonly Type _gridOccupancyType = Type.GetType(BuildingGridOccupancyTypeName);
        private readonly Type _constructionCenterType = Type.GetType(ConstructionCenterTypeName);
        private readonly Type _teamType = Type.GetType(TeamTypeName);

        [SetUp]
        public void SetUp()
        {
            Assert.NotNull(_buildingDataType);
            Assert.NotNull(_gridConfigType);
            Assert.NotNull(_gridServiceType);
            Assert.NotNull(_gridOccupancyType);
            Assert.NotNull(_constructionCenterType);
            Assert.NotNull(_teamType);
        }

        [Test]
        public void RotatingFactoryFootprintSwapsCellDimensions()
        {
            ScriptableObject buildingData = CreateBuildingData(new Vector2Int(2, 3), Vector3.one, Vector3.zero);
            ScriptableObject gridConfig = CreateGridConfig(5f);

            try
            {
                Vector2Int zeroRotation = InvokeStatic<Vector2Int>(
                    "ResolveRotatedFootprint",
                    buildingData,
                    gridConfig,
                    0);
                Vector2Int quarterTurn = InvokeStatic<Vector2Int>(
                    "ResolveRotatedFootprint",
                    buildingData,
                    gridConfig,
                    1);

                Assert.AreEqual(new Vector2Int(2, 3), zeroRotation);
                Assert.AreEqual(new Vector2Int(3, 2), quarterTurn);

                List<Vector2Int> cells = new();
                InvokeStaticVoid(
                    "GetOccupiedCells",
                    buildingData,
                    Vector2Int.zero,
                    1,
                    gridConfig,
                    cells);

                Assert.AreEqual(6, cells.Count);
                Assert.AreEqual(6, new HashSet<Vector2Int>(cells).Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buildingData);
                UnityEngine.Object.DestroyImmediate(gridConfig);
            }
        }

        [Test]
        public void PlacementFailsWhenAnyFootprintCellLeavesBuildRadius()
        {
            ScriptableObject buildingData = CreateBuildingData(new Vector2Int(2, 2), Vector3.one, Vector3.zero);
            ScriptableObject gridConfig = CreateGridConfig(5f);
            GameObject centerObject = new("Construction Center");
            centerObject.AddComponent(_constructionCenterType);
            SetField(centerObject.GetComponent(_constructionCenterType), "_buildRadius", 7.1f);

            try
            {
                Assert.IsTrue(CanPlace(buildingData, gridConfig, Vector2Int.zero));
                Assert.IsFalse(CanPlace(buildingData, gridConfig, new Vector2Int(3, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(centerObject);
                UnityEngine.Object.DestroyImmediate(buildingData);
                UnityEngine.Object.DestroyImmediate(gridConfig);
            }
        }

        [Test]
        public void ReservedCellsBlockOtherOwnersUntilReleased()
        {
            ScriptableObject buildingData = CreateBuildingData(new Vector2Int(2, 2), Vector3.one, Vector3.zero);
            ScriptableObject gridConfig = CreateGridConfig(5f);
            GameObject firstOwner = new("First Building");
            GameObject secondOwner = new("Second Building");

            try
            {
                Assert.IsTrue(Reserve(buildingData, gridConfig, Vector2Int.zero, firstOwner));
                Assert.IsTrue(IsReservedByOther(Vector2Int.zero, secondOwner));
                Assert.IsFalse(Reserve(buildingData, gridConfig, Vector2Int.zero, secondOwner));

                Release(firstOwner);

                Assert.IsTrue(Reserve(buildingData, gridConfig, Vector2Int.zero, secondOwner));
            }
            finally
            {
                Release(firstOwner);
                Release(secondOwner);
                UnityEngine.Object.DestroyImmediate(firstOwner);
                UnityEngine.Object.DestroyImmediate(secondOwner);
                UnityEngine.Object.DestroyImmediate(buildingData);
                UnityEngine.Object.DestroyImmediate(gridConfig);
            }
        }

        [Test]
        public void TriggerBuildingColliderMarksOnlyBlockedFootprintCellInvalid()
        {
            ScriptableObject buildingData = CreateBuildingData(new Vector2Int(2, 2), new Vector3(10f, 6f, 10f), new Vector3(0f, 3f, 0f));
            ScriptableObject gridConfig = CreateGridConfig(5f);
            GameObject centerObject = new("Construction Center");
            centerObject.AddComponent(_constructionCenterType);
            SetField(centerObject.GetComponent(_constructionCenterType), "_buildRadius", 50f);
            GameObject blocker = new("MilitaryBase Trigger Blocker");
            blocker.layer = LayerMask.NameToLayer("Building");
            BoxCollider blockerCollider = blocker.AddComponent<BoxCollider>();
            blockerCollider.isTrigger = true;
            blockerCollider.size = Vector3.one;

            try
            {
                List<Vector2Int> cells = new();
                List<Vector3> centers = new();
                List<Vector2Int> invalidCells = new();
                object team = Enum.ToObject(_teamType, 0);
                LayerMask blockMask = 1 << LayerMask.NameToLayer("Building");

                bool canPlace = InvokeStatic<bool>(
                    "EvaluatePlacement",
                    buildingData,
                    Vector2Int.zero,
                    0,
                    gridConfig,
                    team,
                    blockMask,
                    null,
                    cells,
                    centers,
                    invalidCells);

                Assert.IsFalse(canPlace);
                Assert.AreEqual(1, invalidCells.Count);
                Assert.AreEqual(Vector2Int.zero, invalidCells[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blocker);
                UnityEngine.Object.DestroyImmediate(centerObject);
                UnityEngine.Object.DestroyImmediate(buildingData);
                UnityEngine.Object.DestroyImmediate(gridConfig);
            }
        }

        [Test]
        public void SceneBuildingOccupancySnapsMisalignedTransformBeforeReservation()
        {
            ScriptableObject buildingData = CreateBuildingData(new Vector2Int(3, 3), new Vector3(15f, 8f, 15f), new Vector3(0f, 4f, 0f));
            ScriptableObject gridConfig = CreateGridConfig(5f);
            GameObject building = new("Scene Military Base");
            GameObject otherOwner = new("Other Owner");
            building.SetActive(false);
            Component occupancy = building.AddComponent(_gridOccupancyType);
            building.transform.position = new Vector3(-29.4f, 0.25f, -16.8f);
            SetField(occupancy, "_buildingData", buildingData);
            SetField(occupancy, "_gridConfig", gridConfig);
            SetField(occupancy, "_reserveOnEnable", true);
            SetField(occupancy, "_snapTransformToGridOnEnable", true);

            try
            {
                building.SetActive(true);

                Assert.AreEqual(-30f, building.transform.position.x, 0.001f);
                Assert.AreEqual(0.25f, building.transform.position.y, 0.001f);
                Assert.AreEqual(-15f, building.transform.position.z, 0.001f);
                Assert.IsTrue(IsReservedByOther(new Vector2Int(-6, -3), otherOwner));
            }
            finally
            {
                Release(building);
                UnityEngine.Object.DestroyImmediate(otherOwner);
                UnityEngine.Object.DestroyImmediate(building);
                UnityEngine.Object.DestroyImmediate(buildingData);
                UnityEngine.Object.DestroyImmediate(gridConfig);
            }
        }

        private ScriptableObject CreateBuildingData(Vector2Int footprint, Vector3 checkBoxSize, Vector3 checkBoxOffset)
        {
            ScriptableObject buildingData = ScriptableObject.CreateInstance(_buildingDataType);
            SetField(buildingData, "_gridFootprintCells", footprint);
            SetField(buildingData, "_checkBoxSize", checkBoxSize);
            SetField(buildingData, "_checkBoxOffset", checkBoxOffset);
            return buildingData;
        }

        private ScriptableObject CreateGridConfig(float cellSize)
        {
            ScriptableObject gridConfig = ScriptableObject.CreateInstance(_gridConfigType);
            SetField(gridConfig, "_cellSize", cellSize);
            return gridConfig;
        }

        private bool CanPlace(ScriptableObject buildingData, ScriptableObject gridConfig, Vector2Int originCell)
        {
            List<Vector2Int> cells = new();
            List<Vector3> centers = new();
            object team = Enum.ToObject(_teamType, 0);
            LayerMask blockMask = default;

            return InvokeStatic<bool>(
                "CanPlace",
                buildingData,
                originCell,
                0,
                gridConfig,
                team,
                blockMask,
                null,
                cells,
                centers);
        }

        private bool Reserve(ScriptableObject buildingData, ScriptableObject gridConfig, Vector2Int originCell, GameObject owner)
        {
            return InvokeStatic<bool>("Reserve", buildingData, originCell, 0, gridConfig, owner);
        }

        private bool IsReservedByOther(Vector2Int cell, GameObject owner)
        {
            return InvokeStatic<bool>("IsReservedByOther", cell, owner);
        }

        private void Release(GameObject owner)
        {
            InvokeStaticVoid("Release", owner);
        }

        private T InvokeStatic<T>(string methodName, params object[] args)
        {
            MethodInfo method = _gridServiceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            return (T)method.Invoke(null, args);
        }

        private void InvokeStaticVoid(string methodName, params object[] args)
        {
            MethodInfo method = _gridServiceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            method.Invoke(null, args);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
