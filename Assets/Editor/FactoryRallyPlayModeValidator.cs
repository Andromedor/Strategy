using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Strategy.Buildings;
using Strategy.Units;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Strategy.EditorTools
{
    /// <summary>
    /// Batchmode smoke-validator для factory/rally логіки: створює runtime-сцену, входить у Play Mode і завершує Unity з кодом 0 або 1.
    /// </summary>
    public static class FactoryRallyPlayModeValidator
    {
        private static IEnumerator _routine;
        private static string _resultPath;
        private static bool _previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions _previousEnterPlayModeOptions;

        public static void Run()
        {
            _resultPath = Path.Combine(Application.dataPath, "../Logs/FactoryRallyPlayModeValidation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
            File.WriteAllText(_resultPath, "Factory rally Play Mode validation started.\n");

            _previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _routine = RunValidation();
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (_routine == null)
                return;

            try
            {
                if (!_routine.MoveNext())
                    Finish(0, "Factory rally Play Mode validation passed.");
            }
            catch (Exception exception)
            {
                Finish(1, exception.ToString());
            }
        }

        private static IEnumerator RunValidation()
        {
            while (!EditorApplication.isPlaying || !Application.isPlaying)
                yield return null;

            GameObject navMeshRoot = null;
            GameObject factoryObject = null;

            try
            {
                navMeshRoot = GameObject.CreatePrimitive(PrimitiveType.Plane);
                navMeshRoot.name = "Runtime Validator NavMesh";
                navMeshRoot.transform.localScale = new Vector3(8f, 1f, 8f);

                NavMeshSurface surface = navMeshRoot.AddComponent<NavMeshSurface>();
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();

                yield return null;

                factoryObject = new GameObject("Factory Validator");
                BuildingProduction factory = factoryObject.AddComponent<BuildingProduction>();
                Transform spawnPoint = CreateChildPoint(factoryObject.transform, "Spawn Point", Vector3.zero);
                Transform rallyPoint = CreateChildPoint(factoryObject.transform, "Rally Point", new Vector3(20f, 0f, 0f));

                SetField(factory, "_unitSpawnPoint", spawnPoint);
                SetField(factory, "_unitExitPoint", rallyPoint);
                SetField(factory, "_rallySlotSpacing", 5.75f);
                SetField(factory, "_rallySlotsPerRow", 6);
                SetField(factory, "_rallySlotSearchRows", 2);
                SetField(factory, "_rallyClearancePadding", 0.75f);

                GameObject firstUnit = CreateUnit(Vector3.zero);
                GameObject secondUnit = CreateUnit(Vector3.zero);
                Vector3 firstDestination = InvokeVector3(factory, "ResolveRallyDestination", firstUnit);
                Vector3 secondDestination = InvokeVector3(factory, "ResolveRallyDestination", secondUnit);
                float slotDistance = Vector3.Distance(firstDestination, secondDestination);

                if (slotDistance < 5f)
                    throw new InvalidOperationException($"Rally slots overlap: distance={slotDistance:0.00}");

                InvokeVoid(factory, "ReleaseRallyDestination", firstDestination);
                InvokeVoid(factory, "ReleaseRallyDestination", secondDestination);
                UnityEngine.Object.Destroy(firstUnit);
                UnityEngine.Object.Destroy(secondUnit);

                SetField(factory, "_rallyBlockerMask", default(LayerMask));

                GameObject externalReservedUnit = CreateUnit(new Vector3(30f, 0f, 0f));
                UnitDestinationReservations.Reserve(externalReservedUnit, rallyPoint.position, 3.1f);

                GameObject thirdUnit = CreateUnit(Vector3.zero);
                Vector3 thirdDestination = InvokeVector3(factory, "ResolveRallyDestination", thirdUnit);

                if (Vector3.Distance(thirdDestination, rallyPoint.position) < 5f)
                {
                    throw new InvalidOperationException(
                        $"Factory assigned an already reserved rally point. Destination={thirdDestination}");
                }

                UnitDestinationReservations.Release(externalReservedUnit);
                UnityEngine.Object.Destroy(externalReservedUnit);
                UnityEngine.Object.Destroy(thirdUnit);

                GameObject trafficRequester = CreateUnit(Vector3.zero);
                GameObject trafficBlocker = CreateUnit(new Vector3(4f, 0f, 0f));
                NavMeshAgent trafficBlockerAgent = trafficBlocker.GetComponent<NavMeshAgent>();
                bool trafficBlocked = UnitTrafficCoordinator.RequestYieldForCorridor(
                    trafficRequester,
                    Vector3.zero,
                    new Vector3(10f, 0f, 0f),
                    3.2f);

                yield return null;

                if (!trafficBlocked || !trafficBlockerAgent.hasPath)
                    throw new InvalidOperationException("Idle traffic blocker did not yield from the factory corridor.");

                if (DistancePointToSegment2D(
                        trafficBlockerAgent.destination,
                        Vector3.zero,
                        new Vector3(10f, 0f, 0f)) <= 3.2f)
                {
                    throw new InvalidOperationException(
                        $"Traffic blocker yielded inside the blocked corridor. Destination={trafficBlockerAgent.destination}");
                }

                UnityEngine.Object.Destroy(trafficRequester);
                UnityEngine.Object.Destroy(trafficBlocker);

                GameObject commandedUnit = CreateUnit(Vector3.zero);
                UnitSpawnActivator activator = commandedUnit.GetComponent<UnitSpawnActivator>();
                NavMeshAgent agent = commandedUnit.GetComponent<NavMeshAgent>();

                activator.SetSpawningState(true);
                activator.QueueMoveAfterSpawn(new Vector3(24f, 0f, 0f));

                bool releasedRallyReservation = false;
                IEnumerator moveOutRoutine = activator.MoveOutOfFactory(
                    new Vector3(4f, 0f, 0f),
                    new Vector3(10f, 0f, 0f),
                    _ => releasedRallyReservation = true);

                while (moveOutRoutine.MoveNext())
                    yield return null;

                yield return null;

                if (!releasedRallyReservation)
                    throw new InvalidOperationException("Queued player command did not release the rally reservation.");

                if (!agent.enabled || !agent.isOnNavMesh)
                    throw new InvalidOperationException("Agent was not activated on NavMesh after factory exit.");

                if (Vector3.Distance(agent.destination, new Vector3(24f, 0f, 0f)) > 1.5f)
                    throw new InvalidOperationException($"Player destination was not applied. Actual={agent.destination}");

                UnityEngine.Object.Destroy(commandedUnit);
            }
            finally
            {
                NavMesh.RemoveAllNavMeshData();

                if (factoryObject != null)
                    UnityEngine.Object.Destroy(factoryObject);

                if (navMeshRoot != null)
                    UnityEngine.Object.Destroy(navMeshRoot);
            }
        }

        private static Transform CreateChildPoint(Transform parent, string name, Vector3 position)
        {
            Transform point = new GameObject(name).transform;
            point.position = position;
            point.SetParent(parent);
            return point;
        }

        private static GameObject CreateUnit(Vector3 position)
        {
            GameObject unit = new GameObject("Runtime Rally Validator Unit");
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

            unit.AddComponent<UnitSpawnActivator>();

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            return unit;
        }

        private static Vector3 InvokeVector3(Component target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, methodName);

            return (Vector3)method.Invoke(target, args);
        }

        private static void InvokeVoid(Component target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, methodName);

            method.Invoke(target, args);
        }

        private static void SetField(Component target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
                throw new MissingFieldException(target.GetType().FullName, fieldName);

            field.SetValue(target, value);
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

        private static void Finish(int exitCode, string message)
        {
            EditorApplication.update -= Tick;
            _routine = null;
            EditorSettings.enterPlayModeOptionsEnabled = _previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = _previousEnterPlayModeOptions;

            File.AppendAllText(_resultPath, message + "\n");

            if (exitCode == 0)
                Debug.Log(message);
            else
                Debug.LogError(message);

            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exitCode);
        }
    }
}
