using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
        private const string ConstructionCenterTypeName = "Strategy.Buildings.ConstructionCenter, Assembly-CSharp";
        private const string ProductionConfigTypeName = "Strategy.Data.ProductionConfig, Assembly-CSharp";
        private const string ProductionItemDataTypeName = "Strategy.Data.ProductionItemData, Assembly-CSharp";
        private const string UnitDataTypeName = "Strategy.Data.UnitData, Assembly-CSharp";
        private const string FactoryProductionDistributorTypeName = "Strategy.Buildings.FactoryProductionDistributor, Assembly-CSharp";
        private const string FactoryProductionRuntimeStateTypeName = "Strategy.Buildings.FactoryProductionRuntimeState, Assembly-CSharp";
        private const string FactoryProductionStatusPresenterTypeName = "Strategy.Buildings.FactoryProductionStatusPresenter, Assembly-CSharp";
        private const string BuildingSelectionStateTypeName = "Strategy.Buildings.BuildingSelectionState, Assembly-CSharp";
        private const string ProductionButtonRuntimeStateTypeName = "Strategy.UI.ProductionButtonRuntimeState, Assembly-CSharp";
        private const string ProductionButtonStateAggregatorTypeName = "Strategy.UI.ProductionButtonStateAggregator, Assembly-CSharp";
        private const string ProductionButtonUiTypeName = "Strategy.UI.ProductionButtonUI, Assembly-CSharp";
        private const string SelectionInfoPanelUiTypeName = "Strategy.UI.SelectionInfoPanelUI, Assembly-CSharp";
        private const string EventManagerTypeName = "Strategy.Core.EventManager, Assembly-CSharp";
        private const string TextMeshProUiTypeName = "TMPro.TextMeshProUGUI, Unity.TextMeshPro";

        private readonly Type _buildingProductionType = Type.GetType(BuildingProductionTypeName);
        private readonly Type _unitSpawnActivatorType = Type.GetType(UnitSpawnActivatorTypeName);
        private readonly Type _unitDestinationReservationsType = Type.GetType(UnitDestinationReservationsTypeName);
        private readonly Type _unitTrafficCoordinatorType = Type.GetType(UnitTrafficCoordinatorTypeName);
        private readonly Type _unitCommandArrowManagerType = Type.GetType(UnitCommandArrowManagerTypeName);
        private readonly Type _unitCommandControllerType = Type.GetType(UnitCommandControllerTypeName);
        private readonly Type _unitControlGroupControllerType = Type.GetType(UnitControlGroupControllerTypeName);
        private readonly Type _unitSelectionStateType = Type.GetType(UnitSelectionStateTypeName);
        private readonly Type _constructionCenterType = Type.GetType(ConstructionCenterTypeName);
        private readonly Type _productionConfigType = Type.GetType(ProductionConfigTypeName);
        private readonly Type _productionItemDataType = Type.GetType(ProductionItemDataTypeName);
        private readonly Type _unitDataType = Type.GetType(UnitDataTypeName);
        private readonly Type _factoryProductionDistributorType = Type.GetType(FactoryProductionDistributorTypeName);
        private readonly Type _factoryProductionRuntimeStateType = Type.GetType(FactoryProductionRuntimeStateTypeName);
        private readonly Type _factoryProductionStatusPresenterType = Type.GetType(FactoryProductionStatusPresenterTypeName);
        private readonly Type _buildingSelectionStateType = Type.GetType(BuildingSelectionStateTypeName);
        private readonly Type _productionButtonRuntimeStateType = Type.GetType(ProductionButtonRuntimeStateTypeName);
        private readonly Type _productionButtonStateAggregatorType = Type.GetType(ProductionButtonStateAggregatorTypeName);
        private readonly Type _productionButtonUiType = Type.GetType(ProductionButtonUiTypeName);
        private readonly Type _textMeshProUiType = Type.GetType(TextMeshProUiTypeName);
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
            Assert.NotNull(_constructionCenterType);
            Assert.NotNull(_productionConfigType);
            Assert.NotNull(_productionItemDataType);
            Assert.NotNull(_unitDataType);
            Assert.NotNull(_factoryProductionDistributorType);
            Assert.NotNull(_factoryProductionRuntimeStateType);
            Assert.NotNull(_factoryProductionStatusPresenterType);
            Assert.NotNull(_buildingSelectionStateType);
            Assert.NotNull(_productionButtonRuntimeStateType);
            Assert.NotNull(_productionButtonStateAggregatorType);
            Assert.NotNull(_productionButtonUiType);
            Assert.NotNull(_textMeshProUiType);
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

        [UnityTest]
        public IEnumerator DragSelectionPrefersUnitsWhenBuildingsAreInside()
        {
            GameObject cameraObject = new GameObject("Selection Priority Camera");
            cameraObject.AddComponent<Camera>();
            Component selectionController = cameraObject.AddComponent(_unitCommandControllerType);
            GameObject unit = CreateUnit(new Vector3(0f, 0f, 0f));
            Component factory = CreateSelectableFactory("Selectable Factory");
            List<GameObject> selected = new List<GameObject>();

            InvokeVoid(
                selectionController,
                "ApplyDragSelection",
                new object[] { new[] { unit.GetComponent<Collider>(), factory.GetComponent<Collider>() } });

            InvokeVoid(selectionController, "CopySelectedObjects", selected);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(unit, selected[0]);

            UnityEngine.Object.Destroy(unit);
            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DragSelectionSelectsBuildingsWhenNoUnitsAreInside()
        {
            GameObject cameraObject = new GameObject("Building Selection Camera");
            cameraObject.AddComponent<Camera>();
            Component selectionController = cameraObject.AddComponent(_unitCommandControllerType);
            Component factory = CreateSelectableFactory("Selectable Factory");
            GameObject baseObject = new GameObject("MilitaryBase");
            baseObject.layer = LayerMask.NameToLayer("Building");
            baseObject.AddComponent<BoxCollider>();
            baseObject.AddComponent(_constructionCenterType);
            List<GameObject> selected = new List<GameObject>();

            InvokeVoid(
                selectionController,
                "ApplyDragSelection",
                new object[] { new[] { factory.GetComponent<Collider>(), baseObject.GetComponent<Collider>() } });

            InvokeVoid(selectionController, "CopySelectedObjects", selected);

            Assert.AreEqual(2, selected.Count);
            Assert.Contains(factory.gameObject, selected);
            Assert.Contains(baseObject, selected);

            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(baseObject);
            UnityEngine.Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingToggleSelectionAddsAndRemovesFactory()
        {
            GameObject cameraObject = new GameObject("Building Toggle Camera");
            cameraObject.AddComponent<Camera>();
            Component selectionController = cameraObject.AddComponent(_unitCommandControllerType);
            Component factory = CreateSelectableFactory("Toggle Factory");
            List<GameObject> selected = new List<GameObject>();

            Assert.IsTrue(InvokeBool(selectionController, "TryToggleSelection", factory.gameObject));
            InvokeVoid(selectionController, "CopySelectedObjects", selected);
            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(factory.gameObject, selected[0]);

            Assert.IsTrue(InvokeBool(selectionController, "TryToggleSelection", factory.gameObject));
            InvokeVoid(selectionController, "CopySelectedObjects", selected);
            Assert.Zero(selected.Count);

            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EndSelectionIgnoredWhenPressDidNotStartInWorld()
        {
            object mouse = AddMouseDevice();
            GameObject cameraObject = new GameObject("UI Release Selection Camera");
            cameraObject.AddComponent<Camera>();
            Component selectionController = cameraObject.AddComponent(_unitCommandControllerType);
            Component factory = CreateSelectableFactory("Selected Factory Before UI Click");
            List<GameObject> selected = new List<GameObject>();

            InvokeVoid(selectionController, "SelectObjects", new object[] { new[] { factory.gameObject } });
            InvokeVoid(selectionController, "EndSelection");
            InvokeVoid(selectionController, "CopySelectedObjects", selected);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(factory.gameObject, selected[0]);

            RemoveInputDevice(mouse);
            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MixedControlGroupRecallRestoresUnitsFactoryAndMilitaryBase()
        {
            GameObject cameraObject = new GameObject("Mixed Control Group Camera");
            cameraObject.AddComponent<Camera>();
            Component selectionController = cameraObject.AddComponent(_unitCommandControllerType);
            Component controlGroups = cameraObject.AddComponent(_unitControlGroupControllerType);
            GameObject unit = CreateUnit(new Vector3(0f, 0f, 0f));
            Component factory = CreateSelectableFactory("Grouped Factory");
            GameObject baseObject = new GameObject("Grouped MilitaryBase");
            baseObject.layer = LayerMask.NameToLayer("Building");
            baseObject.AddComponent<BoxCollider>();
            baseObject.AddComponent(_constructionCenterType);
            List<GameObject> selected = new List<GameObject>();

            InvokeVoid(selectionController, "SelectObjects", new object[] { new[] { unit, factory.gameObject, baseObject } });
            InvokeVoid(controlGroups, "SaveGroup", 1);
            InvokeVoid(selectionController, "ClearSelection");

            InvokeVoid(selectionController, "CopySelectedObjects", selected);
            Assert.Zero(selected.Count);

            InvokeVoid(controlGroups, "RecallGroup", 1);
            InvokeVoid(selectionController, "CopySelectedObjects", selected);

            Assert.AreEqual(3, selected.Count);
            Assert.Contains(unit, selected);
            Assert.Contains(factory.gameObject, selected);
            Assert.Contains(baseObject, selected);

            UnityEngine.Object.Destroy(unit);
            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(baseObject);
            UnityEngine.Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MultiFactoryDistributorQueuesLeastLoadedFactoriesInStableOrder()
        {
            ScriptableObject config = ScriptableObject.CreateInstance(_productionConfigType);
            ScriptableObject item = CreateProductionItem("Test Tank", 100f, out GameObject unitPrefab);
            InvokeVoid(config, "AddItem", item);
            Component first = CreateProductionFactory("Factory 1", config);
            Component second = CreateProductionFactory("Factory 2", config);
            Component third = CreateProductionFactory("Factory 3", config);
            Array factories = Array.CreateInstance(_buildingProductionType, 3);
            factories.SetValue(first, 0);
            factories.SetValue(second, 1);
            factories.SetValue(third, 2);

            AssertQueuedTo(factories, item, first, 1, 0, 0);
            AssertQueuedTo(factories, item, second, 1, 1, 0);
            AssertQueuedTo(factories, item, third, 1, 1, 1);
            AssertQueuedTo(factories, item, first, 2, 1, 1);

            UnityEngine.Object.Destroy(first.gameObject);
            UnityEngine.Object.Destroy(second.gameObject);
            UnityEngine.Object.Destroy(third.gameObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(GetPropertyValue<UnityEngine.Object>(item, "UnitData"));
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MultiFactoryDistributorQueuesEquivalentItemsToLeastLoadedFactory()
        {
            ScriptableObject firstConfig = ScriptableObject.CreateInstance(_productionConfigType);
            ScriptableObject secondConfig = ScriptableObject.CreateInstance(_productionConfigType);
            ScriptableObject firstItem = CreateProductionItem("Shared Tank", 100f, out GameObject unitPrefab);
            ScriptableObject sharedUnitData = GetPropertyValue<ScriptableObject>(firstItem, "UnitData");
            ScriptableObject secondItem = CreateProductionItemForUnitData("Shared Tank Copy", sharedUnitData, 100f);

            InvokeVoid(firstConfig, "AddItem", firstItem);
            InvokeVoid(secondConfig, "AddItem", secondItem);

            Component first = CreateProductionFactory("Factory 1", firstConfig);
            Component second = CreateProductionFactory("Factory 2", secondConfig);
            Array factories = Array.CreateInstance(_buildingProductionType, 2);
            factories.SetValue(first, 0);
            factories.SetValue(second, 1);

            MethodInfo method = _factoryProductionDistributorType.GetMethod(
                "TryQueueLeastLoaded",
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method);

            object[] firstArgs = { factories, firstItem, null };
            Assert.IsTrue((bool)method.Invoke(null, firstArgs));
            Assert.AreSame(first, firstArgs[2]);

            object[] secondArgs = { factories, firstItem, null };
            Assert.IsTrue((bool)method.Invoke(null, secondArgs));
            Assert.AreSame(second, secondArgs[2]);
            Assert.AreEqual(1, GetPropertyValue<int>(first, "PendingWorkCount"));
            Assert.AreEqual(1, GetPropertyValue<int>(second, "PendingWorkCount"));

            UnityEngine.Object.Destroy(first.gameObject);
            UnityEngine.Object.Destroy(second.gameObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(sharedUnitData);
            UnityEngine.Object.Destroy(firstItem);
            UnityEngine.Object.Destroy(secondItem);
            UnityEngine.Object.Destroy(firstConfig);
            UnityEngine.Object.Destroy(secondConfig);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingProductionRuntimeStateTracksProgressAndClearsAfterSpawn()
        {
            ScriptableObject config = ScriptableObject.CreateInstance(_productionConfigType);
            ScriptableObject item = CreateProductionItem("Progress Tank", 0.3f, out GameObject unitPrefab);
            InvokeVoid(config, "AddItem", item);
            Component factory = CreateProductionFactory("Progress Factory", config);

            Assert.IsTrue(InvokeBool(factory, "AddToQueue", item));
            yield return null;

            Assert.IsTrue(TryGetCurrentProductionState(factory, out object firstState));
            float firstProgress = GetPropertyValue<float>(firstState, "Progress");
            float firstRemaining = GetPropertyValue<float>(firstState, "RemainingSeconds");
            Assert.Greater(firstRemaining, 0f);
            Assert.Less(firstProgress, 1f);

            yield return new WaitForSeconds(0.12f);

            Assert.IsTrue(TryGetCurrentProductionState(factory, out object secondState));
            Assert.Greater(GetPropertyValue<float>(secondState, "Progress"), firstProgress);
            Assert.Less(GetPropertyValue<float>(secondState, "RemainingSeconds"), firstRemaining);

            yield return new WaitForSeconds(0.35f);
            yield return null;

            Assert.IsFalse(TryGetCurrentProductionState(factory, out _));

            DestroyRuntimeObjects();
            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(GetPropertyValue<UnityEngine.Object>(item, "UnitData"));
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator CountPendingWorkForCountsCurrentAndEquivalentQueuedItems()
        {
            ScriptableObject config = ScriptableObject.CreateInstance(_productionConfigType);
            ScriptableObject item = CreateProductionItem("Queued Tank", 100f, out GameObject unitPrefab);
            ScriptableObject sharedUnitData = GetPropertyValue<ScriptableObject>(item, "UnitData");
            ScriptableObject equivalentItem = CreateProductionItemForUnitData("Queued Tank Copy", sharedUnitData, 100f);
            InvokeVoid(config, "AddItem", item);
            Component factory = CreateProductionFactory("Queue Factory", config);

            Assert.IsTrue(InvokeBool(factory, "AddToQueue", item));
            Assert.IsTrue(InvokeBool(factory, "AddToQueue", item));

            Assert.AreEqual(2, InvokeInt(factory, "CountPendingWorkFor", item));
            Assert.AreEqual(2, InvokeInt(factory, "CountPendingWorkFor", equivalentItem));

            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(sharedUnitData);
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(equivalentItem);
            UnityEngine.Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionButtonAggregatorSumsCountsAndUsesFastestActiveFactory()
        {
            ScriptableObject config = ScriptableObject.CreateInstance(_productionConfigType);
            ScriptableObject item = CreateProductionItem("Aggregate Tank", 0.8f, out GameObject unitPrefab);
            InvokeVoid(config, "AddItem", item);
            Component first = CreateProductionFactory("Fast Factory", config);
            Component second = CreateProductionFactory("Slow Factory", config);
            Array factories = Array.CreateInstance(_buildingProductionType, 2);
            factories.SetValue(first, 0);
            factories.SetValue(second, 1);

            Assert.IsTrue(InvokeBool(first, "AddToQueue", item));
            yield return new WaitForSeconds(0.18f);
            Assert.IsTrue(InvokeBool(second, "AddToQueue", item));
            yield return null;

            object aggregateState = InvokeAggregatorBuild(factories, item);
            Assert.AreEqual(2, GetPropertyValue<int>(aggregateState, "PendingCount"));
            Assert.IsTrue(GetPropertyValue<bool>(aggregateState, "HasActiveProgress"));

            Assert.IsTrue(TryGetActiveProductionFor(first, item, out object firstState));
            Assert.IsTrue(TryGetActiveProductionFor(second, item, out object secondState));
            float fastestRemaining = GetPropertyValue<float>(firstState, "RemainingSeconds");
            float slowestRemaining = GetPropertyValue<float>(secondState, "RemainingSeconds");

            Assert.Less(fastestRemaining, slowestRemaining);
            Assert.AreEqual(
                fastestRemaining,
                GetPropertyValue<float>(aggregateState, "RemainingSeconds"),
                0.08f);

            UnityEngine.Object.Destroy(first.gameObject);
            UnityEngine.Object.Destroy(second.gameObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(GetPropertyValue<UnityEngine.Object>(item, "UnitData"));
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator ProductionButtonUiShowsQueueBadgeAndActiveProgress()
        {
            ScriptableObject item = CreateProductionItem("Button Tank", 5f, out GameObject unitPrefab);
            GameObject buttonObject = CreateProductionButtonObject(out Component button, out GameObject badgeRoot, out Component badgeText, out GameObject progressRoot, out Image progressFill);

            InvokeVoid(button, "Initialize", item, null);
            object activeState = Activator.CreateInstance(
                _productionButtonRuntimeStateType,
                3,
                true,
                0.35f,
                1.2f);

            InvokeVoid(button, "SetProductionState", activeState);

            Assert.IsTrue(badgeRoot.activeSelf);
            Assert.AreEqual("3", GetStringProperty(badgeText, "text"));
            Assert.IsTrue(progressRoot.activeSelf);
            Assert.AreEqual(0.35f, progressFill.fillAmount, 0.01f);
            Assert.AreEqual(0.35f, progressFill.rectTransform.anchorMax.x, 0.01f);
            Assert.AreEqual("2s", GetStringProperty((Component)GetFieldValue(button, "_timeText"), "text"));

            object queuedOnlyState = Activator.CreateInstance(
                _productionButtonRuntimeStateType,
                2,
                false,
                0f,
                0f);
            InvokeVoid(button, "SetProductionState", queuedOnlyState);

            Assert.IsTrue(badgeRoot.activeSelf);
            Assert.AreEqual("2", GetStringProperty(badgeText, "text"));
            Assert.IsFalse(progressRoot.activeSelf);
            Assert.AreEqual(0f, progressFill.fillAmount, 0.01f);
            Assert.AreEqual(0f, progressFill.rectTransform.anchorMax.x, 0.01f);

            UnityEngine.Object.Destroy(buttonObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(GetPropertyValue<UnityEngine.Object>(item, "UnitData"));
            UnityEngine.Object.Destroy(item);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FactoryProductionStatusPresenterShowsBarOnlyForSelectedProducingFactory()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<UnityEngine.Camera>();

            ScriptableObject config = ScriptableObject.CreateInstance(_productionConfigType);
            ScriptableObject item = CreateProductionItem("World Bar Tank", 1f, out GameObject unitPrefab);
            InvokeVoid(config, "AddItem", item);
            Component factory = CreateProductionFactory("World Bar Factory", config);
            factory.gameObject.AddComponent(_buildingSelectionStateType);
            Component presenter = factory.gameObject.AddComponent(_factoryProductionStatusPresenterType);
            GameObject statusBar = CreateFactoryStatusBarObject(factory.transform, out Image fill);
            RectTransform trackRect = statusBar.transform.Find("Track").GetComponent<RectTransform>();
            SetField(presenter, "_statusBarRoot", statusBar);
            SetField(presenter, "_trackRect", trackRect);
            SetField(presenter, "_fillRect", fill.rectTransform);
            SetField(presenter, "_fillImage", fill);

            InvokeStaticVoid(_eventManagerType, "RaiseBuildingSelected", factory.gameObject);
            yield return null;
            Assert.IsFalse(statusBar.activeSelf);

            Assert.IsTrue(InvokeBool(factory, "AddToQueue", item));
            yield return null;

            Assert.IsTrue(statusBar.activeSelf);
            Assert.Greater(fill.fillAmount, 0f);
            Assert.Greater(fill.rectTransform.anchorMax.x, 0f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(new Vector3(-90f, 0f, 0f)), statusBar.transform.localRotation), 0.1f);

            InvokeStaticVoid(_eventManagerType, "RaiseBuildingDeselected", factory.gameObject);
            yield return null;

            Assert.IsFalse(statusBar.activeSelf);

            UnityEngine.Object.Destroy(cameraObject);
            UnityEngine.Object.Destroy(factory.gameObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(GetPropertyValue<UnityEngine.Object>(item, "UnitData"));
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(config);
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

        private Component CreateSelectableFactory(string name)
        {
            GameObject factoryObject = new GameObject(name);
            factoryObject.layer = LayerMask.NameToLayer("Building");

            BoxCollider collider = factoryObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 4f, 8f);
            collider.center = new Vector3(0f, 2f, 0f);

            Component factory = factoryObject.AddComponent(_buildingProductionType);

            Transform spawnPoint = new GameObject(name + " Spawn").transform;
            spawnPoint.SetParent(factoryObject.transform);
            SetField(factory, "_unitSpawnPoint", spawnPoint);

            return factory;
        }

        private Component CreateProductionFactory(string name, ScriptableObject config)
        {
            Component factory = CreateSelectableFactory(name);
            SetField(factory, "_productionConfig", config);
            return factory;
        }

        private ScriptableObject CreateProductionItem(
            string itemName,
            float productionTime,
            out GameObject unitPrefab)
        {
            unitPrefab = new GameObject(itemName + " Prefab");
            ScriptableObject unitData = ScriptableObject.CreateInstance(_unitDataType);
            InvokeVoid(
                unitData,
                "Configure",
                unitPrefab,
                100f,
                10f,
                4f,
                20f,
                1f,
                4f,
                180f,
                90f,
                -5f,
                20f,
                3f,
                2f,
                90f,
                itemName,
                null,
                null);

            ScriptableObject item = ScriptableObject.CreateInstance(_productionItemDataType);
            InvokeVoid(item, "Configure", itemName, unitData, 0, productionTime, null);
            return item;
        }

        private ScriptableObject CreateProductionItemForUnitData(
            string itemName,
            ScriptableObject unitData,
            float productionTime)
        {
            ScriptableObject item = ScriptableObject.CreateInstance(_productionItemDataType);
            InvokeVoid(item, "Configure", itemName, unitData, 0, productionTime, null);
            return item;
        }

        private void AssertQueuedTo(
            Array factories,
            ScriptableObject item,
            Component expectedFactory,
            int firstCount,
            int secondCount,
            int thirdCount)
        {
            MethodInfo method = _factoryProductionDistributorType.GetMethod(
                "TryQueueLeastLoaded",
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method);

            object[] args = { factories, item, null };
            Assert.IsTrue((bool)method.Invoke(null, args));
            Component queuedFactory = (Component)args[2];
            Assert.AreSame(expectedFactory, queuedFactory);
            Assert.AreEqual(firstCount, GetPropertyValue<int>(factories.GetValue(0), "PendingWorkCount"));
            Assert.AreEqual(secondCount, GetPropertyValue<int>(factories.GetValue(1), "PendingWorkCount"));
            Assert.AreEqual(thirdCount, GetPropertyValue<int>(factories.GetValue(2), "PendingWorkCount"));
        }

        private bool TryGetCurrentProductionState(Component factory, out object state)
        {
            state = null;
            MethodInfo method = _buildingProductionType.GetMethod(
                "TryGetCurrentProduction",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);

            object[] args = { null };
            bool result = (bool)method.Invoke(factory, args);
            state = args[0];
            return result;
        }

        private bool TryGetActiveProductionFor(Component factory, ScriptableObject item, out object state)
        {
            state = null;
            MethodInfo method = _buildingProductionType.GetMethod(
                "TryGetActiveProductionFor",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);

            object[] args = { item, null };
            bool result = (bool)method.Invoke(factory, args);
            state = args[1];
            return result;
        }

        private object InvokeAggregatorBuild(Array factories, ScriptableObject item)
        {
            MethodInfo method = _productionButtonStateAggregatorType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(method);
            return method.Invoke(null, new object[] { factories, item });
        }

        private GameObject CreateProductionButtonObject(
            out Component button,
            out GameObject badgeRoot,
            out Component badgeText,
            out GameObject progressRoot,
            out Image progressFill)
        {
            GameObject root = new GameObject(
                "Production Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            button = root.AddComponent(_productionButtonUiType);

            badgeRoot = new GameObject(
                "QueueBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            badgeRoot.transform.SetParent(root.transform, false);

            GameObject badgeTextObject = new GameObject(
                "QueueCountText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                _textMeshProUiType);
            badgeTextObject.transform.SetParent(badgeRoot.transform, false);
            badgeText = badgeTextObject.GetComponent(_textMeshProUiType);

            progressRoot = new GameObject(
                "ProgressRoot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            progressRoot.transform.SetParent(root.transform, false);

            GameObject progressFillObject = new GameObject(
                "ProgressFill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            progressFillObject.transform.SetParent(progressRoot.transform, false);
            progressFill = progressFillObject.GetComponent<Image>();
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            return root;
        }

        private static GameObject CreateFactoryStatusBarObject(Transform parent, out Image fillImage)
        {
            GameObject root = new GameObject(
                "FactoryProductionStatusBar",
                typeof(RectTransform),
                typeof(Canvas));
            root.transform.SetParent(parent, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.localPosition = new Vector3(0f, 5.25f, 7.8f);
            rootRect.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            rootRect.localScale = Vector3.one * 0.02f;
            rootRect.sizeDelta = new Vector2(180f, 12f);

            GameObject track = new GameObject(
                "Track",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            track.transform.SetParent(root.transform, false);
            RectTransform trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            GameObject fill = new GameObject(
                "Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fill.transform.SetParent(track.transform, false);
            fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;

            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            root.SetActive(false);
            return root;
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

        private static void InvokeVoid(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(target, args);
        }

        private static bool InvokeBool(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (bool)method.Invoke(target, args);
        }

        private static int InvokeInt(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (int)method.Invoke(target, args);
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

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            return (T)property.GetValue(target);
        }

        private static string GetStringProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            return (string)property.GetValue(target);
        }

        private static object AddMouseDevice()
        {
            Type inputSystemType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            Type mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            Assert.NotNull(inputSystemType);
            Assert.NotNull(mouseType);

            MethodInfo addDeviceMethod = null;
            foreach (MethodInfo method in inputSystemType.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (method.Name == "AddDevice" && method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0)
                {
                    addDeviceMethod = method;
                    break;
                }
            }

            Assert.NotNull(addDeviceMethod);
            return addDeviceMethod.MakeGenericMethod(mouseType).Invoke(null, null);
        }

        private static void RemoveInputDevice(object device)
        {
            if (device == null)
                return;

            Type inputSystemType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            Assert.NotNull(inputSystemType);

            foreach (MethodInfo method in inputSystemType.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name != "RemoveDevice" || parameters.Length != 1 ||
                    !parameters[0].ParameterType.IsInstanceOfType(device))
                {
                    continue;
                }

                method.Invoke(null, new[] { device });
                return;
            }
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

        private static void DestroyRuntimeObjects()
        {
            GameObject runtimeObjects = GameObject.Find("Runtime Objects");
            if (runtimeObjects != null)
                UnityEngine.Object.Destroy(runtimeObjects);
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
