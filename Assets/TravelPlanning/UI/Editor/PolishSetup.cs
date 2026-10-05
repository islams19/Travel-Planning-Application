using TravelPlanning.UI.Desktop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Applies desktop resizing and keyboard behavior without changing the login layout.</summary>
    public static class PolishSetup
    {
#region Prepare
        [MenuItem("Travel Planning/Polish/1 - Prepare Desktop Polish")]
        public static void Prepare()
        {
            if (MultiSceneSetup.OpenMigratedProject()) return;
            TrackingSetup.Prepare();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string[] names =
            {
                "Canvas",
                "FlightCanvas",
                "DestinationCanvas",
                "ReviewsCanvas",
                "SavedTripsCanvas",
                "PriceTrackingCanvas"
            };
            string[] back =
            {
                "Right Panel/RegistrationCard/Create an account",
                "Main/Header/BackButton",
                "Main/Header/BackButton",
                "Main/Header/CloseButton",
                "Main/Header/CloseButton",
                "Main/Header/CloseButton"
            };
            string[] focus =
            {
                "Right Panel/LoginCard/EmailInput",
                "Main/RouteFields/Origin/Dropdown",
                "Main/DestinationSelection/Dropdown",
                "Main/Header/CloseButton",
                "Main/TripSelection/TripChoice",
                "Main/WatchSelection/Dropdown"
            };
            var roots = scene.GetRootGameObjects();
            var canvases = new Canvas[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var root = System.Array.Find(roots, candidate => candidate.name == names[i]);
                if (!root)
                    throw new System.InvalidOperationException("Missing desktop canvas: " + names[i]);
                canvases[i] = root.GetComponent<Canvas>();
                var fit = root.GetComponent<DesktopCanvasFit>();
                if (!fit)
                    fit = root.AddComponent<DesktopCanvasFit>();
                fit.Apply();
            }

            var controller = Object.FindFirstObjectByType<DesktopKeyboardNavigation>();
            if (!controller)
                controller = new GameObject("DesktopNavigationController").AddComponent<DesktopKeyboardNavigation>();
            var fields = new SerializedObject(controller);
            fields.FindProperty("account").objectReferenceValue = Object.FindFirstObjectByType<LoginPage>();
            var bindings = fields.FindProperty("screens");
            bindings.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("canvas").objectReferenceValue = canvases[i];
                binding.FindPropertyRelative("backButton").objectReferenceValue = canvases[i].transform.Find(back[i]).GetComponent<UnityEngine.UI.Button>();
                binding.FindPropertyRelative("defaultFocus").objectReferenceValue = canvases[i].transform.Find(focus[i]).GetComponent<UnityEngine.UI.Selectable>();
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("DESKTOP_POLISH_SCENE_READY " + scene.path);
        }
#endregion
    }
}
