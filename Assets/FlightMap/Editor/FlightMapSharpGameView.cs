#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelApp.FlightMap.Editor
{
    /// <summary>Selects a full-resolution Game view and disables Unity's low-resolution preview.</summary>
    [InitializeOnLoad]
    public static class FlightMapSharpGameView
    {
        const string ScenePath = "Assets/FlightMap/Scenes/FlightMapScene.unity";
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static FlightMapSharpGameView()
        {
            EditorSceneManager.sceneOpened += (_, __) => QueueForFlightMap();
            QueueForFlightMap();
        }

        static void QueueForFlightMap()
        {
            if (EditorSceneManager.GetActiveScene().path == ScenePath)
                EditorApplication.delayCall += Configure1280x720;
        }

        [MenuItem("Tools/Flight Map/Sharp Game View/1280 x 720")]
        public static void Configure1280x720() => Configure(1280, 720);

        [MenuItem("Tools/Flight Map/Sharp Game View/1920 x 1080")]
        public static void Configure1920x1080() => Configure(1920, 1080);

        public static bool Configure(int width, int height)
        {
            try
            {
                Assembly assembly = typeof(EditorWindow).Assembly;
                Type gameViewType = assembly.GetType("UnityEditor.GameView");
                Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                Type groupEnumType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
                Type sizeKindType = assembly.GetType("UnityEditor.GameViewSizeType");
                if (gameViewType == null || sizesType == null || groupEnumType == null || sizeType == null || sizeKindType == null) return false;

                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                SetBoolean(gameViewType, gameView, "lowResolutionForAspectRatios", false);
                // In Unity 6.3, instance is inherited from ScriptableSingleton<T>, not declared
                // directly on GameViewSizes. Reflect the closed generic base to obtain it.
                Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singletonType.GetProperty("instance", All)?.GetValue(null);
                object standalone = Enum.Parse(groupEnumType, "Standalone");
                object group = sizesType.GetMethod("GetGroup", All)?.Invoke(sizes, new[] { standalone });
                if (group == null) return false;

                Type groupType = group.GetType();
                MethodInfo getTotalCount = groupType.GetMethod("GetTotalCount", All);
                MethodInfo getSize = groupType.GetMethod("GetGameViewSize", All);
                int selected = FindSize(group, getTotalCount, getSize, width, height);
                if (selected < 0)
                {
                    object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
                    object newSize = Activator.CreateInstance(sizeType, All, null,
                        new object[] { fixedResolution, width, height, $"Flight Map {width}x{height}" }, null);
                    groupType.GetMethod("AddCustomSize", All)?.Invoke(group, new[] { newSize });
                    selected = FindSize(group, getTotalCount, getSize, width, height);
                }

                gameViewType.GetProperty("selectedSizeIndex", All)?.SetValue(gameView, selected);
                gameView.Repaint();
                return selected >= 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Flight Map could not configure the Game view automatically: " + exception.Message);
                return false;
            }
        }

        static int FindSize(object group, MethodInfo getTotalCount, MethodInfo getSize, int width, int height)
        {
            if (getTotalCount == null || getSize == null) return -1;
            int count = (int)getTotalCount.Invoke(group, null);
            for (int index = 0; index < count; index++)
            {
                object size = getSize.Invoke(group, new object[] { index });
                Type type = size.GetType();
                int candidateWidth = Convert.ToInt32(type.GetProperty("width", All)?.GetValue(size));
                int candidateHeight = Convert.ToInt32(type.GetProperty("height", All)?.GetValue(size));
                if (candidateWidth == width && candidateHeight == height) return index;
            }
            return -1;
        }

        static void SetBoolean(Type type, object target, string memberName, bool value)
        {
            PropertyInfo property = type.GetProperty(memberName, All);
            if (property?.CanWrite == true) property.SetValue(target, value);
            else type.GetField(memberName, All)?.SetValue(target, value);
        }
    }
}
#endif
