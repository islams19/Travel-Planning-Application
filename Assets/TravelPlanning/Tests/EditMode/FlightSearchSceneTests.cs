using System.Collections;
using System.Linq;
using NUnit.Framework;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Flights;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class FlightSearchSceneTests
    {
#region MaximumPriceUsesExactUsdCents
        [TestCase("", null)]
        [TestCase("527.25", 52725)]
        [TestCase("0", 0)]
        [TestCase("21474836.47", int.MaxValue)]
        public void MaximumPriceUsesExactUsdCents(string input, int? expected) => Assert.That(FlightSearchPage.ParseMaximumPrice(input), Is.EqualTo(expected));
#endregion
#region MaximumPriceRejectsAmbiguousOrOutOfRangeValues
        [TestCase("-1")]
        [TestCase("1.001")]
        [TestCase("NaN")]
        [TestCase("1,000")]
        [TestCase("21474836.48")]
        public void MaximumPriceRejectsAmbiguousOrOutOfRangeValues(string input) => Assert.Throws<System.ArgumentException>(() => FlightSearchPage.ParseMaximumPrice(input));
#endregion
#region FlightScreenReferencesAndScrollViewHierarchyAreComplete
        [Test]
        public void FlightScreenReferencesAndScrollViewHierarchyAreComplete()
        {
            ApplicationSceneFixture.Open();
            var page = Object.FindFirstObjectByType<FlightSearchPage>();
            Assert.That(page.transform.parent, Is.Null);
            var fields = new SerializedObject(page);
            foreach (string name in new[]
            {
                "account",
                "authCanvas",
                "flightCanvas",
                "origin",
                "destination",
                "departureDate",
                "returnDate",
                "airline",
                "timeBand",
                "sort",
                "maximumPrice",
                "status",
                "outboundHeading",
                "returnHeading",
                "openButton",
                "searchButton",
                "clearButton",
                "backButton",
                "logoutButton",
                "outboundContent",
                "returnContent",
                "outboundScroll",
                "returnScroll",
                "rowPrefab"
            }

            )
                Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var canvas = (GameObject)fields.FindProperty("flightCanvas").objectReferenceValue;
            Assert.That(canvas.activeSelf, Is.True, "The saved scene must be visible for direct Editor authoring.");
            foreach (var scroll in canvas.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Where(x => x.name == "ScrollView"))
            {
                Assert.That(scroll.content.parent, Is.EqualTo(scroll.viewport));
                Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(), Is.Not.Null);
                Assert.That(scroll.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit, Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
                Assert.That(scroll.horizontal, Is.False);
            }

            canvas.SetActive(true);
            Canvas.ForceUpdateCanvases();
            foreach (var scroll in canvas.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Where(x => x.name == "ScrollView"))
            {
                Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200));
                Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            }

            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }

#endregion
#region ActualControlsSearchFilterValidateCancelAndRecover
        [UnityTest]
        public IEnumerator ActualControlsSearchFilterValidateCancelAndRecover()
        {
            ApplicationSceneFixture.Open();
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "flight-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>();
            string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("flight-editor-"));
            var runner = Object.FindFirstObjectByType<FlightSmokeRunner>();
            var task = runner.RunChecksAsync(true);
            float deadline = Time.realtimeSinceStartup + 90;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, "UI flow timed out.");
            if (task.IsFaulted)
                Assert.Fail(task.Exception.GetBaseException().ToString());
            Assert.That(task.IsCanceled, Is.False);
            account.Logout();
            string path = System.IO.Path.Combine(Application.persistentDataPath, file);
            Assert.That(System.IO.File.Exists(path), Is.True);
            System.IO.File.Delete(path);
            yield return new ExitPlayMode();
        }
#endregion
    }
}
