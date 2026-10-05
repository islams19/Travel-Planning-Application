using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Creates the isolated diagnostic scene and builds it without replacing the app scene list.</summary>
    public static class SqliteProofSetup
    {
        public const string ScenePath = "Assets/TravelPlanning/Scenes/SqliteProof.unity";
        public const string BuildPath = "Builds/SqliteProof/TravelPlannerSqliteProof.exe";
#region CreateScene
        [MenuItem("Travel Planning/SQLite Proof/1 - Create Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ScenePath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath);
                if (EnsureCamera())
                    EditorSceneManager.SaveScene(existing, ScenePath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvasObject = new GameObject("SqliteProofCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000, 700);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("ProofPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(SqliteProofPage));
            panel.transform.SetParent(canvasObject.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24, 24);
            rect.offsetMax = new Vector2(-24, -24);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.96f, 0.97f, 1f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Text(panel.transform, "Title", "SQLite proof - Milestone 1A", 28, 44);
            var status = Text(panel.transform, "Status", "Waiting to start...", 23, 50);
            var details = Text(panel.transform, "Details", "", 18, 320);
            var heartbeat = Text(panel.transform, "Heartbeat", "", 18, 32);
            var buttonObject = new GameObject("RunAgain", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            buttonObject.transform.SetParent(panel.transform, false);
            buttonObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48;
            buttonObject.GetComponent<UnityEngine.UI.Image>().color = new Color32(49, 87, 255, 255);
            var label = Text(buttonObject.transform, "Label", "Run again (keeps the same row)", 20, 48);
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var serialized = new SerializedObject(panel.GetComponent<SqliteProofPage>());
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("detailsText").objectReferenceValue = details;
            serialized.FindProperty("heartbeatText").objectReferenceValue = heartbeat;
            serialized.FindProperty("runButton").objectReferenceValue = buttonObject.GetComponent<UnityEngine.UI.Button>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EnsureCamera();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("SQLITE_PROOF_SCENE_READY " + ScenePath);
        }

#endregion
#region ConfigureWindows
        [MenuItem("Travel Planning/SQLite Proof/2 - Configure Windows")]
        public static void ConfigureWindows()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1000;
            PlayerSettings.defaultScreenHeight = 700;
            // This is the serialized setting behind Player > Active Input Handling.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input == null)
                throw new InvalidOperationException("Active Input Handling setting was not found.");
            input.intValue = 0; // Input Manager (Old). No new Input System required by this scene.
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("SQLITE_PROOF_WINDOWS_CONFIGURED - restart the Editor after changing input handling.");
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/SQLite Proof/3 - Build Windows x64")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Create the SQLite proof scene first.", ScenePath);
            CreateScene();
            // Keep the diagnostic responsive when its window loses focus, including smoke captures.
            PlayerSettings.runInBackground = true;
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("SQLite proof build failed: " + report.summary.result);
            string licenses = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(licenses);
            foreach (string source in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    File.Copy(source, Path.Combine(licenses, Path.GetFileName(source)), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(licenses, "LiteDB-LICENSE.txt"), true);
            Debug.Log("SQLITE_PROOF_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

#endregion
#region Prepare
        // Command-line entry point: configuration is separate so the next process uses legacy input.
        public static void Prepare()
        {
            ConfigureWindows();
            CreateScene();
        }

#endregion
#region EnsureCamera
        private static bool EnsureCamera()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Camera>() != null)
                return false;
            // URP needs a camera to produce a frame, even with Screen Space Overlay UI.
            var camera = new GameObject("ProofCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.10f, 0.16f);
            camera.cullingMask = 0;
            return true;
        }

#endregion
#region Text
        private static UnityEngine.UI.Text Text(Transform parent, string name, string value, int size, int height)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.LayoutElement));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<UnityEngine.UI.Text>();
            // Diagnostic scene only: no TMP asset import or changes to Kevin's login font.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.10f, 0.13f, 0.20f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            item.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return text;
        }
#endregion
    }
}
