using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using TravelPlanning.UI;

namespace TravelPlanning.Tests
{
    public sealed class SqliteProofPlayModeTests
    {
#region MissingInspectorReferencesAreReportedClearly
        [UnityTest]
        public IEnumerator MissingInspectorReferencesAreReportedClearly()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            LogAssert.Expect(LogType.Error, "SqliteProofPage: assign Status Text, Details Text, Heartbeat Text and Run Button in the Inspector.");
            var incomplete = new GameObject("IncompleteProofPage");
            incomplete.AddComponent<SqliteProofPage>();
            yield return null;
            Object.Destroy(incomplete);
            yield return new ExitPlayMode();
        }

#endregion
#region DiagnosticSceneDisplaysSuccessfulResult
        [UnityTest]
        public IEnumerator DiagnosticSceneDisplaysSuccessfulResult()
        {
            EditorSceneManager.OpenScene("Assets/TravelPlanning/Scenes/SqliteProof.unity");
            yield return new EnterPlayMode();
            var status = GameObject.Find("Status").GetComponent<UnityEngine.UI.Text>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!status.text.StartsWith("PASS") && !status.text.StartsWith("FAIL") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(status.text, Does.StartWith("PASS"));
            var details = GameObject.Find("Details").GetComponent<UnityEngine.UI.Text>();
            Assert.That(details.text, Does.Contain("Worker thread:"));
            var button = GameObject.Find("RunAgain").GetComponent<UnityEngine.UI.Button>();
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
            while (!status.text.StartsWith("PASS") && !status.text.StartsWith("FAIL") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(status.text, Does.Contain("saved row loaded again"));
            yield return new ExitPlayMode();
        }
#endregion
    }
}
