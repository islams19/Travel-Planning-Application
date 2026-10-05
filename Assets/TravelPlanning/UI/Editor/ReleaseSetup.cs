using System;
using System.IO;
using System.Linq;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Polish;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Tracking;
using TravelPlanning.UI.Trips;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    public static class ReleaseSetup
    {
        public const string BuildPath = "Builds/TravelPlannerRelease/TravelPlanner.exe";
        public const string DevelopmentBuildPath = "Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe";
#region Prepare
        [MenuItem("Travel Planning/Release/1 - Prepare Final Scene")]
        public static void Prepare()
        {
            // Preparation migrates authored objects once and is safe to rerun.
            // It must never regenerate pages over the team's Inspector edits.
            MultiSceneSetup.Migrate();
            Debug.Log("RELEASE_SCENES_READY " + AuthSetup.ScenePath);
        }

#endregion
#region ConfigureBuildScenes
        [MenuItem("Travel Planning/Release/Configure Application Build Scenes")]
        public static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = MultiSceneSetup.BuildScenePaths
                .Concat(new[] { SqliteProofSetup.ScenePath, TravelDatabaseSetup.ScenePath, "Assets/Scenes/SampleScene.unity" })
                .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }
#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Release/2 - Build Windows Release")]
        public static void BuildWindows() => Build(BuildPath, false);
#endregion
#region BuildDevelopment
        [MenuItem("Travel Planning/Release/3 - Build Windows Development QA")]
        public static void BuildDevelopment() => Build(DevelopmentBuildPath, true);
#endregion
#region Build
        private static void Build(string path, bool development)
        {
            Prepare();
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            string developmentOverride = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            if (!string.IsNullOrWhiteSpace(developmentOverride))
                throw new InvalidOperationException("Clear the AccountController development database override before building the final scene. It was not changed automatically.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Final Windows build failed.");
            string root = Path.GetDirectoryName(path);
            string notices = Path.Combine(root, "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            File.WriteAllText(Path.Combine(root, "release-validation.json"),
                JsonUtility.ToJson(new ReleaseValidationSettings
                {
                    companyName = PlayerSettings.companyName,
                    productName = PlayerSettings.productName,
                    developmentBuild = development
                }, true));
            Debug.Log((development ? "FINAL_DEVELOPMENT_BUILD_PASS " : "FINAL_RELEASE_BUILD_PASS ") + Path.GetFullPath(path));
        }

#endregion
#region Set
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
#endregion
    }
}
