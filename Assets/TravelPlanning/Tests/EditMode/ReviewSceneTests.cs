using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TravelPlanning.Reviews;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Reviews;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class ReviewSceneTests
    {
#region ReviewOverlayReferencesAndInputBlockingAreComplete
        [Test]
        public void ReviewOverlayReferencesAndInputBlockingAreComplete()
        {
            ApplicationSceneFixture.Open();
            var page = Object.FindFirstObjectByType<ReviewsPage>();
            Assert.That(page.transform.parent, Is.Null);
            var fields = new SerializedObject(page);
            foreach (string name in new[]
            {
                "account",
                "hub",
                "overlayCanvas",
                "placeName",
                "summary",
                "status",
                "closeButton",
                "retryButton",
                "mapsButton",
                "scroll",
                "rowPrefab"
            }

            )
                Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var canvas = (GameObject)fields.FindProperty("overlayCanvas").objectReferenceValue;
            Assert.That(canvas.activeSelf, Is.True, "The saved scene must be visible for direct Editor authoring.");
            Assert.That(canvas.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(3));
            Assert.That(canvas.transform.Find("InputBlocker").GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            foreach (var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                Assert.That(button.navigation.mode, Is.EqualTo(UnityEngine.UI.Navigation.Mode.Explicit));
            var row = (ReviewRow)fields.FindProperty("rowPrefab").objectReferenceValue;
            Assert.That(row.GetComponentsInChildren<StarGraphic>(true).Length, Is.EqualTo(5));
            Assert.That(row.transform.Find("Body").GetComponent<UnityEngine.UI.LayoutElement>(), Is.Null, "The review body must use its full preferred height.");
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }

#endregion
#region LongLiteralReviewBodyExpandsWithoutClippingAndShowsFiveStarShapes
        [Test]
        public void LongLiteralReviewBodyExpandsWithoutClippingAndShowsFiveStarShapes()
        {
            ApplicationSceneFixture.Open();
            var fields = new SerializedObject(Object.FindFirstObjectByType<ReviewsPage>());
            var canvas = (GameObject)fields.FindProperty("overlayCanvas").objectReferenceValue;
            canvas.SetActive(true);
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue;
            var prefab = (ReviewRow)fields.FindProperty("rowPrefab").objectReferenceValue;
            var row = Object.Instantiate(prefab, scroll.content);
            string body = string.Concat(Enumerable.Repeat("<b>Literal catalog text</b> should remain readable while the whole review wraps onto more lines. ", 35));
            row.Show(new ReviewOption { TravelerName = "<b>Demo traveler</b>", Rating = 3, IsDemo = true, Body = body });
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
            var text = row.transform.Find("Body").GetComponent<TMP_Text>();
            Assert.That(text.richText, Is.False);
            Assert.That(text.text, Is.EqualTo(body));
            Assert.That(text.rectTransform.rect.height + 1, Is.GreaterThanOrEqualTo(text.preferredHeight));
            Assert.That(row.GetComponent<RectTransform>().rect.height, Is.GreaterThan(250));
            Assert.That(row.GetComponentsInChildren<StarGraphic>().Count(star => star.Filled), Is.EqualTo(3));
            Object.DestroyImmediate(row.gameObject);
        }

#endregion
#region ActualReviewControlsPreserveHubValidateLinksAndCancelStaleRequests
        [UnityTest]
        public IEnumerator ActualReviewControlsPreserveHubValidateLinksAndCancelStaleRequests()
        {
            ApplicationSceneFixture.Open();
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "review-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>();
            string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("review-editor-"));
            Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<ReviewSmokeRunner>().RunChecksAsync(true);
            float deadline = Time.realtimeSinceStartup + 100;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, "Review UI flow timed out.");
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
