using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Find editable screen objects and their scripts without rebuilding the scene.</summary>
    public sealed class EditableProjectWindow : EditorWindow
    {
        private static readonly string[] screenNames =
        {
            "Login",
            "Registration",
            "Signed-in home",
            "Flights",
            "Destinations",
            "Reviews",
            "Saved trips",
            "Price tracking"
        };
        private static readonly string[] canvasNames =
        {
            "Canvas",
            "RegistrationCanvas",
            "HomeCanvas",
            "FlightCanvas",
            "DestinationCanvas",
            "ReviewsCanvas",
            "SavedTripsCanvas",
            "PriceTrackingCanvas"
        };
        private static readonly string[] controllers =
        {
            "AccountController",
            "AccountController",
            "AccountController",
            "FlightController",
            "DestinationController",
            "ReviewsController",
            "SavedTripsController",
            "PriceTrackingController"
        };
        [SerializeField]
        private int selectedScreen;
        [MenuItem("Travel Planning/Open Editable Project")]
        public static void Open()
        {
            var window = GetWindow<EditableProjectWindow>("Travel Planner Screens");
            // Leave enough room to read the project path and the authoring instructions.
            window.minSize = new Vector2(650, 480);
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Edit the actual Unity scene", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Choose a screen and use Show Screen. This changes temporary visibility only; " +
                "edit its objects in the Inspector. Visibility restores before saving, entering Play mode, " +
                "rebuilding scripts or closing this window. Your actual Inspector edits are kept. " +
                "The Scene view opens flat in 2D, with its grid and skybox hidden. " +
                "No scene rebuilding is needed.", MessageType.Info);
            EditorGUILayout.LabelField("Project", Application.dataPath);
            EditorGUILayout.LabelField("Scene", SelectedScenePath);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Open Login scene"))
                    OpenScene(AuthSetup.ScenePath);
                selectedScreen = EditorGUILayout.Popup("Screen", selectedScreen, screenNames);
                if (GUILayout.Button("Show Screen in Scene view"))
                    ShowScreen();
                if (GUILayout.Button("Restore startup visibility"))
                    EditableScreenPreview.Restore();
                if (GUILayout.Button("Select screen controller"))
                    SelectController(false);
                if (GUILayout.Button("Open controller C# script"))
                    SelectController(true);
            }

            EditorGUILayout.HelpBox(
                "Use the Hierarchy to select a label/button and change its properties. " +
                "Keep script references and asset .meta files. The original login design is the team's baseline. " +
                "Generated lists are populated at runtime; their reusable prefabs are under " +
                "Assets/TravelPlanning/Prefabs.", MessageType.None);
            if (GUILayout.Button("Show editable prefabs folder"))
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/TravelPlanning/Prefabs");
        }

        private string SelectedScenePath => MultiSceneSetup.ScenePath(MultiSceneSetup.PageNames[selectedScreen]);

        private static bool OpenScene(string path)
        {
            EditableScreenPreview.Restore();
            if (SceneManager.GetActiveScene().path == path)
                return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            EditorSceneManager.OpenScene(path);
            return true;
        }

        private void ShowScreen()
        {
            if (!OpenScene(SelectedScenePath))
                return;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            GameObject chosen = Array.Find(roots, root => root.name == canvasNames[selectedScreen]);
            if (!chosen)
            {
                EditorUtility.DisplayDialog("Screen not prepared",
                    "This scene does not contain the selected screen. Use the scene migration menu to split the existing authored UI.", "OK");
                return;
            }

            foreach (var root in roots)
                if (root.GetComponent<Canvas>())
                    EditableScreenPreview.SetVisible(root, root == chosen);
            if (selectedScreen < 3)
            {
                string[] panels =
                {
                    "LoginCard",
                    "RegistrationCard",
                    "SignedInCard"
                };
                for (int index = 0; index < panels.Length; index++)
                {
                    var panel = chosen.transform.Find("Right Panel/" + panels[index]);
                    if (panel)
                        EditableScreenPreview.SetVisible(panel.gameObject, index == selectedScreen);
                }
            }

            Selection.activeGameObject = chosen;
            var view = GetWindow<SceneView>();
            view.in2DMode = true;
            view.showGrid = false;
            view.sceneViewState.showSkybox = false;
            view.Focus();
            view.FrameSelected();
            SceneView.RepaintAll();
        }

        private void SelectController(bool openScript)
        {
            if (!OpenScene(SelectedScenePath))
                return;
            // Shared authentication behavior lives in Login; Registration and Home own their UI.
            if (selectedScreen == 1 || selectedScreen == 2)
                EditorSceneManager.OpenScene(AuthSetup.ScenePath, OpenSceneMode.Additive);
            var root = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(candidate => candidate.parent == null && candidate.name == controllers[selectedScreen])
                .Select(candidate => candidate.gameObject).FirstOrDefault();
            if (!root)
                return;
            Selection.activeGameObject = root;
            if (!openScript)
                return;
            foreach (var component in root.GetComponents<MonoBehaviour>())
            {
                if (!component)
                    continue;
                var script = MonoScript.FromMonoBehaviour(component);
                if (script)
                {
                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }
        }

        private void OnDisable() => EditableScreenPreview.Restore();
    }
}
