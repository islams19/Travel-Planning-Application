using System;
using System.Linq;
using NUnit.Framework;
using TravelPlanning.UI.Editor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TravelPlanning.Tests
{
    internal static class ApplicationSceneFixture
    {
        public static void Open()
        {
            EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            foreach (string path in MultiSceneSetup.BuildScenePaths.Skip(1))
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(AuthSetup.ScenePath));
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("TravelPlanning.UI.Shared.TravelSceneBootstrap"))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Application bootstrap must be compiled.");
            var bind = type.GetMethod("BindLoadedScenesForEditor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(bind, Is.Not.Null, "Application bootstrap must provide an editor binding entry point.");
            bind.Invoke(null, null);
        }
    }
}
