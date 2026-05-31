#if UNITY_EDITOR
using System.Collections.Generic;
using Strategy.Buildings;
using Strategy.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

using Strategy.Core;
using Strategy.Data;
using Strategy.UI;

/// <summary>
/// Editor-only tool that configures NavMeshAgent, TrackedVehicleMotor, TankTrackAnimator,
/// and UnitSpawnActivator for all tracked vehicle prefabs (Tank and Self-Propelled Artillery).
/// Invoked via Tools/RTS/Configure Tracked Vehicles, and also exposes a CI-callable Validate method.
/// </summary>
public static class TrackedVehiclePrefabConfigurator
{
    private const string TankPrefabPath =
        "Assets/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_2/prefabs_yup/unit_Tank_Combat_B_yup.prefab";
    private const string ArtilleryPrefabPath = "Assets/Prefabs/unit_SelfPropelledArtillery.prefab";

    // Pre-built configuration bundles for each tracked vehicle type.
    private static readonly VehicleConfig TankConfig = new VehicleConfig(
        TankPrefabPath,
        "Tank",
        2.66f,
        3.43f,
        -0.1f,
        3.5f,
        4.8f,
        2f,
        112f,
        102f,
        45f,
        2.55f,
        1.05f,
        0.72f,
        0.35f,
        72f,
        12f,
        10,
        6,
        1.58f,
        4.45f,
        0.42f,
        0.42f,
        new Vector3(0.24f, 0.075f, 0.34f),
        8f,
        3.3f);

    private static readonly VehicleConfig ArtilleryConfig = new VehicleConfig(
        ArtilleryPrefabPath,
        "Self-propelled artillery",
        2.35f,
        3.2f,
        -0.08f,
        2.65f,
        3.8f,
        3.6f,
        82f,
        98f,
        42f,
        1.9f,
        0.9f,
        0.62f,
        0.28f,
        70f,
        9f,
        30,
        14,
        2.29f,
        5.12f,
        0.48f,
        0.78f,
        new Vector3(0.15f, 0.09f, 0.22f),
        7f,
        3.4f);

    /// <summary>
    /// Configures both tracked vehicle prefabs and saves them to disk.
    /// </summary>
    [MenuItem("Tools/RTS/Configure Tracked Vehicles")]
    public static void Configure()
    {
        ConfigurePrefab(TankConfig);
        ConfigurePrefab(ArtilleryConfig);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Tracked vehicle prefabs configured.");
    }

