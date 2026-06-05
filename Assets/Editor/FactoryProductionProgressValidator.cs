using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Strategy.Buildings;
using Strategy.Core;
using Strategy.Data;
using Strategy.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Strategy.EditorTools
{
    public static class FactoryProductionProgressValidator
    {
        private static IEnumerator _routine;
        private static string _resultPath;
        private static bool _previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions _previousEnterPlayModeOptions;

        public static void Run()
        {
            _resultPath = Path.Combine(Application.dataPath, "../Logs/FactoryProductionProgressValidation.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
            File.WriteAllText(_resultPath, "Factory production progress validation started.\n");

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
                    Finish(0, "Factory production progress validation passed.");
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

            GameObject cameraObject = null;
            GameObject statusBar = null;
            GameObject buttonObject = null;
            GameObject unitPrefab = null;
            UnitData unitData = null;
            ProductionItemData item = null;
            ProductionConfig config = null;
            BuildingProduction firstFactory = null;
            BuildingProduction secondFactory = null;
            BuildingProduction presenterFactory = null;

            try
            {
                unitPrefab = new GameObject("Validator Unit Prefab");
                unitData = ScriptableObject.CreateInstance<UnitData>();
                unitData.Configure(
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
                    "Validator Tank");

                item = ScriptableObject.CreateInstance<ProductionItemData>();
                item.Configure("Validator Tank", unitData, 0, 0.8f);

                config = ScriptableObject.CreateInstance<ProductionConfig>();
                config.AddItem(item);

                firstFactory = CreateFactory("Validator Factory A", config);
                secondFactory = CreateFactory("Validator Factory B", config);

                if (!firstFactory.AddToQueue(item))
                    throw new InvalidOperationException("First factory rejected a valid production item.");

                yield return null;

                if (!firstFactory.TryGetCurrentProduction(out FactoryProductionRuntimeState firstState))
                    throw new InvalidOperationException("Factory did not expose active production state.");

                float startProgress = firstState.Progress;
                float startRemaining = firstState.RemainingSeconds;

                yield return WaitRealtime(0.18f);

                if (!firstFactory.TryGetCurrentProduction(out FactoryProductionRuntimeState advancedState))
                    throw new InvalidOperationException("Factory production state disappeared before completion.");

                if (advancedState.Progress <= startProgress)
                    throw new InvalidOperationException("Factory production progress did not advance.");

                if (advancedState.RemainingSeconds >= startRemaining)
                    throw new InvalidOperationException("Factory remaining time did not decrease.");

                if (!secondFactory.AddToQueue(item))
                    throw new InvalidOperationException("Second factory rejected a valid production item.");

                yield return null;

                ProductionButtonRuntimeState aggregateState =
                    ProductionButtonStateAggregator.Build(new[] { firstFactory, secondFactory }, item);

                if (aggregateState.PendingCount != 2 || !aggregateState.HasActiveProgress)
                    throw new InvalidOperationException(
                        $"Invalid aggregate state. Count={aggregateState.PendingCount}, active={aggregateState.HasActiveProgress}");

                if (!firstFactory.TryGetActiveProductionFor(item, out FactoryProductionRuntimeState fastestState) ||
                    !secondFactory.TryGetActiveProductionFor(item, out FactoryProductionRuntimeState slowerState) ||
                    fastestState.RemainingSeconds >= slowerState.RemainingSeconds)
                {
                    throw new InvalidOperationException("Factories did not report the expected fastest active production.");
                }

                if (Mathf.Abs(aggregateState.RemainingSeconds - fastestState.RemainingSeconds) > 0.08f)
                    throw new InvalidOperationException("Aggregator did not use the fastest factory timer.");

                buttonObject = CreateProductionButtonObject(out ProductionButtonUI button, out GameObject badgeRoot, out Component badgeText, out GameObject progressRoot, out Image progressFill);
                button.Initialize(item, null);
                button.SetProductionState(new ProductionButtonRuntimeState(3, true, 0.35f, 1.2f));

                if (!badgeRoot.activeSelf || GetText(badgeText) != "3")
                    throw new InvalidOperationException("Production button queue badge did not show the aggregate count.");

                if (!progressRoot.activeSelf ||
                    Mathf.Abs(progressFill.fillAmount - 0.35f) > 0.01f ||
                    Mathf.Abs(progressFill.rectTransform.anchorMax.x - 0.35f) > 0.01f)
                {
                    throw new InvalidOperationException("Production button progress strip did not update.");
                }

                cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();

                presenterFactory = CreateFactory("Presenter Factory", config);
                statusBar = CreateFactoryStatusBarObject(presenterFactory.transform, out Image statusFill);
                presenterFactory.gameObject.AddComponent<BuildingSelectionState>();
                FactoryProductionStatusPresenter presenter =
                    presenterFactory.gameObject.AddComponent<FactoryProductionStatusPresenter>();
                SetField(presenter, "_statusBarRoot", statusBar);
                SetField(presenter, "_trackRect", statusBar.transform.Find("Track").GetComponent<RectTransform>());
                SetField(presenter, "_fillRect", statusFill.rectTransform);
                SetField(presenter, "_fillImage", statusFill);

                EventManager.RaiseBuildingSelected(presenterFactory.gameObject);
                yield return null;

                if (statusBar.activeSelf)
                    throw new InvalidOperationException("Factory production world bar was visible while factory was idle.");

                if (!presenterFactory.AddToQueue(item))
                    throw new InvalidOperationException("Presenter factory rejected a valid production item.");

                yield return null;

                if (!statusBar.activeSelf)
                    throw new InvalidOperationException("Factory production world bar was not visible while selected and producing.");

                if (statusFill.fillAmount <= 0f || statusFill.rectTransform.anchorMax.x <= 0f)
                    throw new InvalidOperationException("Factory production world bar fill did not update.");

                if (Quaternion.Angle(Quaternion.Euler(new Vector3(-90f, 0f, 0f)), statusBar.transform.localRotation) > 0.1f)
                    throw new InvalidOperationException("Factory production world bar should keep the authored local rotation.");

                EventManager.RaiseBuildingDeselected(presenterFactory.gameObject);
                yield return null;

                if (statusBar.activeSelf)
                    throw new InvalidOperationException("Factory production world bar stayed visible after deselect.");
            }
            finally
            {
                DestroyRuntimeObjects();
                DestroyObject(buttonObject);
                DestroyObject(statusBar);
                DestroyObject(cameraObject);
                DestroyFactory(firstFactory);
                DestroyFactory(secondFactory);
                DestroyFactory(presenterFactory);
                DestroyObject(unitPrefab);
                DestroyObject(unitData);
                DestroyObject(item);
                DestroyObject(config);
            }
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float endTime = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < endTime)
                yield return null;
        }

        private static BuildingProduction CreateFactory(string name, ProductionConfig config)
        {
            GameObject factoryObject = new GameObject(name);
            BuildingProduction factory = factoryObject.AddComponent<BuildingProduction>();
            Transform spawnPoint = new GameObject(name + " Spawn").transform;
            spawnPoint.SetParent(factoryObject.transform, false);
            SetField(factory, "_unitSpawnPoint", spawnPoint);
            SetField(factory, "_productionConfig", config);
            return factory;
        }

        private static GameObject CreateProductionButtonObject(
            out ProductionButtonUI button,
            out GameObject badgeRoot,
            out Component badgeText,
            out GameObject progressRoot,
            out Image progressFill)
        {
            Type textMeshProType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (textMeshProType == null)
                throw new InvalidOperationException("TextMeshProUGUI type was not available.");

            GameObject root = new GameObject(
                "Production Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(ProductionButtonUI));
            button = root.GetComponent<ProductionButtonUI>();

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
                textMeshProType);
            badgeTextObject.transform.SetParent(badgeRoot.transform, false);
            badgeText = badgeTextObject.GetComponent(textMeshProType);

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
            return root;
        }

        private static GameObject CreateFactoryStatusBarObject(Transform parent, out Image fillImage)
        {
            GameObject root = new GameObject(
                "FactoryProductionStatusBar",
                typeof(RectTransform),
                typeof(Canvas));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
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

        private static string GetText(Component textComponent)
        {
            PropertyInfo property = textComponent.GetType().GetProperty("text");
            return property != null ? (string)property.GetValue(textComponent) : string.Empty;
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

        private static void DestroyFactory(BuildingProduction factory)
        {
            if (factory != null)
                DestroyObject(factory.gameObject);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyObject(GameObject.Find("Runtime Objects"));
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target != null)
                UnityEngine.Object.Destroy(target);
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
