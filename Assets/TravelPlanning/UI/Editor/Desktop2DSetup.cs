using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Keeps the desktop app and its authoring defaults explicitly two-dimensional.</summary>
    public static class Desktop2DSetup
    {
        public static void Apply()
        {
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            ConfigureScene(SceneManager.GetActiveScene());
        }

        public static void ConfigureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new System.ArgumentException("Open the application scene before configuring 2D mode.", nameof(scene));

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.orthographic = true;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.white;
                    // A camera-specific Skybox would otherwise override its background.
                    var skybox = camera.GetComponent<Skybox>();
                    if (skybox) skybox.enabled = false;
                }

                // uGUI does not need 3D lighting. Keep authoring objects and references intact.
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    light.enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
