using System.Collections;
using NUnit.Framework;
using TMPro;
using TravelPlanning.Trips;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Trips;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TripSceneTests
    {
#region TripModalReferencesAndSourcePrefabButtonsAreComplete
        [Test]
        public void TripModalReferencesAndSourcePrefabButtonsAreComplete()
        {
            ApplicationSceneFixture.Open();
            var page = Object.FindFirstObjectByType<SavedTripsPage>();
            Assert.That(page.transform.parent, Is.Null);
            var fields = new SerializedObject(page);
            foreach (string name in new[]
            {
                "account",
                "flights",
                "hub",
                "reviews",
                "modalCanvas",
                "homeButton",
                "flightButton",
                "hubButton",
                "closeButton",
                "createButton",
                "saveButton",
                "retryButton",
                "tripName",
                "tripChoice",
                "startDate",
                "endDate",
                "targetPreview",
                "status",
                "tripInfo",
                "scroll",
                "rowPrefab"
            }

            )
                Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var groups = fields.FindProperty("underlyingGroups");
            Assert.That(groups.arraySize, Is.EqualTo(3));
            for (int i = 0; i < groups.arraySize; i++)
                Assert.That(groups.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            var modal = (GameObject)fields.FindProperty("modalCanvas").objectReferenceValue;
            Assert.That(modal.activeSelf, Is.True, "The saved scene must be visible for direct Editor authoring.");
            Assert.That(modal.GetComponent<Canvas>().sortingOrder, Is.EqualTo(5));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FlightSearchSetup.RowPath).transform.Find("SaveActions/SaveToTripButton"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DestinationHubSetup.PlaceCardPath).transform.Find("ReviewActions/SaveToTripButton"), Is.Not.Null);
            modal.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue;
            Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200));
            Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }

#endregion
#region SavedItemRowShowsLiteralDetailsCurrentPriceAndUnits
        [Test]
        public void SavedItemRowShowsLiteralDetailsCurrentPriceAndUnits()
        {
            ApplicationSceneFixture.Open();
            var fields = new SerializedObject(Object.FindFirstObjectByType<SavedTripsPage>());
            ((GameObject)fields.FindProperty("modalCanvas").objectReferenceValue).SetActive(true);
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue;
            var row = Object.Instantiate((SavedTripRow)fields.FindProperty("rowPrefab").objectReferenceValue, scroll.content);
            // The service normally fills these read-only-to-callers model properties.
            var item = new SavedTripItem();
            typeof(SavedTripItem).GetProperty("Title").SetValue(item, "<b>Literal place</b>");
            typeof(SavedTripItem).GetProperty("Details").SetValue(item, "Original flight dates stay visible\n2027-06-10 08:00 JFK → LHR");
            typeof(SavedTripItem).GetProperty("PriceCents").SetValue(item, 12345);
            typeof(SavedTripItem).GetProperty("PriceUnit").SetValue(item, "/ flight");
            row.Show(item, () =>
            {
            });
            Canvas.ForceUpdateCanvases();
            Assert.That(row.transform.Find("Top/Title").GetComponent<TMP_Text>().richText, Is.False);
            Assert.That(row.transform.Find("Price").GetComponent<TMP_Text>().text, Is.EqualTo("USD 123.45 / flight"));
            Assert.That(row.transform.Find("Details").GetComponent<TMP_Text>().text, Does.Contain("2027-06-10"));
            Object.DestroyImmediate(row.gameObject);
        }

#endregion
#region ActualTripControlsSaveAllKindsRecoverAndIsolateTwoAccounts
        [UnityTest]
        public IEnumerator ActualTripControlsSaveAllKindsRecoverAndIsolateTwoAccounts()
        {
            ApplicationSceneFixture.Open();
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "trip-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>();
            string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("trip-editor-"));
            Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<TripSmokeRunner>().RunChecksAsync(true);
            float deadline = Time.realtimeSinceStartup + 130;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, "Saved-trip UI flow timed out.");
            if (task.IsFaulted)
                Assert.Fail(task.Exception.GetBaseException().ToString());
            Assert.That(task.IsCanceled, Is.False);
            var pageFields = new SerializedObject(Object.FindFirstObjectByType<SavedTripsPage>());
            var groups = pageFields.FindProperty("underlyingGroups");
            for (int i = 0; i < groups.arraySize; i++)
                Assert.That(((CanvasGroup)groups.GetArrayElementAtIndex(i).objectReferenceValue).interactable, Is.True, "Modal should restore underlying interaction on logout.");
            string path = System.IO.Path.Combine(Application.persistentDataPath, file);
            Assert.That(System.IO.File.Exists(path), Is.True);
            System.IO.File.Delete(path);
            yield return new ExitPlayMode();
        }
#endregion
    }
}
