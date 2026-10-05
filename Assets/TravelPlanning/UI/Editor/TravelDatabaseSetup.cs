using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Builds a separate seed-database diagnostic without altering Kevin's login.</summary>
    public static class TravelDatabaseSetup
    {
        public const string ScenePath = "Assets/TravelPlanning/Scenes/TravelDatabase.unity";
        public const string BuildPath = "Builds/TravelDatabase/TravelPlannerDatabase.exe";
#region CreateScene
        [MenuItem("Travel Planning/Travel Database/2 - Create Diagnostic Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            if (!File.Exists(SqliteProofSetup.ScenePath))
                SqliteProofSetup.CreateScene();
            // Use the established diagnostic layout, then save as a NEW scene.
            var scene = EditorSceneManager.OpenScene(SqliteProofSetup.ScenePath);
            var oldPage = UnityEngine.Object.FindFirstObjectByType<SqliteProofPage>();
            var oldFields = new SerializedObject(oldPage);
            string[] names =
            {
                "statusText",
                "detailsText",
                "heartbeatText",
                "runButton"
            };
            var references = new UnityEngine.Object[names.Length];
            for (int index = 0; index < names.Length; index++)
                references[index] = oldFields.FindProperty(names[index]).objectReferenceValue;
            GameObject panel = oldPage.gameObject;
            UnityEngine.Object.DestroyImmediate(oldPage);
            var newFields = new SerializedObject(panel.AddComponent<TravelDatabasePage>());
            for (int index = 0; index < names.Length; index++)
                newFields.FindProperty(names[index]).objectReferenceValue = references[index];
            newFields.ApplyModifiedPropertiesWithoutUndo();
            panel.transform.Find("Title").GetComponent<UnityEngine.UI.Text>().text = "Travel database - Milestone 1B";
            panel.transform.Find("RunAgain/Label").GetComponent<UnityEngine.UI.Text>().text = "Validate again (preserves your database)";
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("TRAVEL_DATABASE_SCENE_READY " + ScenePath);
        }

#endregion
#region Prepare
        public static void Prepare()
        {
            TravelSeedBuilder.BuildSeed();
            CreateScene();
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Travel Database/3 - Build Windows x64")]
        public static void BuildWindows()
        {
            TravelSeedBuilder.BuildSeed();
            CreateScene();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Travel database build failed.");
            string notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("TRAVEL_DATABASE_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
#endregion
    }
}
