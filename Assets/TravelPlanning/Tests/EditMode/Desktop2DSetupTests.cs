using System.Linq;
using NUnit.Framework;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.Tests
{
    public sealed class Desktop2DSetupTests
    {
        [Test]
        public void PreparedLoginSceneUsesFlat2DRenderingAndStartupScene()
        {
            // Release preparation runs before this validation suite. Inspect the saved asset.
            var scene = EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            var roots = scene.GetRootGameObjects();
            var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();

            Assert.That(EditorSettings.defaultBehaviorMode, Is.EqualTo(EditorBehaviorMode.Mode2D));
            Assert.That(cameras, Is.Not.Empty, "The application scene must retain its camera.");
            foreach (var camera in cameras)
            {
                Assert.That(camera.orthographic, Is.True, camera.name);
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor), camera.name);
                Assert.That(camera.backgroundColor, Is.EqualTo(Color.white), camera.name);
                var skybox = camera.GetComponent<Skybox>();
                Assert.That(!skybox || !skybox.enabled, Is.True, camera.name);
            }

            foreach (var light in roots.SelectMany(root => root.GetComponentsInChildren<Light>(true)))
                Assert.That(light.enabled, Is.False, light.name);
            Assert.That(EditorBuildSettings.scenes.Select(buildScene => buildScene.path), Is.EqualTo(
                MultiSceneSetup.BuildScenePaths.Concat(new[] { SqliteProofSetup.ScenePath, TravelDatabaseSetup.ScenePath, "Assets/Scenes/SampleScene.unity" })));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(AuthSetup.ScenePath));
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
            Assert.That(EditorBuildSettings.scenes.All(buildScene => buildScene.enabled), Is.True);
        }
    }
}
