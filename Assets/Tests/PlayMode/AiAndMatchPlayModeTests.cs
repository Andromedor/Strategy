using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Strategy.Tests
{
    public class AiAndMatchPlayModeTests
    {
        private const string AiDifficultyProfileTypeName = "Strategy.AI.AiDifficultyProfile, Assembly-CSharp";
        private const string AiDifficultyLevelTypeName = "Strategy.AI.AiDifficultyLevel, Assembly-CSharp";
        private const string AiRuntimeSettingsTypeName = "Strategy.AI.AiRuntimeSettings, Assembly-CSharp";
        private const string BuildingDataTypeName = "Strategy.Data.BuildingData, Assembly-CSharp";
        private const string BuildingConstructionStateTypeName = "Strategy.Buildings.BuildingConstructionState, Assembly-CSharp";
        private const string BuildingHealthTypeName = "Strategy.Buildings.BuildingHealth, Assembly-CSharp";
        private const string BuildingProductionTypeName = "Strategy.Buildings.BuildingProduction, Assembly-CSharp";
        private const string ConstructionCenterTypeName = "Strategy.Buildings.ConstructionCenter, Assembly-CSharp";
        private const string ConstructionPanelUITypeName = "Strategy.UI.ConstructionPanelUI, Assembly-CSharp";
        private const string MatchVictorySystemTypeName = "Strategy.Core.MatchVictorySystem, Assembly-CSharp";
        private const string MatchTeamSettingsTypeName = "Strategy.Core.MatchTeamSettings, Assembly-CSharp";
        private const string PlayerCommandTypeName = "Strategy.Core.PlayerCommand, Assembly-CSharp";
        private const string PlayerCommandExecutorTypeName = "Strategy.Core.PlayerCommandExecutor, Assembly-CSharp";
        private const string ProductionConfigTypeName = "Strategy.Data.ProductionConfig, Assembly-CSharp";
        private const string ProductionItemDataTypeName = "Strategy.Data.ProductionItemData, Assembly-CSharp";
        private const string ResourceManagerTypeName = "Strategy.Core.ResourceManager, Assembly-CSharp";
        private const string TeamComponentTypeName = "Strategy.Units.TeamComponent, Assembly-CSharp";
        private const string TeamTypeName = "Strategy.Units.TeamType, Assembly-CSharp";
        private const string UnitDataTypeName = "Strategy.Data.UnitData, Assembly-CSharp";
        private const string OutpostTypeName = "Strategy.Buildings.Outpost, Assembly-CSharp";

        [Test]
        public void RuntimeDifficultyDefaultsUsePlannedAttackGroupSizes()
        {
            Type profileType = RequireType(AiDifficultyProfileTypeName);
            Type levelType = RequireType(AiDifficultyLevelTypeName);
            MethodInfo factory = profileType.GetMethod("CreateRuntimeDefault", BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(factory);

            ScriptableObject easy = (ScriptableObject)factory.Invoke(null, new[] { Enum.Parse(levelType, "Easy") });
            ScriptableObject medium = (ScriptableObject)factory.Invoke(null, new[] { Enum.Parse(levelType, "Medium") });
            ScriptableObject hard = (ScriptableObject)factory.Invoke(null, new[] { Enum.Parse(levelType, "Hard") });

            Assert.AreEqual(4, GetPropertyValue<int>(easy, "AttackGroupSize"));
            Assert.AreEqual(6, GetPropertyValue<int>(medium, "AttackGroupSize"));
            Assert.AreEqual(8, GetPropertyValue<int>(hard, "AttackGroupSize"));

            UnityEngine.Object.DestroyImmediate(easy);
            UnityEngine.Object.DestroyImmediate(medium);
            UnityEngine.Object.DestroyImmediate(hard);
        }

        [Test]
        public void AiRuntimeToggleCanDisableAndEnableAi()
        {
            Type settingsType = RequireType(AiRuntimeSettingsTypeName);
            MethodInfo setEnabled = settingsType.GetMethod("SetAllAiEnabled", BindingFlags.Static | BindingFlags.Public);
            PropertyInfo isEnabled = settingsType.GetProperty("IsAiEnabled", BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(setEnabled);
            Assert.NotNull(isEnabled);

            setEnabled.Invoke(null, new object[] { false });
            Assert.IsFalse((bool)isEnabled.GetValue(null));

            setEnabled.Invoke(null, new object[] { true });
            Assert.IsTrue((bool)isEnabled.GetValue(null));
        }

        [Test]
        public void OutpostCanBeCapturedByTeam3()
        {
            Type outpostType = RequireType(OutpostTypeName);
            Type teamType = RequireType(TeamTypeName);
            object team3 = Enum.Parse(teamType, "Team3");
            GameObject outpostObject = new("Team3 Capture Outpost");
            Component outpost = outpostObject.AddComponent(outpostType);
            SetField(outpost, "_captureTime", 0.25f);
            InvokeVoid(outpost, "Awake");

            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(teamType, typeof(int));
            object counts = Activator.CreateInstance(dictionaryType);
            dictionaryType.GetMethod("Add")?.Invoke(counts, new[] { team3, 1 });

            MethodInfo tickCapture = null;
            foreach (MethodInfo method in outpostType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name == "TickCapture" && method.GetParameters().Length == 3)
                {
                    tickCapture = method;
                    break;
                }
            }

            Assert.NotNull(tickCapture);
            tickCapture.Invoke(outpost, new[] { counts, false, 0.3f });

            object owner = GetPropertyValue<object>(outpost, "Owner");
            Assert.AreEqual(team3, owner);

            UnityEngine.Object.DestroyImmediate(outpostObject);
        }

        [UnityTest]
        public IEnumerator EnemyProductionCommandSpendsEnemyResources()
        {
            Type resourceType = RequireType(ResourceManagerTypeName);
            Type teamType = RequireType(TeamTypeName);
            Type teamComponentType = RequireType(TeamComponentTypeName);
            Type productionType = RequireType(BuildingProductionTypeName);
            Type configType = RequireType(ProductionConfigTypeName);
            Type itemType = RequireType(ProductionItemDataTypeName);
            Type unitDataType = RequireType(UnitDataTypeName);
            Type commandType = RequireType(PlayerCommandTypeName);
            Type executorType = RequireType(PlayerCommandExecutorTypeName);

            object enemy = Enum.Parse(teamType, "Enemy");
            GameObject resourceObject = new("Test Resource Manager");
            Component resource = resourceObject.AddComponent(resourceType);
            InvokeVoid(resource, "SetResource", enemy, 500);

            GameObject unitPrefab = new("AI Unit Prefab");
            ScriptableObject unitData = ScriptableObject.CreateInstance(unitDataType);
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
                "AI Test Unit",
                null,
                null);

            ScriptableObject item = ScriptableObject.CreateInstance(itemType);
            InvokeVoid(item, "Configure", "AI Test Unit", unitData, 120, 10f, null);

            ScriptableObject config = ScriptableObject.CreateInstance(configType);
            InvokeVoid(config, "AddItem", item);

            GameObject factoryObject = new("Enemy Test Factory");
            Component team = factoryObject.AddComponent(teamComponentType);
            InvokeVoid(team, "SetTeam", enemy);
            Component factory = factoryObject.AddComponent(productionType);
            Transform spawn = new GameObject("Spawn").transform;
            spawn.SetParent(factoryObject.transform);
            SetField(factory, "_unitSpawnPoint", spawn);
            SetField(factory, "_productionConfig", config);

            MethodInfo produceFactory = commandType.GetMethod("ProduceUnit", BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(produceFactory);
            object command = produceFactory.Invoke(null, new object[] { enemy, 1, new[] { factoryObject }, item });
            executorType.GetMethod("Execute", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new[] { command });

            Assert.AreEqual(380, InvokeInt(resource, "GetResource", enemy));

            UnityEngine.Object.Destroy(factoryObject);
            UnityEngine.Object.Destroy(unitPrefab);
            UnityEngine.Object.Destroy(unitData);
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(config);
            UnityEngine.Object.Destroy(resourceObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingConstructionTimerBlocksFactoryAndBuildAreaUntilComplete()
        {
            Type stateType = RequireType(BuildingConstructionStateTypeName);
            Type productionType = RequireType(BuildingProductionTypeName);
            Type constructionCenterType = RequireType(ConstructionCenterTypeName);
            Type buildingDataType = RequireType(BuildingDataTypeName);

            GameObject buildingObject = new("Construction Timer Test Building");
            Behaviour production = buildingObject.AddComponent(productionType) as Behaviour;
            Behaviour constructionCenter = buildingObject.AddComponent(constructionCenterType) as Behaviour;
            Component state = buildingObject.AddComponent(stateType);
            ScriptableObject buildingData = ScriptableObject.CreateInstance(buildingDataType);
            SetField(buildingData, "_buildTime", 0.05f);

            InvokeVoid(state, "Begin", buildingData);

            Assert.IsFalse(production.enabled);
            Assert.IsFalse(constructionCenter.enabled);
            Assert.IsTrue(GetPropertyValue<bool>(state, "IsUnderConstruction"));

            yield return new WaitForSeconds(0.08f);

            Assert.IsFalse(GetPropertyValue<bool>(state, "IsUnderConstruction"));
            Assert.IsTrue(production.enabled);
            Assert.IsTrue(constructionCenter.enabled);

            UnityEngine.Object.DestroyImmediate(buildingData);
            UnityEngine.Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void MatchVictorySystemReportsVictoryWhenOnlyLocalBuildingsRemain()
        {
            Type victoryType = RequireType(MatchVictorySystemTypeName);
            Type matchTeamSettingsType = RequireType(MatchTeamSettingsTypeName);
            Type buildingHealthType = RequireType(BuildingHealthTypeName);
            Type teamComponentType = RequireType(TeamComponentTypeName);
            Type teamType = RequireType(TeamTypeName);
            object player = Enum.Parse(teamType, "Player");

            GameObject settingsObject = new("Match Team Settings");
            settingsObject.AddComponent(matchTeamSettingsType);

            GameObject buildingObject = new("Local Building");
            Component team = buildingObject.AddComponent(teamComponentType);
            InvokeVoid(team, "SetTeam", player);
            buildingObject.AddComponent(buildingHealthType);

            GameObject systemObject = new("Victory System");
            Component victorySystem = systemObject.AddComponent(victoryType);
            SetField(victorySystem, "_startEvaluationDelay", 0f);
            SetField(victorySystem, "_startTime", -10f);
            SetField(victorySystem, "_hasSeenCompleteStartingState", true);

            InvokeVoid(victorySystem, "Evaluate");
            Assert.IsTrue(GetFieldValue<bool>(victorySystem, "_isEnded"));

            UnityEngine.Object.DestroyImmediate(systemObject);
            UnityEngine.Object.DestroyImmediate(buildingObject);
            UnityEngine.Object.DestroyImmediate(settingsObject);
        }

        [Test]
        public void MatchVictorySystemWaitsForExpectedEnemyBaseBeforeDeclaringVictory()
        {
            Type victoryType = RequireType(MatchVictorySystemTypeName);
            Type matchTeamSettingsType = RequireType(MatchTeamSettingsTypeName);
            Type buildingHealthType = RequireType(BuildingHealthTypeName);
            Type teamComponentType = RequireType(TeamComponentTypeName);
            Type teamType = RequireType(TeamTypeName);
            object player = Enum.Parse(teamType, "Player");

            GameObject settingsObject = new("Match Team Settings");
            settingsObject.AddComponent(matchTeamSettingsType);

            GameObject localBuilding = new("Local Building");
            Component team = localBuilding.AddComponent(teamComponentType);
            InvokeVoid(team, "SetTeam", player);
            localBuilding.AddComponent(buildingHealthType);

            GameObject systemObject = new("Victory System");
            Component victorySystem = systemObject.AddComponent(victoryType);
            SetField(victorySystem, "_startEvaluationDelay", 0f);
            SetField(victorySystem, "_startTime", -10f);

            InvokeVoid(victorySystem, "Evaluate");
            Assert.IsFalse(GetFieldValue<bool>(victorySystem, "_isEnded"));

            UnityEngine.Object.DestroyImmediate(systemObject);
            UnityEngine.Object.DestroyImmediate(localBuilding);
            UnityEngine.Object.DestroyImmediate(settingsObject);
        }

        [Test]
        public void ConstructionPanelDisablesBuildButtonWhenResourcesAreInsufficient()
        {
            Type panelType = RequireType(ConstructionPanelUITypeName);
            Type buildingDataType = RequireType(BuildingDataTypeName);
            Type constructionCenterType = RequireType(ConstructionCenterTypeName);
            Type resourceType = RequireType(ResourceManagerTypeName);
            Type teamComponentType = RequireType(TeamComponentTypeName);
            Type teamType = RequireType(TeamTypeName);
            object player = Enum.Parse(teamType, "Player");

            GameObject resourceObject = new("Resource Manager");
            Component resources = resourceObject.AddComponent(resourceType);
            InvokeVoid(resources, "SetResource", player, 230);

            GameObject centerObject = new("Construction Center");
            Component team = centerObject.AddComponent(teamComponentType);
            InvokeVoid(team, "SetTeam", player);
            centerObject.AddComponent(constructionCenterType);

            ScriptableObject building = ScriptableObject.CreateInstance(buildingDataType);
            SetField(building, "_buildingName", "Heavy Factory");
            SetField(building, "_economyCost", 250);

            Type buildingListType = typeof(List<>).MakeGenericType(buildingDataType);
            IList buildings = (IList)Activator.CreateInstance(buildingListType);
            buildings.Add(building);

            GameObject contentObject = new("Construction Content", typeof(RectTransform));
            GameObject panelObject = new("Construction Panel");
            panelObject.SetActive(false);
            Component panel = panelObject.AddComponent(panelType);
            SetField(panel, "_contentRoot", contentObject.transform);
            SetField(panel, "_buildings", buildings);
            SetField(panel, "_team", player);
            panelObject.SetActive(true);

            Button button = contentObject.GetComponentInChildren<Button>();
            Assert.NotNull(button);
            Assert.IsFalse(button.interactable);

            InvokeVoid(resources, "SetResource", player, 300);
            Assert.IsTrue(button.interactable);

            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(contentObject);
            UnityEngine.Object.DestroyImmediate(building);
            UnityEngine.Object.DestroyImmediate(centerObject);
            UnityEngine.Object.DestroyImmediate(resourceObject);
        }

#if UNITY_EDITOR
        [Test]
        public void BalanceAssetsHaveExpectedCosts()
        {
            ScriptableObject mediumTank = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Balance/MeadleTankProduction.asset");
            ScriptableObject factory = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Balance/HeavyFactory.asset");
            ScriptableObject militaryBase = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Balance/MilitaryBase.asset");

            Assert.NotNull(mediumTank);
            Assert.NotNull(factory);
            Assert.NotNull(militaryBase);
            Assert.AreEqual(120, GetPropertyValue<int>(mediumTank, "Cost"));
            Assert.AreEqual(250, GetPropertyValue<int>(factory, "EconomyCost"));
            Assert.AreEqual(600, GetPropertyValue<int>(militaryBase, "EconomyCost"));
            Assert.AreEqual(10f, GetPropertyValue<float>(factory, "BuildTime"));
            Assert.AreEqual(10f, GetPropertyValue<float>(militaryBase, "BuildTime"));
        }

        [Test]
        public void ProductionAndBuildingAssetsHaveNonZeroCosts()
        {
            Type buildingDataType = RequireType(BuildingDataTypeName);
            Type productionItemType = RequireType(ProductionItemDataTypeName);
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Balance" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset == null)
                    continue;

                if (productionItemType.IsInstanceOfType(asset))
                {
                    Assert.Greater(GetPropertyValue<int>(asset, "Cost"), 0, path);
                    continue;
                }

                if (buildingDataType.IsInstanceOfType(asset))
                    Assert.Greater(GetPropertyValue<int>(asset, "EconomyCost"), 0, path);
            }
        }
#endif

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.NotNull(type, "Missing type " + typeName);
            return type;
        }

        private static void InvokeVoid(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing method {methodName} on {target.GetType().Name}");
            method.Invoke(target, args);
        }

        private static int InvokeInt(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing method {methodName} on {target.GetType().Name}");
            return (int)method.Invoke(target, args);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Missing property {propertyName} on {target.GetType().Name}");
            return (T)property.GetValue(target);
        }

        private static T GetFieldValue<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            return (T)field.GetValue(target);
        }
    }
}
