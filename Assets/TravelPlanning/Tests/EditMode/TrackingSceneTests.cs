using System.Collections;
using NUnit.Framework;
using TMPro;
using TravelPlanning.Tracking;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Tracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TrackingSceneTests
    {
#region TrackingReferencesBadgesAndTrackButtonsAreComplete
        [Test]
        public void TrackingReferencesBadgesAndTrackButtonsAreComplete()
        {
            ApplicationSceneFixture.Open();
            var page = Object.FindFirstObjectByType<PriceTrackingPage>();
            Assert.That(page.transform.parent, Is.Null);
            var fields = new SerializedObject(page);
            foreach (string name in new[]
            {
                "account",
                "flights",
                "hub",
                "reviews",
                "trips",
                "badge",
                "modalCanvas",
                "demoPanel",
                "homeButton",
                "flightButton",
                "hubButton",
                "closeButton",
                "refreshButton",
                "trackButton",
                "readAllButton",
                "applyButton",
                "watchChoice",
                "demoPrice",
                "targetPreview",
                "status",
                "unreadSummary",
                "trackLabel",
                "demoFeedback",
                "scroll",
                "rowPrefab"
            }

            )
                Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var groups = fields.FindProperty("underlyingGroups");
            Assert.That(groups.arraySize, Is.EqualTo(3));
            for (int i = 0; i < groups.arraySize; i++)
                Assert.That(groups.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            var badge = new SerializedObject(fields.FindProperty("badge").objectReferenceValue);
            var labels = badge.FindProperty("labels");
            Assert.That(labels.arraySize, Is.EqualTo(3));
            for (int i = 0; i < labels.arraySize; i++)
                Assert.That(labels.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            var canvas = (GameObject)fields.FindProperty("modalCanvas").objectReferenceValue;
            Assert.That(canvas.activeSelf, Is.True, "The saved scene must be visible for direct Editor authoring.");
            Assert.That(canvas.GetComponent<Canvas>().sortingOrder, Is.EqualTo(6));
            canvas.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue;
            Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200));
            Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FlightSearchSetup.RowPath).transform.Find("SaveActions/TrackPriceButton"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DestinationHubSetup.PlaceCardPath).transform.Find("ReviewActions/TrackPriceButton"), Is.Not.Null);
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }

#endregion
#region NoticeRowDisplaysLiteralTextAndDisablesAlreadyReadAction
        [Test]
        public void NoticeRowDisplaysLiteralTextAndDisablesAlreadyReadAction()
        {
            ApplicationSceneFixture.Open();
            var fields = new SerializedObject(Object.FindFirstObjectByType<PriceTrackingPage>());
            ((GameObject)fields.FindProperty("modalCanvas").objectReferenceValue).SetActive(true);
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue;
            var row = Object.Instantiate((PriceNoticeRow)fields.FindProperty("rowPrefab").objectReferenceValue, scroll.content);
            var notice = new PriceNotice();
            typeof(PriceNotice).GetProperty("Title").SetValue(notice, "<b>Literal flight title</b>");
            typeof(PriceNotice).GetProperty("Body").SetValue(notice, "USD 527.00 changed to USD 520.00");
            typeof(PriceNotice).GetProperty("CreatedUtc").SetValue(notice, "2027-06-01T10:00:00Z");
            typeof(PriceNotice).GetProperty("IsRead").SetValue(notice, true);
            row.Show(notice, () =>
            {
            });
            Assert.That(row.transform.Find("Top/Title").GetComponent<TMP_Text>().richText, Is.False);
            Assert.That(row.transform.Find("Body").GetComponent<TMP_Text>().text, Does.Contain("527.00"));
            Assert.That(row.transform.Find("Top/ReadButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Object.DestroyImmediate(row.gameObject);
        }

#endregion
#region ActualTrackingControlsFanOutNotifyPersistAndIsolateAccounts
        [UnityTest]
        public IEnumerator ActualTrackingControlsFanOutNotifyPersistAndIsolateAccounts()
        {
            ApplicationSceneFixture.Open();
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "tracking-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>();
            string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("tracking-editor-"));
            Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<TrackingSmokeRunner>().RunChecksAsync(true);
            float deadline = Time.realtimeSinceStartup + 170;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, "Tracking UI flow timed out.");
            if (task.IsFaulted)
                Assert.Fail(task.Exception.GetBaseException().ToString());
            Assert.That(task.IsCanceled, Is.False);
            Assert.That(Object.FindFirstObjectByType<NotificationBadge>().UnreadCount, Is.EqualTo(0), "Logout must clear unread badges.");
            var pageFields = new SerializedObject(Object.FindFirstObjectByType<PriceTrackingPage>());
            var groups = pageFields.FindProperty("underlyingGroups");
            for (int i = 0; i < groups.arraySize; i++)
                Assert.That(((CanvasGroup)groups.GetArrayElementAtIndex(i).objectReferenceValue).interactable, Is.True);
            string path = System.IO.Path.Combine(Application.persistentDataPath, file);
            Assert.That(System.IO.File.Exists(path), Is.True);
            System.IO.File.Delete(path);
            yield return new ExitPlayMode();
        }
#endregion
    }
}
