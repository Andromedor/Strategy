using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Strategy.Tests
{
    /// <summary>
    /// PlayMode regression-тести для factory rally, queued move, traffic-yield і очищення command arrow.
    /// Вони використовують runtime NavMesh, бо ці сценарії залежать від реального lifecycle NavMeshAgent, корутин і LineRenderer.
    /// </summary>
    public class FactoryRallyPlayModeTests
    {
        private const string BuildingProductionTypeName = "Strategy.Buildings.BuildingProduction, Assembly-CSharp";
        private const string UnitSpawnActivatorTypeName = "Strategy.Buildings.UnitSpawnActivator, Assembly-CSharp";
        private const string UnitDestinationReservationsTypeName = "Strategy.Units.UnitDestinationReservations, Assembly-CSharp";
        private const string UnitTrafficCoordinatorTypeName = "Strategy.Units.UnitTrafficCoordinator, Assembly-CSharp";
        private const string UnitCommandArrowManagerTypeName = "Strategy.Units.UnitCommandArrowManager, Assembly-CSharp";
        private const string UnitCommandControllerTypeName = "Strategy.Units.UnitCommandController, Assembly-CSharp";
        private const string UnitControlGroupControllerTypeName = "Strategy.Units.UnitControlGroupController, Assembly-CSharp";
        private const string UnitSelectionStateTypeName = "Strategy.Units.UnitSelectionState, Assembly-CSharp";
        private const string SelectionInfoPanelUiTypeName = "Strategy.UI.SelectionInfoPanelUI, Assembly-CSharp";
        private const string EventManagerTypeName = "Strategy.Core.EventManager, Assembly-CSharp";

        private readonly Type _buildingProductionType = Type.GetType(BuildingProductionTypeName);
        private readonly Type _unitSpawnActivatorType = Type.GetType(UnitSpawnActivatorTypeName);
        private readonly Type _unitDestinationReservationsType = Type.GetType(UnitDestinationReservationsTypeName);
        private readonly Type _unitTrafficCoordinatorType = Type.GetType(UnitTrafficCoordinatorTypeName);
        private readonly Type _unitCommandArrowManagerType = Type.GetType(UnitCommandArrowManagerTypeName);
        private readonly Type _unitCommandControllerType = Type.GetType(UnitCommandControllerTypeName);
        private readonly Type _unitControlGroupControllerType = Type.GetType(UnitControlGroupControllerTypeName);
        private readonly Type _unitSelectionStateType = Type.GetType(UnitSelectionStateTypeName);
        private readonly Type _selectionInfoPanelUiType = Type.GetType(SelectionInfoPanelUiTypeName);
        private readonly Type _eventManagerType = Type.GetType(EventManagerTypeName);

        private GameObject _navMeshRoot;
        private GameObject _factoryObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.NotNull(_buildingProductionType);
            Assert.NotNull(_unitSpawnActivatorType);
            Assert.NotNull(_unitDestinationReservationsType);
            Assert.NotNull(_unitTrafficCoordinatorType);
            Assert.NotNull(_unitCommandArrowManagerType);
            Assert.NotNull(_unitCommandControllerType);
            Assert.NotNull(_unitControlGroupControllerType);
            Assert.NotNull(_unitSelectionStateType);
            Assert.NotNull(_selectionInfoPanelUiType);
            Assert.NotNull(_eventManagerType);

            _navMeshRoot = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _navMeshRoot.name = "Runtime Test NavMesh";
            _navMeshRoot.transform.localScale = new Vector3(8f, 1f, 8f);

            NavMeshSurface surface = _navMeshRoot.AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NavMesh.RemoveAllNavMeshData();

            if (_factoryObject != null)
                UnityEngine.Object.Destroy(_factoryObject);

            if (_navMeshRoot != null)
                UnityEngine.Object.Destroy(_navMeshRoot);

            yield return null;
        }

        [UnityTest]
        public IEnumerator RallyPointAssignsSeparatedLineSlots()
        {
            Component factory = CreateFactory(Vector3.zero, new Vector3(20f, 0f, 0f));
            GameObject firstUnit = CreateUnit(new Vector3(0f, 0f, 0f));
            GameObject secondUnit = CreateUnit(new Vector3(0f, 0f, 0f));

            Vector3 firstDestination = InvokeVector3(factory, "ResolveRallyDestination", firstUnit);
            Vector3 secondDestination = InvokeVector3(factory, "ResolveRallyDestination", secondUnit);

            float distance = Vector3.Distance(firstDestination, secondDestination);

            Assert.GreaterOrEqual(distance, 5f);

            UnityEngine.Object.Destroy(firstUnit);
            UnityEngine.Object.Destroy(secondUnit);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RallyPointSkipsGloballyReservedDestinationWhenBlockerMaskIsEmpty()
        {
            Component factory = CreateFactory(Vector3.zero, new Vector3(20f, 0f, 0f));
            SetField(factory, "_rallyBlockerMask", default(LayerMask));

            GameObject reservedOwner = CreateUnit(new Vector3(30f, 0f, 0f));
            GameObject newUnit = CreateUnit(Vector3.zero);
            InvokeStaticVoid(
                _unitDestinationReservationsType,
                "Reserve",
                reservedOwner,
                new Vector3(20f, 0f, 0f),
                3.1f);

            Vector3 destination = InvokeVector3(factory, "ResolveRallyDestination", newUnit);

            Assert.GreaterOrEqual(Vector3.Distance(destination, new Vector3(20f, 0f, 0f)), 5f);

            InvokeStaticVoid(_unitDestinationReservationsType, "Release", reservedOwner);
            UnityEngine.Object.Destroy(reservedOwner);
            UnityEngine.Object.Destroy(newUnit);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QueuedMoveDuringFactoryExitReservesDestinationImmediately()
        {
            GameObject unit = CreateUnit(Vector3.zero);
            GameObject otherUnit = CreateUnit(new Vector3(30f, 0f, 0f));
            Component activator = unit.GetComponent(_unitSpawnActivatorType);
            Vector3 queuedDestination = new Vector3(24f, 0f, 0f);

            InvokeVoid(activator, "SetSpawningState", true);
            InvokeVoid(activator, "QueueMoveAfterSpawn", queuedDestination);

            bool isReserved = InvokeStaticBool(
                _unitDestinationReservationsType,
                "IsReservedByOther",
                otherUnit,
                queuedDestination,
                3.1f);

            Assert.IsTrue(isReserved);

            InvokeStaticVoid(_unitDestinationReservationsType, "Release", unit);
            UnityEngine.Object.Destroy(unit);
            UnityEngine.Object.Destroy(otherUnit);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IdleUnitYieldsFromFactoryExitCorridor()
        {
            GameObject requester = CreateUnit(Vector3.zero);
            GameObject blocker = CreateUnit(new Vector3(4f, 0f, 0f));
            NavMeshAgent blockerAgent = blocker.GetComponent<NavMeshAgent>();

            yield return null;

            bool blocked = InvokeStaticBool(
                _unitTrafficCoordinatorType,
                "RequestYieldForCorridor",
                requester,
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                3.2f);

            yield return null;

            Assert.IsTrue(blocked);
            Assert.IsTrue(blockerAgent.hasPath);
            Assert.Greater(
                DistancePointToSegment2D(blockerAgent.destination, Vector3.zero, new Vector3(10f, 0f, 0f)),
                3.2f);

            UnityEngine.Object.Destroy(requester);
            UnityEngine.Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator MovingUnitRequestsIdleBlockerToYield()
        {
            GameObject mover = CreateUnit(Vector3.zero);
            GameObject blocker = CreateUnit(new Vector3(5f, 0f, 0f));
            NavMeshAgent moverAgent = mover.GetComponent<NavMeshAgent>();
            NavMeshAgent blockerAgent = blocker.GetComponent<NavMeshAgent>();

            moverAgent.SetDestination(new Vector3(14f, 0f, 0f));

            float timeout = Time.time + 1.2f;
            while (Time.time < timeout && !blockerAgent.hasPath)
                yield return null;

            Assert.IsTrue(blockerAgent.hasPath);
            Assert.Greater(
                DistancePointToSegment2D(blockerAgent.destination, Vector3.zero, new Vector3(14f, 0f, 0f)),
                3.2f);

            UnityEngine.Object.Destroy(mover);
            UnityEngine.Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator MoveCommandArrowClearsAfterArrival()
        {
            GameObject managerObject = new GameObject("Command Arrow Manager Test");
            Component manager = managerObject.AddComponent(_unitCommandArrowManagerType);
            Material lineMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
            SetField(manager, "_moveLineMaterial", lineMaterial);

            GameObject unit = CreateUnit(Vector3.zero);
            unit.AddComponent(_unitSelectionStateType);
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            Vector3 destination = new Vector3(10f, 0f, 0f);

            InvokeStaticVoid(_eventManagerType, "RaiseUnitSelected", unit);
            Assert.IsTrue(agent.SetDestination(destination));
            InvokeStaticVoid(_eventManagerType, "RaiseUnitMoveCommand", unit, destination);

            yield return null;

            Assert.NotNull(FindCommandLine(unit));

            agent.Warp(destination);
            agent.ResetPath();

            yield return null;
            yield return null;

            Assert.IsNull(FindCommandLine(unit));

            UnityEngine.Object.Destroy(lineMaterial);
            UnityEngine.Object.Destroy(unit);
            UnityEngine.Object.Destroy(managerObject);
        }

        [UnityTest]
        public IEnumerator PlayerMoveCommandOverridesRallyAfterFactoryExit()
        {
            GameObject unit = CreateUnit(Vector3.zero);
            Component activator = unit.GetComponent(_unitSpawnActivatorType);
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();

            InvokeVoid(activator, "SetSpawningState", true);
            InvokeVoid(activator, "QueueMoveAfterSpawn", new Vector3(24f, 0f, 0f));

            bool releasedRallyReservation = false;
            IEnumerator moveRoutine = (IEnumerator)_unitSpawnActivatorType
                .GetMethod(
                    "MoveOutOfFactory",
                    new[] { typeof(Vector3), typeof(Vector3), typeof(Action<Vector3>) })
                .Invoke(
                    activator,
                    new object[]
                    {
                        new Vector3(4f, 0f, 0f),
                        new Vector3(10f, 0f, 0f),
                        new Action<Vector3>(_ => releasedRallyReservation = true)
                    });

            yield return moveRoutine;
            yield return null;

            Assert.IsTrue(releasedRallyReservation);
            Assert.IsTrue(agent.enabled);
            Assert.IsTrue(agent.isOnNavMesh);
            Assert.Less(Vector3.Distance(agent.destination, new Vector3(24f, 0f, 0f)), 1.5f);

            UnityEngine.Object.Destroy(unit);
        }

        [UnityTest]
        public IEnumerator ControlGroupRecallRestoresSavedSelectionAndPrunesDestroyedUnits()
        {
            GameObject cameraObject = new GameObject("Control Group Camera");
            cameraObject.AddComponent<Camera>();
            Component selectionController = cameraObject.AddComponent(_unitCommandControllerType);
            Component controlGroups = cameraObject.AddComponent(_unitControlGroupControllerType);
            GameObject firstUnit = CreateUnit(new Vector3(0f, 0f, 0f));
            GameObject secondUnit = CreateUnit(new Vector3(5f, 0f, 0f));
            List<GameObject> selected = new List<GameObject>();

            InvokeVoid(selectionController, "SelectUnits", new List<GameObject> { firstUnit, secondUnit });
            InvokeVoid(controlGroups, "SaveGroup", 1);
            InvokeVoid(selectionController, "ClearSelection");

            InvokeVoid(selectionController, "CopySelectedUnits", selected);
            Assert.Zero(selected.Count);

            InvokeVoid(controlGroups, "RecallGroup", 1);
            InvokeVoid(selectionController, "CopySelectedUnits", selected);
            Assert.AreEqual(2, selected.Count);
            Assert.Contains(firstUnit, selected);
            Assert.Contains(secondUnit, selected);

            InvokeStaticVoid(_eventManagerType, "RaiseUnitDestroyed", firstUnit);
            UnityEngine.Object.Destroy(firstUnit);
            yield return null;

            InvokeVoid(selectionController, "ClearSelection");
            InvokeVoid(controlGroups, "RecallGroup", 1);
            InvokeVoid(selectionController, "CopySelectedUnits", selected);
            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(secondUnit, selected[0]);

            UnityEngine.Object.Destroy(secondUnit);
            UnityEngine.Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator ControlGroupHotkeyFlowRestoresSelectionAfterDeselect()
        {
            GameObject cameraObject = new GameObject("Control Group Hotkey Camera");
            cameraObject.AddComponent<Camera>();
            Component selectionController = cameraObject.AddComponent(_unitCommandControllerType);
            Component controlGroups = cameraObject.AddComponent(_unitControlGroupControllerType);
            GameObject firstUnit = CreateUnit(new Vector3(0f, 0f, 0f));
            GameObject secondUnit = CreateUnit(new Vector3(5f, 0f, 0f));
            List<GameObject> selected = new List<GameObject>();

            InvokeVoid(selectionController, "SelectUnits", new List<GameObject> { firstUnit, secondUnit });
            InvokeVoid(controlGroups, "ProcessGroupKey", 1, true);
            InvokeVoid(selectionController, "ClearSelection");

            InvokeVoid(selectionController, "CopySelectedUnits", selected);
            Assert.Zero(selected.Count);

            InvokeVoid(controlGroups, "ProcessGroupKey", 1, false);
            InvokeVoid(selectionController, "CopySelectedUnits", selected);

            Assert.AreEqual(2, selected.Count);
            Assert.Contains(firstUnit, selected);
            Assert.Contains(secondUnit, selected);

            UnityEngine.Object.Destroy(firstUnit);
            UnityEngine.Object.Destroy(secondUnit);
            UnityEngine.Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionInfoKeepsUnitsWhenBuildingSelectionClosesOnSameMouseRelease()
        {
            GameObject panelObject = new GameObject("Selection Info Regression Panel");
            Component panel = panelObject.AddComponent(_selectionInfoPanelUiType);
            GameObject firstUnit = CreateUnit(new Vector3(0f, 0f, 0f));
            GameObject secondUnit = CreateUnit(new Vector3(5f, 0f, 0f));

            yield return null;

            InvokeStaticVoid(_eventManagerType, "RaiseUnitSelected", firstUnit);
            InvokeStaticVoid(_eventManagerType, "RaiseUnitSelected", secondUnit);

            IList selectedUnits = (IList)GetFieldValue(panel, "_selectedUnits");
            Assert.AreEqual(2, selectedUnits.Count);

            InvokeStaticVoid(_eventManagerType, "RaiseConstructionClosed");
            selectedUnits = (IList)GetFieldValue(panel, "_selectedUnits");

            Assert.AreEqual(2, selectedUnits.Count);
            Assert.Contains(firstUnit, selectedUnits);
            Assert.Contains(secondUnit, selectedUnits);

            UnityEngine.Object.Destroy(firstUnit);
            UnityEngine.Object.Destroy(secondUnit);
            UnityEngine.Object.Destroy(panelObject);
            yield return null;
        }

        private Component CreateFactory(Vector3 spawnPosition, Vector3 rallyPosition)
        {
            _factoryObject = new GameObject("Factory");
            Component factory = _factoryObject.AddComponent(_buildingProductionType);

            Transform spawnPoint = new GameObject("Spawn Point").transform;
            spawnPoint.position = spawnPosition;
            spawnPoint.SetParent(_factoryObject.transform);

            Transform rallyPoint = new GameObject("Rally Point").transform;
            rallyPoint.position = rallyPosition;
            rallyPoint.SetParent(_factoryObject.transform);

            SetField(factory, "_unitSpawnPoint", spawnPoint);
            SetField(factory, "_unitExitPoint", rallyPoint);
            SetField(factory, "_rallySlotSpacing", 5.75f);
            SetField(factory, "_rallySlotsPerRow", 6);
            SetField(factory, "_rallySlotSearchRows", 2);
            SetField(factory, "_rallyClearancePadding", 0.75f);
            return factory;
        }

        private GameObject CreateUnit(Vector3 position)
        {
            GameObject unit = new GameObject("Runtime Rally Test Unit");
            unit.transform.position = position;
            unit.layer = LayerMask.NameToLayer("PlayerUnit");

            BoxCollider collider = unit.AddComponent<BoxCollider>();
            collider.size = new Vector3(4.1f, 2.25f, 5.65f);
            collider.center = new Vector3(0f, 0.95f, 0f);

            Rigidbody rigidbody = unit.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            NavMeshAgent agent = unit.AddComponent<NavMeshAgent>();
            agent.radius = 2.35f;
            agent.height = 3.2f;
            agent.speed = 30f;
            agent.acceleration = 60f;
            agent.stoppingDistance = 1f;
            agent.updateRotation = false;

            unit.AddComponent(_unitSpawnActivatorType);

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            return unit;
        }

        private static Vector3 InvokeVector3(Component target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (Vector3)method.Invoke(target, args);
        }

        private static void InvokeVoid(Component target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(target, args);
        }

        private static void SetField(Component target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }

        private static object GetFieldValue(Component target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field.GetValue(target);
        }

        private static void InvokeStaticVoid(Type targetType, string methodName, params object[] args)
        {
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method);
            method.Invoke(null, args);
        }

        private static bool InvokeStaticBool(Type targetType, string methodName, params object[] args)
        {
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method);
            return (bool)method.Invoke(null, args);
        }

        private static GameObject FindCommandLine(GameObject unit)
        {
            return unit != null ? GameObject.Find("CommandLine_" + unit.name) : null;
        }

        private static float DistancePointToSegment2D(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
        {
            Vector3 start = new Vector3(segmentStart.x, 0f, segmentStart.z);
            Vector3 end = new Vector3(segmentEnd.x, 0f, segmentEnd.z);
            Vector3 flatPoint = new Vector3(point.x, 0f, point.z);
            Vector3 segment = end - start;

            if (segment.sqrMagnitude <= 0.0001f)
                return Vector3.Distance(flatPoint, start);

            float t = Vector3.Dot(flatPoint - start, segment) / segment.sqrMagnitude;
            Vector3 closest = start + segment * Mathf.Clamp01(t);
            return Vector3.Distance(flatPoint, closest);
        }
    }
}
