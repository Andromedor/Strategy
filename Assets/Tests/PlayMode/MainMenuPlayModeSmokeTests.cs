using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Strategy.Tests
{
    public sealed class MainMenuPlayModeSmokeTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string MainScenePath = "Assets/Scenes/mainScene.unity";
        private const string MainSceneName = "mainScene";
        private const string GameMenuControllerTypeName = "Strategy.Menu.GameMenuController, Assembly-CSharp";
        private const string MatchLaunchContextTypeName = "Strategy.Core.MatchLaunchContext, Assembly-CSharp";
        private const string BuildingHealthTypeName = "Strategy.Buildings.BuildingHealth, Assembly-CSharp";
        private const string AiControllerTypeName = "Strategy.AI.AiController, Assembly-CSharp";
        private const string InputSystemModuleTypeName = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        [UnityTest]
        public IEnumerator MainMenuSceneStartsWithEditableExpectedState()
        {
            yield return LoadMainMenu();

            Component controller = FindMenuController();
            Assert.NotNull(controller);
            Assert.NotNull(GameObject.Find("MainMenuCamera"), "Main menu scene should have a camera even before Play Mode layout code runs.");

            EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            Assert.NotNull(eventSystem);
            Type inputSystemModuleType = RequireType(InputSystemModuleTypeName);
            Assert.NotNull(eventSystem.GetComponent(inputSystemModuleType), "Main menu must use InputSystemUIInputModule.");
            Assert.IsNull(eventSystem.GetComponent<StandaloneInputModule>(), "StandaloneInputModule uses the old Input Manager and breaks this project.");

            AssertPanel(controller, "_mainPanel", true);
            AssertPanel(controller, "_skirmishModePanel", false);
            AssertPanel(controller, "_skirmishPanel", false);
            AssertPanel(controller, "_onlinePanel", false);
            AssertPanel(controller, "_loadPanel", false);
            AssertPanel(controller, "_settingsPanel", false);
        }

        [UnityTest]
        public IEnumerator MainMenuLocalButtonsNavigateWithoutErrors()
        {
            yield return LoadMainMenu();

            Component controller = FindMenuController();

            Click(GetButton(controller, "_settingsButton"));
            yield return null;
            AssertPanel(controller, "_settingsPanel", true);
            Click(GetButton(controller, "_applyResolutionButton"));
            yield return null;
            Assert.That(GetTextValue(controller, "_statusText"), Does.Contain("Resolution"));
            Click(GetButton(controller, "_backFromSettingsButton"));
            yield return null;
            AssertPanel(controller, "_mainPanel", true);

            Click(GetButton(controller, "_loadButton"));
            yield return null;
            AssertPanel(controller, "_loadPanel", true);
            Click(GetButton(controller, "_loadSaveButton"));
            yield return null;
            Assert.That(GetTextValue(controller, "_statusText"), Does.Contain("No save"));
            Click(GetButton(controller, "_backFromLoadButton"));
            yield return null;
            AssertPanel(controller, "_mainPanel", true);

            Click(GetButton(controller, "_skirmishButton"));
            yield return null;
            AssertPanel(controller, "_skirmishModePanel", true);
            Click(GetButton(controller, "_backFromModeButton"));
            yield return null;
            AssertPanel(controller, "_mainPanel", true);

            Click(GetButton(controller, "_skirmishButton"));
            yield return null;
            Click(GetButton(controller, "_offlineBotsButton"));
            yield return null;
            AssertPanel(controller, "_skirmishPanel", true);
            Assert.IsTrue(GetButton(controller, "_startOfflineButton").gameObject.activeSelf);
            Assert.IsFalse(GetButton(controller, "_hostOnlineButton").gameObject.activeSelf);
            Click(GetButton(controller, "_backFromSkirmishButton"));
            yield return null;
            AssertPanel(controller, "_skirmishModePanel", true);

            Click(GetButton(controller, "_onlineButton"));
            yield return null;
            AssertPanel(controller, "_onlinePanel", true);
            Click(GetButton(controller, "_joinOnlineButton"));
            yield return null;
            Assert.That(GetTextValue(controller, "_statusText"), Does.Contain("Enter join code"));
            Click(GetButton(controller, "_openOnlineHostSetupButton"));
            yield return null;
            AssertPanel(controller, "_skirmishPanel", true);
            Assert.IsFalse(GetButton(controller, "_startOfflineButton").gameObject.activeSelf);
            Assert.IsTrue(GetButton(controller, "_hostOnlineButton").gameObject.activeSelf);
            Click(GetButton(controller, "_backFromSkirmishButton"));
            yield return null;
            AssertPanel(controller, "_skirmishModePanel", true);
        }

        [UnityTest]
        public IEnumerator OfflineBotSkirmishLaunchesFromMenuAndSpawnsMatch()
        {
            yield return LoadMainMenu();

            Component controller = FindMenuController();
            Click(GetButton(controller, "_skirmishButton"));
            yield return null;
            Click(GetButton(controller, "_offlineBotsButton"));
            yield return null;
            Click(GetButton(controller, "_startOfflineButton"));

            float deadline = Time.realtimeSinceStartup + 25f;
            while (SceneManager.GetActiveScene().name != MainSceneName && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(MainSceneName, SceneManager.GetActiveScene().name);

            for (int i = 0; i < 30; i++)
                yield return null;

            Assert.GreaterOrEqual(CountStaticList(BuildingHealthTypeName, "All"), 2, "A 1v1 bot match should spawn at least two starting bases.");
            Assert.GreaterOrEqual(CountActiveComponents(AiControllerTypeName), 1, "A bot match should create at least one AI controller.");
        }

        [UnityTest]
        public IEnumerator GameplaySceneHasPauseToastMinimapAndBoundary()
        {
            ClearLaunchContext();
            AsyncOperation operation = SceneManager.LoadSceneAsync(MainScenePath, LoadSceneMode.Single);
            Assert.NotNull(operation, "mainScene is not available in Build Settings.");
            while (!operation.isDone)
                yield return null;

            yield return null;

            GameObject pauseMenu = GameObject.Find("InGamePauseMenu");
            Assert.NotNull(pauseMenu);
            CanvasGroup pauseGroup = pauseMenu.GetComponent<CanvasGroup>();
            Assert.NotNull(pauseGroup);
            Assert.AreEqual(0f, pauseGroup.alpha, 0.01f);
            Assert.IsFalse(pauseGroup.blocksRaycasts);

            GameObject toast = GameObject.Find("SaveGameToast");
            Assert.NotNull(toast);
            CanvasGroup toastGroup = toast.GetComponent<CanvasGroup>();
            Assert.NotNull(toastGroup);
            Assert.AreEqual(0f, toastGroup.alpha, 0.01f);
            Assert.IsFalse(toastGroup.blocksRaycasts);

            GameObject minimapView = GameObject.Find("MinimapView");
            Assert.NotNull(minimapView);
            RawImage rawImage = minimapView.GetComponent<RawImage>();
            Assert.NotNull(rawImage);
            Assert.NotNull(rawImage.texture);

            GameObject minimapCameraObject = GameObject.Find("MinimapCamera");
            Assert.NotNull(minimapCameraObject);
            Camera minimapCamera = minimapCameraObject.GetComponent<Camera>();
            Assert.NotNull(minimapCamera);
            Assert.IsTrue(minimapCamera.orthographic);
            Assert.AreEqual(rawImage.texture, minimapCamera.targetTexture);

            GameObject boundary = GameObject.Find("MapBoundary");
            Assert.NotNull(boundary);
            Assert.NotNull(GameObject.Find("RedBorder")?.GetComponent<LineRenderer>());
            Assert.NotNull(GameObject.Find("OutsideNorth"));
            Assert.NotNull(GameObject.Find("OutsideSouth"));
            Assert.NotNull(GameObject.Find("OutsideEast"));
            Assert.NotNull(GameObject.Find("OutsideWest"));
        }

        private static IEnumerator LoadMainMenu()
        {
            ClearLaunchContext();
            AsyncOperation operation = SceneManager.LoadSceneAsync(MainMenuScenePath, LoadSceneMode.Single);
            Assert.NotNull(operation, "MainMenu scene is not available in Build Settings.");
            while (!operation.isDone)
                yield return null;

            yield return null;
        }

        private static Component FindMenuController()
        {
            Type type = RequireType(GameMenuControllerTypeName);
            GameObject root = GameObject.Find("Root");
            Assert.NotNull(root, "MainMenu Root object is missing.");
            Component controller = root.GetComponent(type);
            Assert.NotNull(controller, "GameMenuController is missing on Root.");
            return controller;
        }

        private static void ClearLaunchContext()
        {
            Type type = Type.GetType(MatchLaunchContextTypeName);
            type?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        }

        private static void AssertPanel(Component controller, string fieldName, bool active)
        {
            GameObject panel = GetField<GameObject>(controller, fieldName);
            Assert.NotNull(panel, fieldName);
            Assert.AreEqual(active, panel.activeSelf, fieldName);
        }

        private static Button GetButton(Component controller, string fieldName)
        {
            Button button = GetField<Button>(controller, fieldName);
            Assert.NotNull(button, fieldName);
            return button;
        }

        private static string GetTextValue(Component controller, string fieldName)
        {
            object text = GetField<UnityEngine.Object>(controller, fieldName);
            Assert.NotNull(text, fieldName);
            PropertyInfo property = text.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property, fieldName + ".text");
            return property.GetValue(text) as string;
        }

        private static T GetField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return field.GetValue(target) as T;
        }

        private static void Click(Button button)
        {
            Assert.NotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name);
            button.onClick.Invoke();
        }

        private static int CountStaticList(string typeName, string propertyOrFieldName)
        {
            Type type = RequireType(typeName);
            object value = type.GetField(propertyOrFieldName, BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            if (value == null)
                value = type.GetProperty(propertyOrFieldName, BindingFlags.Static | BindingFlags.Public)?.GetValue(null);

            Assert.NotNull(value, propertyOrFieldName);
            int count = 0;
            foreach (object item in (IEnumerable)value)
            {
                if (item != null)
                    count++;
            }

            return count;
        }

        private static int CountActiveComponents(string typeName)
        {
            Type type = RequireType(typeName);
            int count = 0;
            foreach (UnityEngine.Object item in Resources.FindObjectsOfTypeAll(type))
            {
                if (item is Component component && component.gameObject.scene.IsValid() && component.gameObject.activeInHierarchy)
                    count++;
            }

            return count;
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.NotNull(type, "Missing type " + typeName);
            return type;
        }
    }
}
