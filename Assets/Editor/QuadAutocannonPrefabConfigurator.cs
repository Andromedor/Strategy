using Strategy.Data;
using System.Collections.Generic;
using Strategy.Units;
using Strategy.Buildings;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

using Strategy.Core;
using Strategy.UI;

/// <summary>
/// Редакторний інструмент для конфігурації префабу Quad Autocannon та його валідації з CI.
/// Викликається через Tools/RTS/Configure Quad Autocannon. Налаштовує бойові характеристики,
/// візуальні ефекти, анімацію коліс, рух, балансові параметри, дані виробництва та NavMesh сцени.
/// </summary>
public static class QuadAutocannonPrefabConfigurator
{
    private const string PrefabPath =
        "Assets/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_2/prefabs_yup/unit_Quad_B_yup.prefab";
    private const string FactoryPrefabPath =
        "Assets/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_2/prefabs_yup/struct_Factory_Heavy_A_yup.prefab";
    private const string MainScenePath = "Assets/Scenes/mainScene.unity";
    private const string UnitDataPath = "Assets/Balance/LightTank.asset";
    private const string ProductionDataPath = "Assets/Balance/LightTankProduction.asset";

    /// <summary>
    /// Основний прохід конфігурації: завантажує префаб, з'єднує всі компоненти та зберігає його на диск.
    /// Також викликає ConfigureSceneNavigation для оновлення маски шарів NavMeshSurface головної сцени.
    /// </summary>
    [MenuItem("Tools/RTS/Configure Quad Autocannon")]
    public static void Configure()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(UnitDataPath);
            Transform turret = FindChildRecursive(root.transform, "unit_Quad_B_Gun_Turret_yup");
            Transform gun = FindChildRecursive(root.transform, "unit_Quad_B_Gun_yup");
            Transform shootPoint = FindChildRecursive(root.transform, "ShootPoint");
            Transform frontWheel = FindChildRecursive(root.transform, "unit_Quad_B_Wheel_Front_yup");
            Transform rearWheel = FindChildRecursive(root.transform, "unit_Quad_B_Wheel_Rear_yup");

            Transform leftMuzzle = GetOrCreateMuzzle(gun, shootPoint, "ShootPoint_Left", -0.18f);
            Transform rightMuzzle = GetOrCreateMuzzle(gun, shootPoint, "ShootPoint_Right", 0.18f);

