using System.Collections;
using NUnit.Framework;
using TravelPlanning.Destinations;
using TravelPlanning.UI;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class DestinationHubSceneTests
    {
#region PriceLabelsExplainDemoUnits
        [TestCase(null, PlaceCategory.Hotel, "Price unavailable")]
        [TestCase(0, PlaceCategory.Hotspot, "Free")]
        [TestCase(12345, PlaceCategory.Hotel, "USD 123.45 / room / night")]
        [TestCase(2500, PlaceCategory.Restaurant, "USD 25.00 / person (meal)")]
        [TestCase(5000, PlaceCategory.Experience, "USD 50.00 / adult")]
        public void PriceLabelsExplainDemoUnits(int? cents, PlaceCategory category, string expected) => Assert.That(PlaceCard.FormatPrice(cents, category), Is.EqualTo(expected));
#endregion
#region DestinationScreenReferencesScrollHierarchyAndCardPrefabAreComplete
        [Test]
        public void DestinationScreenReferencesScrollHierarchyAndCardPrefabAreComplete()
        {
            ApplicationSceneFixture.Open();
            var hub = Object.FindFirstObjectByType<DestinationHubPage>();
            Assert.That(hub.transform.parent, Is.Null);
            var fields = new SerializedObject(hub);
            foreach (string name in new[]
            {
                "account",
                "flights",
                "hubCanvas",
                "destination",
                "description",
                "status",
                "openButton",
                "backButton",
                "logoutButton",
                "retryButton",
                "scroll",
                "hotels",
                "restaurants",
                "experiences",
                "hotspots",
                "cardPrefab"
            }

            )
                Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var canvas = (GameObject)fields.FindProperty("hubCanvas").objectReferenceValue;
            Assert.That(canvas.activeSelf, Is.True, "The saved scene must be visible for direct Editor authoring.");
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue;
            Assert.That(scroll.content.parent, Is.EqualTo(scroll.viewport));
            Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(), Is.Not.Null);
            Assert.That(scroll.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit, Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
            Assert.That(canvas.GetComponentsInChildren<PlaceSection>(true).Length, Is.EqualTo(4));
            var card = new SerializedObject(fields.FindProperty("cardPrefab").objectReferenceValue);
            foreach (string name in new[]
            {
                "placeName",
                "price",
                "rating",
                "description",
                "address"
            }

            )
                Assert.That(card.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            canvas.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200));
            Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }

#endregion
#region ActualControlsExploreChangeCitiesPreserveFlightsAndCancelOnLogout
        [UnityTest]
        public IEnumerator ActualControlsExploreChangeCitiesPreserveFlightsAndCancelOnLogout()
        {
            ApplicationSceneFixture.Open();
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "destination-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>();
            string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("destination-editor-"));
            Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<DestinationSmokeRunner>().RunChecksAsync(true);
            float deadline = Time.realtimeSinceStartup + 90;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, "Destination UI flow timed out.");
            if (task.IsFaulted)
                Assert.Fail(task.Exception.GetBaseException().ToString());
            Assert.That(task.IsCanceled, Is.False);
            string path = System.IO.Path.Combine(Application.persistentDataPath, file);
            Assert.That(System.IO.File.Exists(path), Is.True);
            System.IO.File.Delete(path);
            yield return new ExitPlayMode();
        }
#endregion
    }
}
