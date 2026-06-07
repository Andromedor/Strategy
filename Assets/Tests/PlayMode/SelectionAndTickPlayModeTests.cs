using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Strategy.Tests
{
    public class SelectionAndTickPlayModeTests
    {
        private const string SelectionDragBoxUiTypeName = "Strategy.UI.SelectionDragBoxUI, Assembly-CSharp";
        private const string PlayerCommandTypeName = "Strategy.Core.PlayerCommand, Assembly-CSharp";
        private const string CommandDispatcherTypeName = "Strategy.Core.CommandDispatcher, Assembly-CSharp";
        private const string TeamTypeName = "Strategy.Units.TeamType, Assembly-CSharp";
        private const string GameTickConfigTypeName = "Strategy.Core.GameTickConfig, Assembly-CSharp";

        [UnityTest]
        public IEnumerator SelectionDragBoxUsesScreenSpaceSizeAndCanHide()
        {
            Type dragBoxType = Type.GetType(SelectionDragBoxUiTypeName);
            Assert.NotNull(dragBoxType);

            GameObject canvasObject = new GameObject("Selection Test Canvas", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);

            GameObject boxObject = new GameObject(
                "SelectionDragBox",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            boxObject.transform.SetParent(canvasObject.transform, false);
            RectTransform boxRect = (RectTransform)boxObject.transform;
            Component dragBox = boxObject.AddComponent(dragBoxType);

            SetField(dragBox, "_boxRect", boxRect);
            SetField(dragBox, "_canvas", canvas);

            InvokeVoid(dragBox, "Show", new Vector2(100f, 120f), new Vector2(340f, 300f));

            Assert.IsTrue(boxObject.activeSelf);
            Assert.AreEqual(240f, boxRect.sizeDelta.x, 0.1f);
            Assert.AreEqual(180f, boxRect.sizeDelta.y, 0.1f);

            InvokeVoid(dragBox, "Hide");
            Assert.IsFalse(boxObject.activeSelf);

            UnityEngine.Object.Destroy(boxObject);
            UnityEngine.Object.Destroy(canvasObject);
            yield return null;
        }

        [Test]
        public void CommandDispatcherAcceptsMoveCommandWithTargets()
        {
            Type commandType = Type.GetType(PlayerCommandTypeName);
            Type dispatcherType = Type.GetType(CommandDispatcherTypeName);
            Type teamType = Type.GetType(TeamTypeName);
            Assert.NotNull(commandType);
            Assert.NotNull(dispatcherType);
            Assert.NotNull(teamType);

            GameObject unit = new GameObject("Command Target Unit");
            object playerTeam = Enum.Parse(teamType, "Player");
            MethodInfo moveFactory = commandType.GetMethod(
                "MoveUnits",
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(moveFactory);

            object command = moveFactory.Invoke(
                null,
                new object[]
                {
                    playerTeam,
                    0,
                    new[] { unit },
                    new Vector3(5f, 0f, 7f)
                });

            MethodInfo dispatch = dispatcherType.GetMethod(
                "Dispatch",
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(dispatch);

            bool accepted = (bool)dispatch.Invoke(null, new[] { command, null });
            Assert.IsTrue(accepted);

            UnityEngine.Object.DestroyImmediate(unit);
        }

        [Test]
        public void GameTickConfigDefaultsToTenHzAndTwoCatchUpSteps()
        {
            Type configType = Type.GetType(GameTickConfigTypeName);
            Assert.NotNull(configType);

            ScriptableObject config = ScriptableObject.CreateInstance(configType);

            Assert.AreEqual(10f, GetPropertyValue<float>(config, "TickRate"), 0.001f);
            Assert.AreEqual(0.1f, GetPropertyValue<float>(config, "TickDeltaTime"), 0.001f);
            Assert.AreEqual(2, GetPropertyValue<int>(config, "MaxCatchUpSteps"));

            UnityEngine.Object.DestroyImmediate(config);
        }

        private static void InvokeVoid(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(target, args);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            return (T)property.GetValue(target);
        }
    }
}