            ConfigureCombat(root, unitData, turret, gun, shootPoint, leftMuzzle, rightMuzzle);
            ConfigureEffects(root, gun, leftMuzzle, rightMuzzle);
            ConfigureMovement(root, frontWheel, rearWheel);
            ConfigureBalance(unitData);
            ConfigureProduction();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ConfigureSceneNavigation();
        Debug.Log("Configured quad autocannon prefab, visual effects, wheel animation, movement, and balance.");
    }

    /// <summary>
    /// Точка входу для беззупинної валідації з CI (EditorApplication.Exit(1) при помилці).
    /// Перевіряє наявність компонентів, призначення серіалізованих полів, налаштування агента,
    /// обмеження балансу, конфігурацію префабу заводу та NavMesh головної сцени.
    /// </summary>
    public static void Validate()
    {
        List<string> errors = new List<string>();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        float quadAgentRadius = 1.18f;

        try
        {
            QuadAutocannonCombat combat = root.GetComponent<QuadAutocannonCombat>();
            AutocannonVisualEffects effects = root.GetComponent<AutocannonVisualEffects>();
            NavMeshVehicleMotor motor = root.GetComponent<NavMeshVehicleMotor>();
            WheeledVehicleAnimator wheels = root.GetComponent<WheeledVehicleAnimator>();
            UnitSpawnActivator spawnActivator = root.GetComponent<UnitSpawnActivator>();
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(UnitDataPath);
            ProductionItemData productionData =
                AssetDatabase.LoadAssetAtPath<ProductionItemData>(ProductionDataPath);

            if (combat == null)
                errors.Add("QuadAutocannonCombat is missing.");

            if (root.GetComponents<UnitCombat>().Length != 1)
                errors.Add("Prefab should have exactly one UnitCombat-derived component.");

            ValidateObjectArray(combat, "_muzzlePoints", 2, errors);
            ValidateObject(effects, "_gun", errors);
            ValidateObjectArray(effects, "_muzzles", 2, errors);
            ValidateObjectArray(wheels, "_driveWheels", 2, errors);
            ValidateObjectArray(wheels, "_steeringWheels", 1, errors);

            if (effects == null)
                errors.Add("AutocannonVisualEffects is missing.");

            if (motor == null)
                errors.Add("NavMeshVehicleMotor is missing.");

            if (wheels == null)
                errors.Add("WheeledVehicleAnimator is missing.");

            if (spawnActivator == null)
                errors.Add("UnitSpawnActivator is missing.");
            else
            {
                ValidateFloatAtLeast(spawnActivator, "_navMeshSnapRadius", 6f, errors);
                ValidateFloatAtLeast(spawnActivator, "_navMeshFallbackSnapRadius", 14f, errors);
            }

            if (agent == null)
                errors.Add("NavMeshAgent is missing.");
            else
            {
                quadAgentRadius = agent.radius;

                if (agent.updateRotation)
                    errors.Add("NavMeshAgent.updateRotation should be disabled for vehicle body smoothing.");

                if (!agent.updatePosition)
                    errors.Add("NavMeshAgent.updatePosition should be enabled so the agent owns movement.");

                if (agent.speed < 4.8f || agent.speed > 5.6f || agent.acceleration > 7f || agent.angularSpeed < 300f)
                    errors.Add("NavMeshAgent tuning is outside the expected quad vehicle range.");
            }

            if (unitData == null)
                errors.Add("LightTank UnitData is missing.");
            else
            {
                if (unitData.Damage > 4f)
                    errors.Add("Quad damage should stay small for rapid-fire balance.");

                if (unitData.AttackDelay > 0.22f)
                    errors.Add("Quad attack delay should stay fast for autocannon behavior.");

                if (unitData.AttackRange > 20f)
                    errors.Add("Quad attack range should stay shorter than heavier vehicles.");
            }

            if (productionData == null)
                errors.Add("LightTankProduction data is missing.");
            else if (productionData.Cost > 35 || productionData.Cost <= 0)
                errors.Add("Quad production cost should be a low non-zero value.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        ValidateFactoryPrefab(errors, quadAgentRadius);
        ValidateMainSceneNavigation(errors);

        if (errors.Count > 0)
        {
            for (int i = 0; i < errors.Count; i++)
                Debug.LogError(errors[i]);

            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("Quad autocannon validation passed.");
    }

    /// <summary>
    /// Переконується, що на корені префабу є рівно один компонент QuadAutocannonCombat,
    /// видаляє інші підкласи UnitCombat та прив'язує посилання на турель, гармату, дула і ефекти.
    /// </summary>
    private static void ConfigureCombat(
        GameObject root,
        UnitData unitData,
        Transform turret,
        Transform gun,
        Transform shootPoint,
        Transform leftMuzzle,
        Transform rightMuzzle)
    {
        UnitCombat[] combatComponents = root.GetComponents<UnitCombat>();
        QuadAutocannonCombat quadCombat = null;

        for (int i = 0; i < combatComponents.Length; i++)
        {
            if (combatComponents[i] is QuadAutocannonCombat existingQuadCombat)
            {
                quadCombat = existingQuadCombat;
                continue;
            }

            Object.DestroyImmediate(combatComponents[i], true);
        }

        if (quadCombat == null)
            quadCombat = root.AddComponent<QuadAutocannonCombat>();

        AutocannonVisualEffects effects =
            root.GetComponent<AutocannonVisualEffects>() ?? root.AddComponent<AutocannonVisualEffects>();

        SerializedObject serializedCombat = new SerializedObject(quadCombat);
        SetObject(serializedCombat, "_unitData", unitData);
        SetObject(serializedCombat, "_pointPosition", shootPoint);
        SetObject(serializedCombat, "_shotEffects", null);
        SetObject(serializedCombat, "_turret", turret);
        SetObject(serializedCombat, "_gun", gun);
        SetObject(serializedCombat, "_autocannonEffects", effects);
        SetObjectArray(serializedCombat, "_muzzlePoints", leftMuzzle, rightMuzzle);
        SetFloat(serializedCombat, "_barrelSpacing", 0.18f);
        serializedCombat.ApplyModifiedPropertiesWithoutUndo();

        // Якщо присутній індикатор дальності, оновлюємо його посилання на бойовий компонент.
        UnitAttackRangeIndicator indicator = root.GetComponent<UnitAttackRangeIndicator>();

        if (indicator != null)
        {
            SerializedObject serializedIndicator = new SerializedObject(indicator);
            SetObject(serializedIndicator, "_combat", quadCombat);
            serializedIndicator.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// Налаштовує AutocannonVisualEffects з трансформом гармати, двома трансформами дул та
    /// параметрами віддачі/кількості частинок для візуального відчуття швидкострільності Quad Autocannon.
    /// </summary>
    private static void ConfigureEffects(
        GameObject root,
        Transform gun,
        Transform leftMuzzle,
        Transform rightMuzzle)
    {
        AutocannonVisualEffects effects =
            root.GetComponent<AutocannonVisualEffects>() ?? root.AddComponent<AutocannonVisualEffects>();

        SerializedObject serializedEffects = new SerializedObject(effects);
        SetObject(serializedEffects, "_gun", gun);
        SetObjectArray(serializedEffects, "_muzzles", leftMuzzle, rightMuzzle);
        SetFloat(serializedEffects, "_recoilDistance", 0.1f);
        SetFloat(serializedEffects, "_recoilBackDuration", 0.022f);
        SetFloat(serializedEffects, "_recoilReturnDuration", 0.075f);
        SetInt(serializedEffects, "_flashParticles", 9);
        SetInt(serializedEffects, "_smokeParticles", 5);
        serializedEffects.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Налаштовує NavMeshAgent, NavMeshVehicleMotor, WheeledVehicleAnimator та UnitSpawnActivator
    /// для руху колісного транспортного засобу. Поворот агента обробляється NavMeshVehicleMotor, а не Unity.
    /// </summary>
    private static void ConfigureMovement(GameObject root, Transform frontWheel, Transform rearWheel)
    {
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.radius = 1.18f;
            agent.height = 1.85f;
            agent.baseOffset = -0.08f;
            agent.speed = 5.2f;
            agent.acceleration = 5.2f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 1.45f;
            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = false;
        }

        NavMeshVehicleMotor motor =
            root.GetComponent<NavMeshVehicleMotor>() ?? root.AddComponent<NavMeshVehicleMotor>();
        SerializedObject serializedMotor = new SerializedObject(motor);
        SetObject(serializedMotor, "_agent", agent);
        SetBool(serializedMotor, "_applyAgentTuning", true);
        SetFloat(serializedMotor, "_cruiseSpeed", 5.2f);
        SetFloat(serializedMotor, "_acceleration", 5.2f);
        SetFloat(serializedMotor, "_stoppingDistance", 1.45f);
        SetFloat(serializedMotor, "_maxSteerAngle", 28f);
        SetFloat(serializedMotor, "_steerResponsiveness", 7f);
        SetFloat(serializedMotor, "_maxSteerSpeed", 125f);
        SetFloat(serializedMotor, "_bodyTurnSpeed", 150f);
        SetFloat(serializedMotor, "_sharpTurnSlowdownAngle", 95f);
        SetFloat(serializedMotor, "_minimumForwardSpeed", 0f);
        SetFloat(serializedMotor, "_alignmentStopAngle", 105f);
        SetFloat(serializedMotor, "_alignmentResumeAngle", 68f);
        serializedMotor.ApplyModifiedPropertiesWithoutUndo();

        UnitSpawnActivator spawnActivator =
            root.GetComponent<UnitSpawnActivator>() ?? root.AddComponent<UnitSpawnActivator>();
        SerializedObject serializedSpawnActivator = new SerializedObject(spawnActivator);
        SetFloat(serializedSpawnActivator, "_exitMoveSpeed", 4f);
        SetFloat(serializedSpawnActivator, "_exitDistance", 0.2f);
        SetFloat(serializedSpawnActivator, "_navMeshSnapRadius", 6f);
        SetFloat(serializedSpawnActivator, "_navMeshFallbackSnapRadius", 14f);
        serializedSpawnActivator.ApplyModifiedPropertiesWithoutUndo();

        WheeledVehicleAnimator wheels =
            root.GetComponent<WheeledVehicleAnimator>() ?? root.AddComponent<WheeledVehicleAnimator>();
        SerializedObject serializedWheels = new SerializedObject(wheels);
        SetObject(serializedWheels, "_agent", agent);
        SetObject(serializedWheels, "_vehicleMotor", motor);
        SetObjectArray(serializedWheels, "_driveWheels", frontWheel, rearWheel);
        SetObjectArray(serializedWheels, "_steeringWheels", frontWheel);
        SetFloat(serializedWheels, "_wheelRadius", 0.43f);
        SetFloat(serializedWheels, "_maxSteerAngle", 28f);
        SetFloat(serializedWheels, "_steerResponsiveness", 7f);
        SetFloat(serializedWheels, "_maxSteerSpeed", 125f);
        SetFloat(serializedWheels, "_spinMultiplier", 1.08f);
        serializedWheels.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Записує еталонні бойові характеристики в асет UnitData LightTank для Quad Autocannon
    /// (швидкострільність, коротка дальність, малий збиток за постріл).
    /// </summary>
    private static void ConfigureBalance(UnitData unitData)
    {
        if (unitData == null)
            return;

        unitData.Configure(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
            72f,
            3f,
            58f,
            18f,
            0.18f,
            3.2f,
            560f,
            180f,
            -7f,
            18f,
            4.5f,
            1.1f,
            160f);
        EditorUtility.SetDirty(unitData);
    }

    /// <summary>
    /// Записує еталонну вартість та час побудови в асет ProductionItemData LightTankProduction.
    /// </summary>
    private static void ConfigureProduction()
    {
        ProductionItemData productionData =
            AssetDatabase.LoadAssetAtPath<ProductionItemData>(ProductionDataPath);

        if (productionData == null)
            return;

        productionData.Configure(
            "Quad Autocannon",
            AssetDatabase.LoadAssetAtPath<UnitData>(UnitDataPath),
            30,
            2f);
        EditorUtility.SetDirty(productionData);
    }

    /// <summary>
    /// Відкриває головну сцену, знаходить NavMeshSurface та переконується, що встановлено
    /// потрібні фізичні шари та прапорці ігнорування агентів/перешкод, після чого зберігає сцену.
    /// </summary>
    private static void ConfigureSceneNavigation()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();

        if (surface == null)
        {
            Debug.LogWarning("NavMeshSurface was not found in " + MainScenePath + ".");
            return;
        }

        SerializedObject serializedSurface = new SerializedObject(surface);
        SerializedProperty layerMask = serializedSurface.FindProperty("m_LayerMask");

        if (layerMask != null)
            layerMask.intValue |= GetRequiredNavSurfaceMask();

        SetBool(serializedSurface, "m_IgnoreNavMeshAgent", true);
        SetBool(serializedSurface, "m_IgnoreNavMeshObstacle", true);
        serializedSurface.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// Перевіряє префаб заводу на наявність необхідних компонентів та перевіряє, чи
    /// UnitExitPoint знаходиться достатньо далеко від NavMeshObstacle, щоб заспавнені юніти могли прокласти маршрут.
    /// </summary>
    private static void ValidateFactoryPrefab(List<string> errors, float quadAgentRadius)
    {
        GameObject factoryRoot = PrefabUtility.LoadPrefabContents(FactoryPrefabPath);

        try
        {
            BuildingProduction production = factoryRoot.GetComponent<BuildingProduction>();
            NavMeshObstacle obstacle = factoryRoot.GetComponent<NavMeshObstacle>();
            Transform exitPoint = FindChildRecursive(factoryRoot.transform, "UnitExitPoint");

            if (production == null)
                errors.Add("Factory prefab is missing BuildingProduction.");

            if (obstacle == null)
            {
                errors.Add("Factory prefab is missing NavMeshObstacle.");
            }
            else
            {
                if (!obstacle.carving)
                    errors.Add("Factory NavMeshObstacle.carving should be enabled.");

                if (obstacle.shape != NavMeshObstacleShape.Box)
                    errors.Add("Factory NavMeshObstacle should be a box.");
            }

            if (exitPoint == null)
            {
                errors.Add("Factory prefab is missing UnitExitPoint.");
            }
            else if (obstacle != null)
            {
                ValidateFactoryExitClearance(factoryRoot.transform, obstacle, exitPoint, quadAgentRadius, errors);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(factoryRoot);
        }
    }

    /// <summary>
    /// Перевіряє, що UnitExitPoint заводу знаходиться щонайменше на два радіуси агента за межами
    /// NavMeshObstacle в горизонтальній площині, щоб юніти могли прокласти маршрут після спауну.
    /// </summary>
    private static void ValidateFactoryExitClearance(
        Transform factoryTransform,
        NavMeshObstacle obstacle,
        Transform exitPoint,
        float quadAgentRadius,
        List<string> errors)
    {
        SerializedObject serializedObstacle = new SerializedObject(obstacle);
        SerializedProperty extentsProperty = serializedObstacle.FindProperty("m_Extents");
        SerializedProperty centerProperty = serializedObstacle.FindProperty("m_Center");

        if (extentsProperty == null || centerProperty == null)
        {
            errors.Add("Factory NavMeshObstacle serialized bounds could not be read.");
            return;
        }

        Vector3 extents = extentsProperty.vector3Value;
        Vector3 center = centerProperty.vector3Value;
        Vector3 localExit = factoryTransform.InverseTransformPoint(exitPoint.position);
        Vector3 offset = localExit - center;
        float horizontalClearance = Mathf.Max(
            Mathf.Abs(offset.x) - extents.x,
            Mathf.Abs(offset.z) - extents.z);
        float requiredClearance = quadAgentRadius * 2f;

        if (horizontalClearance < requiredClearance)
        {
            errors.Add(
                "Factory UnitExitPoint should be outside the NavMeshObstacle by at least " +
                requiredClearance.ToString("0.00") + " units.");
        }
    }

    /// <summary>
    /// Перевіряє, що mainScene має NavMeshSurface з потрібною маскою шарів, прапорцями ігнорування
    /// та наявними запеченими даними NavMesh.
    /// </summary>
    private static void ValidateMainSceneNavigation(List<string> errors)
    {
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();

        if (surface == null)
        {
            errors.Add("mainScene is missing NavMeshSurface.");
            return;
        }

        SerializedObject serializedSurface = new SerializedObject(surface);
        SerializedProperty layerMask = serializedSurface.FindProperty("m_LayerMask");

        if (layerMask == null)
        {
            errors.Add("NavMeshSurface layer mask could not be read.");
        }
        else
        {
            int mask = layerMask.intValue;
            string[] requiredLayers = { "Ground", "PlayerUnit", "EnemyUnit", "Obstacle" };

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                int layer = LayerMask.NameToLayer(requiredLayers[i]);

                if (layer < 0)
                {
                    errors.Add("Required layer is missing: " + requiredLayers[i] + ".");
                    continue;
                }

                if ((mask & (1 << layer)) == 0)
                    errors.Add("NavMeshSurface layer mask should include " + requiredLayers[i] + ".");
            }
        }

        ValidateBool(serializedSurface, "m_IgnoreNavMeshAgent", true, errors);
        ValidateBool(serializedSurface, "m_IgnoreNavMeshObstacle", true, errors);
        ValidateObject(surface, "m_NavMeshData", errors);
    }

    /// <summary>Повертає об'єднану маску шарів, що охоплює всі шари, які має включати NavMeshSurface.</summary>
    private static int GetRequiredNavSurfaceMask()
    {
        return LayerMask.GetMask("Ground", "PlayerUnit", "EnemyUnit", "Obstacle");
    }

    /// <summary>
    /// Повертає дочірній об'єкт з іменем під <paramref name="gun"/> або створює новий GameObject,
    /// прив'язаний до <paramref name="gun"/>, зі зміщенням по локальній осі X від <paramref name="center"/>.
    /// Використовується для налаштування лівої/правої точки дула при стрільбі з двох стволів.
    /// </summary>
    private static Transform GetOrCreateMuzzle(
        Transform gun,
        Transform center,
        string objectName,
        float localX)
    {
        Transform existing = FindChildRecursive(gun, objectName);

        if (existing != null)
            return existing;

        GameObject muzzleObject = new GameObject(objectName);
        muzzleObject.layer = gun.gameObject.layer;
        muzzleObject.transform.SetParent(gun, false);
        muzzleObject.transform.localPosition =
            (center != null ? center.localPosition : Vector3.forward * 1.55f) + new Vector3(localX, 0f, 0f);
        muzzleObject.transform.localRotation = center != null ? center.localRotation : Quaternion.identity;
        muzzleObject.transform.localScale = Vector3.one;
        return muzzleObject.transform;
    }

    /// <summary>
    /// Рекурсивний пошук у глибину дочірнього Transform із заданою назвою.
    /// </summary>
    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);

            if (nested != null)
                return nested;
        }

        return null;
    }

    /// <summary>Встановлює властивість-посилання на Object у вже відкритому SerializedObject.</summary>
    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Встановлює властивість-масив Object точно до наданих значень, змінюючи розмір за потреби.
    /// </summary>
    private static void SetObjectArray(SerializedObject serializedObject, string propertyName, params Object[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    /// <summary>Встановлює властивість типу float у вже відкритому SerializedObject.</summary>
    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>Встановлює властивість типу int у вже відкритому SerializedObject.</summary>
    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>Встановлює властивість типу bool у вже відкритому SerializedObject.</summary>
    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>Встановлює властивість типу int (маска шарів) у вже відкритому SerializedObject.</summary>
    private static void SetLayerMask(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Перевіряє, що серіалізована властивість типу float на <paramref name="target"/> не менша
    /// за <paramref name="minimum"/>, додаючи описове повідомлення про помилку, якщо це не так.
    /// </summary>
    private static void ValidateFloatAtLeast(Object target, string propertyName, float minimum, List<string> errors)
    {
        if (target == null)
        {
            errors.Add(propertyName + " owner is missing.");
            return;
        }

        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

        if (property == null)
        {
            errors.Add(propertyName + " is missing.");
            return;
        }

        if (property.floatValue < minimum)
            errors.Add(propertyName + " should be at least " + minimum.ToString("0.##") + ".");
    }

    /// <summary>
    /// Перевіряє, що властивість типу bool у вже відкритому SerializedObject відповідає очікуваному значенню.
    /// </summary>
    private static void ValidateBool(
        SerializedObject serializedObject,
        string propertyName,
        bool expected,
        List<string> errors)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            errors.Add(propertyName + " is missing.");
            return;
        }

        if (property.boolValue != expected)
            errors.Add(propertyName + " should be " + expected + ".");
    }

    /// <summary>
    /// Перевіряє, що серіалізована властивість-посилання на Object у <paramref name="target"/> призначена.
    /// </summary>
    private static void ValidateObject(Object target, string propertyName, List<string> errors)
    {
        if (target == null)
        {
            errors.Add(propertyName + " owner is missing.");
            return;
        }

        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

        if (property == null || property.objectReferenceValue == null)
            errors.Add(propertyName + " is not assigned.");
    }

    /// <summary>
    /// Перевіряє, що серіалізований масив Object на <paramref name="target"/> має рівно
    /// <paramref name="expectedSize"/> ненульових записів.
    /// </summary>
    private static void ValidateObjectArray(
        Object target,
        string propertyName,
        int expectedSize,
        List<string> errors)
    {
        if (target == null)
        {
            errors.Add(propertyName + " owner is missing.");
            return;
        }

        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

        if (property == null || !property.isArray)
        {
            errors.Add(propertyName + " is missing or is not an array.");
            return;
        }

        if (property.arraySize != expectedSize)
        {
            errors.Add(propertyName + " should have " + expectedSize + " entries.");
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                errors.Add(propertyName + " has an empty entry at index " + i + ".");
        }
    }
}
