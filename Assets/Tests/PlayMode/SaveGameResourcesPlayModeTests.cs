using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Strategy.Tests
{
    public class SaveGameResourcesPlayModeTests
    {
        private const string GameAssetRegistryTypeName = "Strategy.Data.GameAssetRegistry, Assembly-CSharp";
        private const string MapCatalogTypeName = "Strategy.Maps.MapCatalog, Assembly-CSharp";
        private const string SaveGameFileIOTypeName = "Strategy.Save.SaveGameFileIO, Assembly-CSharp";
        private const string BuildingHealthTypeName = "Strategy.Buildings.BuildingHealth, Assembly-CSharp";
        private const string BuildingConstructionStateTypeName = "Strategy.Buildings.BuildingConstructionState, Assembly-CSharp";

        [Test]
        public void SaveFallbackRegistryAssetsAreAvailableFromResources()
        {
            Type registryType = RequireType(GameAssetRegistryTypeName);
            Type mapCatalogType = RequireType(MapCatalogTypeName);

            Assert.NotNull(Resources.Load("GameAssetRegistry", registryType));
            Assert.NotNull(Resources.Load("MapCatalog", mapCatalogType));
        }

        [Test]
        public void SaveDisplayNameUsesFriendlyIndexAndLocalDate()
        {
            string path = Path.Combine(Application.temporaryCachePath, "save_display_test.json");
            File.WriteAllText(path, "{\"savedAtUtc\":\"2026-06-12 19:24:18Z\",\"mapId\":\"main_scene\"}");

            try
            {
                Type fileIoType = RequireType(SaveGameFileIOTypeName);
                MethodInfo method = fileIoType.GetMethod(
                    "GetDisplayName",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(string), RequireType(MapCatalogTypeName), typeof(int) },
                    null);
                Assert.NotNull(method);

                string displayName = method.Invoke(null, new object[] { path, null, 0 }) as string;
                StringAssert.StartsWith("Сейв 1", displayName);
                StringAssert.Contains("12.06.2026", displayName);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void BuildingConstructionRestorePreservesReadyAndUnderConstructionHealth()
        {
            GameObject readyBuilding = new("Ready Building");
            try
            {
                Component readyHealth = readyBuilding.AddComponent(RequireType(BuildingHealthTypeName));
                Component readyState = readyBuilding.AddComponent(RequireType(BuildingConstructionStateTypeName));

                InvokeRestoreForLoad(readyState, false, 0f, 0f, 777f);

                Assert.IsFalse(GetProperty<bool>(readyState, "IsUnderConstruction"));
                Assert.AreEqual(777f, GetProperty<float>(readyHealth, "CurrentHealth"), 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readyBuilding);
            }

            GameObject constructionBuilding = new("Construction Building");
            try
            {
                Component constructionHealth = constructionBuilding.AddComponent(RequireType(BuildingHealthTypeName));
                Component constructionState = constructionBuilding.AddComponent(RequireType(BuildingConstructionStateTypeName));

                InvokeRestoreForLoad(constructionState, true, 3f, 10f, 150f);

                Assert.IsTrue(GetProperty<bool>(constructionState, "IsUnderConstruction"));
                Assert.AreEqual(0.3f, GetProperty<float>(constructionState, "Progress"), 0.01f);
                Assert.AreEqual(150f, GetProperty<float>(constructionHealth, "CurrentHealth"), 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(constructionBuilding);
            }
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.NotNull(type, $"Missing runtime type: {typeName}");
            return type;
        }

        private static void InvokeRestoreForLoad(
            Component state,
            bool isUnderConstruction,
            float elapsedSeconds,
            float durationSeconds,
            float currentHealth)
        {
            Type buildingDataType = RequireType("Strategy.Data.BuildingData, Assembly-CSharp");
            MethodInfo method = state.GetType().GetMethod(
                "RestoreForLoad",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { buildingDataType, typeof(bool), typeof(float), typeof(float), typeof(float) },
                null);
            Assert.NotNull(method);
            method.Invoke(state, new object[] { null, isUnderConstruction, elapsedSeconds, durationSeconds, currentHealth });
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property, propertyName);
            return (T)property.GetValue(target);
        }
    }
}
