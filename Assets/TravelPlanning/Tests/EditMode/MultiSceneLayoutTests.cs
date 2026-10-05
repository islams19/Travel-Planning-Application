using System.Linq;
using NUnit.Framework;
using TravelPlanning.UI.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.Tests
{
    public sealed class MultiSceneLayoutTests
    {
        [TestCase("Registration", "RegistrationCanvas", "RegistrationCard")]
        [TestCase("Home", "HomeCanvas", "SignedInCard")]
        [TestCase("Flights", "FlightCanvas", "")]
        [TestCase("Destinations", "DestinationCanvas", "")]
        [TestCase("Reviews", "ReviewsCanvas", "")]
        [TestCase("SavedTrips", "SavedTripsCanvas", "")]
        [TestCase("Notifications", "PriceTrackingCanvas", "")]
        public void EachAuthoredPageLivesInItsOwnSavedScene(string page, string canvasName, string cardName)
        {
            var scene = EditorSceneManager.OpenScene(MultiSceneSetup.ScenePath(page));
            var canvases = scene.GetRootGameObjects().Where(root => root.GetComponent<Canvas>()).ToArray();
            Assert.That(canvases.Select(canvas => canvas.name), Is.EqualTo(new[] { canvasName }));
            Assert.That(canvases[0].activeSelf, Is.True, "Open the scene and edit its visible UI directly.");
            if (!string.IsNullOrEmpty(cardName))
            {
                var panel = canvases[0].transform.Find("Right Panel/" + cardName);
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(580, page == "Registration" ? 800 : 700)));
                Assert.That(canvases[0].transform.Find("Right Panel/LoginCard"), Is.Null);
            }
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)), Is.Empty,
                "The shared Login camera must not be duplicated into feature scenes.");
        }

        [Test]
        public void LoginOwnsOnlyLoginCanvasAndSharedSessionShell()
        {
            var scene = EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            var roots = scene.GetRootGameObjects();
            Assert.That(roots.Where(root => root.GetComponent<Canvas>()).Select(root => root.name), Is.EqualTo(new[] { "Canvas" }));
            Assert.That(roots.Any(root => root.name == "AccountController"), Is.True);
            Assert.That(roots.Any(root => root.name == "TravelSceneBootstrap"), Is.True);
            var canvas = roots.Single(root => root.name == "Canvas");
            Assert.That(canvas.transform.Find("Right Panel/LoginCard"), Is.Not.Null);
            Assert.That(canvas.transform.Find("Right Panel/RegistrationCard"), Is.Null);
            Assert.That(canvas.transform.Find("Right Panel/SignedInCard"), Is.Null);
        }
    }
}