    /// <summary>
    /// Headless validation entry point for CI. Checks both prefabs for correct component setup
    /// and calls EditorApplication.Exit(1) if any errors are found.
    /// </summary>
    public static void Validate()
    {
        List<string> errors = new List<string>();
        ValidatePrefab(TankConfig, errors);
        ValidatePrefab(ArtilleryConfig, errors);

        if (errors.Count > 0)
        {
            for (int i = 0; i < errors.Count; i++)
                Debug.LogError(errors[i]);

            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("Tracked vehicle prefab validation passed.");
    }

    /// <summary>
    /// Loads a prefab, applies NavMeshAgent, TrackedVehicleMotor, TankTrackAnimator, and
    /// UnitSpawnActivator settings from <paramref name="config"/>, then saves the prefab.
    /// </summary>
    private static void ConfigurePrefab(VehicleConfig config)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(config.PrefabPath);

        try
        {
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>() ?? root.AddComponent<NavMeshAgent>();
            ConfigureAgent(agent, config);

            RemoveWheelAnimator(root);
            TrackedVehicleMotor motor = EnsureTrackedMotor(root);
            ConfigureMotor(motor, agent, config);

            TankTrackAnimator trackAnimator =
                root.GetComponent<TankTrackAnimator>() ?? root.AddComponent<TankTrackAnimator>();
            ConfigureTrackAnimator(trackAnimator, agent, motor, config);

            UnitSpawnActivator spawnActivator =
                root.GetComponent<UnitSpawnActivator>() ?? root.AddComponent<UnitSpawnActivator>();
            ConfigureSpawnActivator(spawnActivator, config);

            PrefabUtility.SaveAsPrefabAsset(root, config.PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Applies NavMeshAgent movement parameters from <paramref name="config"/>. Rotation is
    /// disabled because TrackedVehicleMotor drives orientation directly.
    /// </summary>
    private static void ConfigureAgent(NavMeshAgent agent, VehicleConfig config)
    {
        agent.radius = config.AgentRadius;
        agent.height = config.AgentHeight;
        agent.baseOffset = config.AgentBaseOffset;
        agent.speed = config.CruiseSpeed;
        agent.acceleration = config.Acceleration;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = config.StoppingDistance;
        agent.autoBraking = true;
        agent.updatePosition = true;
        agent.updateRotation = false;
    }

    /// <summary>
    /// Ensures exactly one TrackedVehicleMotor exists on the root, removing duplicates and
    /// any stale NavMeshVehicleMotor components that belong to wheeled vehicles.
    /// </summary>
    private static TrackedVehicleMotor EnsureTrackedMotor(GameObject root)
    {
        TrackedVehicleMotor[] trackedMotors = root.GetComponents<TrackedVehicleMotor>();
        TrackedVehicleMotor trackedMotor =
            trackedMotors.Length > 0 ? trackedMotors[0] : root.AddComponent<TrackedVehicleMotor>();

        for (int i = 1; i < trackedMotors.Length; i++)
            Object.DestroyImmediate(trackedMotors[i], true);

        NavMeshVehicleMotor[] allMotors = root.GetComponents<NavMeshVehicleMotor>();
        for (int i = 0; i < allMotors.Length; i++)
        {
            if (allMotors[i] != trackedMotor)
                Object.DestroyImmediate(allMotors[i], true);
        }

        return trackedMotor;
    }

    /// <summary>
    /// Destroys all WheeledVehicleAnimator components on the root; tracked vehicles use
    /// TankTrackAnimator instead.
    /// </summary>
    private static void RemoveWheelAnimator(GameObject root)
    {
        WheeledVehicleAnimator[] wheelAnimators = root.GetComponents<WheeledVehicleAnimator>();

        for (int i = 0; i < wheelAnimators.Length; i++)
            Object.DestroyImmediate(wheelAnimators[i], true);
    }

    /// <summary>
    /// Writes all TrackedVehicleMotor serialized fields from <paramref name="config"/> via
    /// SerializedObject so values persist in the prefab asset.
    /// </summary>
    private static void ConfigureMotor(
        TrackedVehicleMotor motor,
        NavMeshAgent agent,
        VehicleConfig config)
    {
        SerializedObject serializedMotor = new SerializedObject(motor);
        SetObject(serializedMotor, "_agent", agent);
        SetBool(serializedMotor, "_applyAgentTuning", true);
        SetFloat(serializedMotor, "_cruiseSpeed", config.CruiseSpeed);
        SetFloat(serializedMotor, "_acceleration", config.Acceleration);
        SetFloat(serializedMotor, "_stoppingDistance", config.StoppingDistance);
        SetFloat(serializedMotor, "_maxSteerAngle", 45f);
        SetFloat(serializedMotor, "_steerResponsiveness", 7f);
        SetFloat(serializedMotor, "_maxSteerSpeed", 130f);
        SetFloat(serializedMotor, "_bodyTurnSpeed", config.BodyTurnSpeed);
        SetFloat(serializedMotor, "_sharpTurnSlowdownAngle", 78f);
        SetFloat(serializedMotor, "_minimumForwardSpeed", 0.25f);
        SetFloat(serializedMotor, "_alignmentStopAngle", config.AlignmentStopAngle);
        SetFloat(serializedMotor, "_alignmentResumeAngle", config.AlignmentResumeAngle);
        SetFloat(serializedMotor, "_pivotTrackSpeed", config.PivotTrackSpeed);
        SetFloat(serializedMotor, "_minimumPivotTrackSpeed", config.MinimumPivotTrackSpeed);
        SetFloat(serializedMotor, "_curveDifferentialMultiplier", config.CurveDifferentialMultiplier);
        SetFloat(serializedMotor, "_minimumCurveDifferentialSpeed", config.MinimumCurveDifferentialSpeed);
        SetFloat(serializedMotor, "_fullDifferentialAngle", config.FullDifferentialAngle);
        SetFloat(serializedMotor, "_trackResponse", config.TrackResponse);
        serializedMotor.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Configures TankTrackAnimator with per-vehicle geometry values (segment count, track
    /// dimensions, scroll speed) from <paramref name="config"/>.
    /// </summary>
    private static void ConfigureTrackAnimator(
        TankTrackAnimator animator,
        NavMeshAgent agent,
        TrackedVehicleMotor motor,
        VehicleConfig config)
    {
        SerializedObject serializedAnimator = new SerializedObject(animator);
        SetObject(serializedAnimator, "_agent", agent);
        SetObject(serializedAnimator, "_vehicleMotor", motor);
        SetInt(serializedAnimator, "_segmentsPerRun", config.SegmentsPerRun);
        SetInt(serializedAnimator, "_endSegmentsPerLoop", config.EndSegmentsPerLoop);
        SetFloat(serializedAnimator, "_trackHalfWidth", config.TrackHalfWidth);
        SetFloat(serializedAnimator, "_trackLength", config.TrackLength);
        SetFloat(serializedAnimator, "_trackCenterY", config.TrackCenterY);
        SetFloat(serializedAnimator, "_trackVerticalSpacing", config.TrackVerticalSpacing);
        SetVector3(serializedAnimator, "_segmentScale", config.SegmentScale);
        SetFloat(serializedAnimator, "_scrollSpeedMultiplier", 1.55f);
        SetFloat(serializedAnimator, "_maxVisualTrackSpeed", config.MaxVisualTrackSpeed);
        SetFloat(serializedAnimator, "_moveThreshold", 0.08f);
        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Configures UnitSpawnActivator with the exit speed and NavMesh snap radii for safe factory
    /// spawning behaviour.
    /// </summary>
    private static void ConfigureSpawnActivator(UnitSpawnActivator spawnActivator, VehicleConfig config)
    {
        SerializedObject serializedSpawn = new SerializedObject(spawnActivator);
        SetFloat(serializedSpawn, "_exitMoveSpeed", config.ExitMoveSpeed);
        SetFloat(serializedSpawn, "_exitDistance", 0.25f);
        SetFloat(serializedSpawn, "_navMeshSnapRadius", 6f);
        SetFloat(serializedSpawn, "_navMeshFallbackSnapRadius", 14f);
        serializedSpawn.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Loads a prefab and validates that its NavMeshAgent, TrackedVehicleMotor, TankTrackAnimator,
    /// and UnitSpawnActivator are correctly set up, appending errors to <paramref name="errors"/>.
    /// </summary>
    private static void ValidatePrefab(VehicleConfig config, List<string> errors)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(config.PrefabPath);

        try
        {
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            TrackedVehicleMotor trackedMotor = root.GetComponent<TrackedVehicleMotor>();
            TankTrackAnimator trackAnimator = root.GetComponent<TankTrackAnimator>();
            UnitSpawnActivator spawnActivator = root.GetComponent<UnitSpawnActivator>();

            if (agent == null)
            {
                errors.Add(config.DisplayName + " is missing NavMeshAgent.");
            }
            else
            {
                if (!agent.updatePosition)
                    errors.Add(config.DisplayName + " NavMeshAgent.updatePosition should be enabled.");

                if (agent.updateRotation)
                    errors.Add(config.DisplayName + " NavMeshAgent.updateRotation should be disabled.");

                if (agent.speed < config.CruiseSpeed - 0.05f || agent.acceleration < config.Acceleration - 0.05f)
                    errors.Add(config.DisplayName + " NavMeshAgent movement tuning was not applied.");
            }

            NavMeshVehicleMotor[] allMotors = root.GetComponents<NavMeshVehicleMotor>();
            if (trackedMotor == null)
                errors.Add(config.DisplayName + " is missing TrackedVehicleMotor.");

            if (allMotors.Length != 1 || trackedMotor == null || allMotors[0] != trackedMotor)
                errors.Add(config.DisplayName + " should have exactly one tracked vehicle motor.");

            if (root.GetComponent<WheeledVehicleAnimator>() != null)
                errors.Add(config.DisplayName + " should not use WheeledVehicleAnimator.");

            if (trackAnimator == null)
            {
                errors.Add(config.DisplayName + " is missing TankTrackAnimator.");
            }
            else
            {
                ValidateObject(trackAnimator, "_agent", errors, config.DisplayName);
                ValidateObject(trackAnimator, "_vehicleMotor", errors, config.DisplayName);
            }

            if (spawnActivator == null)
                errors.Add(config.DisplayName + " is missing UnitSpawnActivator.");
            else
                ValidateSpawnActivator(spawnActivator, config.DisplayName, errors);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Checks that the UnitSpawnActivator's NavMesh snap radii meet the minimum values required
    /// for reliable post-spawn pathing.
    /// </summary>
    private static void ValidateSpawnActivator(
        UnitSpawnActivator spawnActivator,
        string displayName,
        List<string> errors)
    {
        SerializedObject serializedSpawn = new SerializedObject(spawnActivator);
        ValidateFloatAtLeast(serializedSpawn, "_navMeshSnapRadius", 6f, displayName, errors);
        ValidateFloatAtLeast(serializedSpawn, "_navMeshFallbackSnapRadius", 14f, displayName, errors);
    }

    /// <summary>
    /// Checks that a serialized Object reference property on <paramref name="target"/> is assigned,
    /// prefixing the error with <paramref name="displayName"/> for context.
    /// </summary>
    private static void ValidateObject(
        Object target,
        string propertyName,
        List<string> errors,
        string displayName)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

        if (property == null || property.objectReferenceValue == null)
            errors.Add(displayName + " " + propertyName + " is not assigned.");
    }

    /// <summary>
    /// Checks that a float serialized property meets a minimum value, prefixing the error
    /// with <paramref name="displayName"/> for context.
    /// </summary>
    private static void ValidateFloatAtLeast(
        SerializedObject serializedObject,
        string propertyName,
        float minimum,
        string displayName,
        List<string> errors)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            errors.Add(displayName + " " + propertyName + " is missing.");
            return;
        }

        if (property.floatValue < minimum)
            errors.Add(displayName + " " + propertyName + " should be at least " + minimum.ToString("0.##") + ".");
    }

    /// <summary>Sets an Object reference property on an already-open SerializedObject.</summary>
    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>Sets a Vector3 property on an already-open SerializedObject.</summary>
    private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.vector3Value = value;
    }

    /// <summary>Sets a float property on an already-open SerializedObject.</summary>
    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>Sets an int property on an already-open SerializedObject.</summary>
    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>Sets a bool property on an already-open SerializedObject.</summary>
    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Immutable value type that bundles all per-vehicle tuning parameters passed to the
    /// configure/validate methods, avoiding long parameter lists.
    /// </summary>
    private readonly struct VehicleConfig
    {
        public VehicleConfig(
            string prefabPath,
            string displayName,
            float agentRadius,
            float agentHeight,
            float agentBaseOffset,
            float cruiseSpeed,
            float acceleration,
            float stoppingDistance,
            float bodyTurnSpeed,
            float alignmentStopAngle,
            float alignmentResumeAngle,
            float pivotTrackSpeed,
            float minimumPivotTrackSpeed,
            float curveDifferentialMultiplier,
            float minimumCurveDifferentialSpeed,
            float fullDifferentialAngle,
            float trackResponse,
            int segmentsPerRun,
            int endSegmentsPerLoop,
            float trackHalfWidth,
            float trackLength,
            float trackCenterY,
            float trackVerticalSpacing,
            Vector3 segmentScale,
            float maxVisualTrackSpeed,
            float exitMoveSpeed)
        {
            PrefabPath = prefabPath;
            DisplayName = displayName;
            AgentRadius = agentRadius;
            AgentHeight = agentHeight;
            AgentBaseOffset = agentBaseOffset;
            CruiseSpeed = cruiseSpeed;
            Acceleration = acceleration;
            StoppingDistance = stoppingDistance;
            BodyTurnSpeed = bodyTurnSpeed;
            AlignmentStopAngle = alignmentStopAngle;
            AlignmentResumeAngle = alignmentResumeAngle;
            PivotTrackSpeed = pivotTrackSpeed;
            MinimumPivotTrackSpeed = minimumPivotTrackSpeed;
            CurveDifferentialMultiplier = curveDifferentialMultiplier;
            MinimumCurveDifferentialSpeed = minimumCurveDifferentialSpeed;
            FullDifferentialAngle = fullDifferentialAngle;
            TrackResponse = trackResponse;
            SegmentsPerRun = segmentsPerRun;
            EndSegmentsPerLoop = endSegmentsPerLoop;
            TrackHalfWidth = trackHalfWidth;
            TrackLength = trackLength;
            TrackCenterY = trackCenterY;
            TrackVerticalSpacing = trackVerticalSpacing;
            SegmentScale = segmentScale;
            MaxVisualTrackSpeed = maxVisualTrackSpeed;
            ExitMoveSpeed = exitMoveSpeed;
        }

        public string PrefabPath { get; }
        public string DisplayName { get; }
        public float AgentRadius { get; }
        public float AgentHeight { get; }
        public float AgentBaseOffset { get; }
        public float CruiseSpeed { get; }
        public float Acceleration { get; }
        public float StoppingDistance { get; }
        public float BodyTurnSpeed { get; }
        public float AlignmentStopAngle { get; }
        public float AlignmentResumeAngle { get; }
        public float PivotTrackSpeed { get; }
        public float MinimumPivotTrackSpeed { get; }
        public float CurveDifferentialMultiplier { get; }
        public float MinimumCurveDifferentialSpeed { get; }
        public float FullDifferentialAngle { get; }
        public float TrackResponse { get; }
        public int SegmentsPerRun { get; }
        public int EndSegmentsPerLoop { get; }
        public float TrackHalfWidth { get; }
        public float TrackLength { get; }
        public float TrackCenterY { get; }
        public float TrackVerticalSpacing { get; }
        public Vector3 SegmentScale { get; }
        public float MaxVisualTrackSpeed { get; }
        public float ExitMoveSpeed { get; }
    }
}
#endif
