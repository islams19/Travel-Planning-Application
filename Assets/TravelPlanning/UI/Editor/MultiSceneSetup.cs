using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Moves the authored UI into scenes without rebuilding its controls or layout.</summary>
    public static class MultiSceneSetup
    {
        public static readonly string[] PageNames = { "Login", "Registration", "Home", "Flights", "Destinations", "Reviews", "SavedTrips", "Notifications" };
        public static string ScenePath(string page) => page == "Login" ? AuthSetup.ScenePath : "Assets/TravelPlanning/Scenes/" + page + ".unity";
        public static string[] BuildScenePaths => PageNames.Select(ScenePath).ToArray();
        public static bool IsMigrated => File.Exists(ScenePath("Flights"));

        // Historical generation menus may still be used by team members. Once split,
        // opening the existing scene is safe; rebuilding the old monolithic UI is not.
        public static bool OpenMigratedProject()
        {
            if (!IsMigrated) return false;
            Migrate();
            return true;
        }

        [MenuItem("Travel Planning/Scenes/Migrate Authored Pages to Separate Scenes")]
        public static void Migrate()
        {
            EditableScreenPreview.Restore();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var bootstrapType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("TravelPlanning.UI.Shared.TravelSceneBootstrap")).FirstOrDefault(type => type != null);
            if (bootstrapType == null) throw new InvalidOperationException("TravelSceneBootstrap must compile before migration.");
            var login = EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            if (BuildScenePaths.All(File.Exists) && Root(login, "TravelSceneBootstrap"))
            {
                // A prepared project is already authored. Builds inspect it without rewriting it.
                ValidateSavedPages(login);
                ReleaseSetup.ConfigureBuildScenes();
                EditorSceneManager.OpenScene(AuthSetup.ScenePath);
                Debug.Log("MULTI_SCENE_ALREADY_READY");
                return;
            }
            var scenes = new System.Collections.Generic.List<Scene> { login };
            var canvas = Root(login, "Canvas");
            if (!canvas) throw new InvalidOperationException("Login scene is missing its original Canvas.");

            foreach (string page in PageNames.Skip(1))
            {
                string path = ScenePath(page);
                Scene scene = File.Exists(path) ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                // Unity cannot create a second additive scene while the previous one
                // is untitled. Establish its asset path before creating the next page;
                // the authored objects and cleaned references are saved below.
                if (string.IsNullOrEmpty(scene.path))
                    EditorSceneManager.SaveScene(scene, path);
                scenes.Add(scene);
                if (page == "Registration" || page == "Home")
                {
                    string rootName = page + "Canvas";
                    string panelName = page == "Registration" ? "RegistrationCard" : "SignedInCard";
                    var sourcePanel = canvas.transform.Find("Right Panel/" + panelName);
                    if (!Root(scene, rootName))
                    {
                        if (!sourcePanel) throw new InvalidOperationException("Missing original account panel: " + panelName);
                        var copy = UnityEngine.Object.Instantiate(canvas);
                        copy.name = rootName;
                        SceneManager.MoveGameObjectToScene(copy, scene);
                        foreach (string card in new[] { "LoginCard", "RegistrationCard", "SignedInCard" })
                        {
                            var duplicate = copy.transform.Find("Right Panel/" + card);
                            if (duplicate) UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                        }
                        // Reparenting a RectTransform through a scene root can alter its
                        // anchors/offsets. Capture its authored local geometry explicitly.
                        var rect = (RectTransform)sourcePanel;
                        Vector2 anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, size = rect.sizeDelta;
                        Vector3 position = rect.anchoredPosition3D, scale = rect.localScale;
                        Quaternion rotation = rect.localRotation;
                        sourcePanel.SetParent(null, false);
                        SceneManager.MoveGameObjectToScene(sourcePanel.gameObject, scene);
                        sourcePanel.SetParent(copy.transform.Find("Right Panel"), false);
                        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
                        rect.sizeDelta = size; rect.anchoredPosition3D = position;
                        rect.localScale = scale; rect.localRotation = rotation;
                        copy.SetActive(false);
                        sourcePanel.gameObject.SetActive(false);
                    }
                }
                else
                {
                    string prefix = page == "Flights" ? "Flight" : page == "Destinations" ? "Destination" : page == "Notifications" ? "PriceTracking" : page;
                    MoveRoot(login, scene, prefix + "Canvas");
                    MoveRoot(login, scene, prefix + "Controller");
                    if (!Root(scene, prefix + "Canvas") || !Root(scene, prefix + "Controller"))
                        throw new InvalidOperationException("Missing authored roots for " + page + ". Prepare the existing application before migrating.");
                }
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<Canvas>()) root.SetActive(false);
                Desktop2DSetup.ConfigureScene(scene);
            }

            string[] dependentTypes = { "LoginPage", "FlightSearchPage", "DestinationHubPage", "ReviewsPage", "SavedTripsPage", "PriceTrackingPage", "NotificationBadge", "DesktopKeyboardNavigation", "ReleaseSmokeRunner", "FlightSmokeRunner", "DestinationSmokeRunner", "ReviewSmokeRunner", "TripSmokeRunner", "TrackingSmokeRunner" };
            foreach (var scene in scenes)
                foreach (var component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).Where(item => item))
                {
                    if (dependentTypes.Contains(component.GetType().Name)) component.enabled = false;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        UnityEngine.Object reference = property.objectReferenceValue;
                        GameObject target = reference as GameObject;
                        if (reference is Component targetComponent) target = targetComponent.gameObject;
                        if (target && target.scene.IsValid() && target.scene != scene) property.objectReferenceValue = null;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            var bootstrap = Root(login, "TravelSceneBootstrap");
            if (!bootstrap)
            {
                bootstrap = new GameObject("TravelSceneBootstrap");
                SceneManager.MoveGameObjectToScene(bootstrap, login);
            }
            if (!bootstrap.GetComponent(bootstrapType)) bootstrap.AddComponent(bootstrapType);
            canvas.SetActive(true);
            canvas.transform.Find("Right Panel/LoginCard").gameObject.SetActive(true);
            Desktop2DSetup.ConfigureScene(login);
            for (int i = 0; i < scenes.Count; i++) EditorSceneManager.SaveScene(scenes[i], ScenePath(PageNames[i]));
            ReleaseSetup.ConfigureBuildScenes();
            EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            Debug.Log("MULTI_SCENE_MIGRATION_PASS " + string.Join(", ", BuildScenePaths));
        }

        private static void ValidateSavedPages(Scene login)
        {
            string[] roots = { "RegistrationCanvas", "HomeCanvas", "FlightCanvas", "DestinationCanvas", "ReviewsCanvas", "SavedTripsCanvas", "PriceTrackingCanvas" };
            for (int i = 1; i < PageNames.Length; i++)
            {
                var scene = EditorSceneManager.OpenScene(ScenePath(PageNames[i]), OpenSceneMode.Additive);
                if (!Root(scene, roots[i - 1])) throw new InvalidOperationException("Missing authored page canvas in " + scene.path);
                if (Root(login, roots[i - 1])) throw new InvalidOperationException("Page still exists in Login: " + roots[i - 1]);
            }
        }

        private static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        private static void MoveRoot(Scene source, Scene target, string name)
        {
            var root = Root(source, name);
            if (!root) return;
            if (Root(target, name)) throw new InvalidOperationException("Both scenes contain " + name + "; resolve this duplicate before migration.");
            SceneManager.MoveGameObjectToScene(root, target);
        }
    }
}
