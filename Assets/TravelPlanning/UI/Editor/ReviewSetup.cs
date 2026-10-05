using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Reviews;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds review details and upgrades existing place cards without rebuilding the destination scene.</summary>
    public abstract class ReviewSetup : EditorUiBuilderBase
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/Reviews/TravelPlannerReviews.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Reviews/ReviewRow.prefab";
#region Prepare
        [MenuItem("Travel Planning/Reviews/1 - Prepare Reviews")]
        public static void Prepare()
        {
            if (MultiSceneSetup.OpenMigratedProject()) return;
            DestinationHubSetup.Prepare();
            UpgradePlaceCards();
            if (UnityEngine.Object.FindFirstObjectByType<ReviewsPage>() != null)
                return;
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            var hub = UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>();
            CreateScreen(account, hub);
        }

#endregion
#region CreateScreen
        // Creation runs only when this feature has not already been prepared.
        private static void CreateScreen(LoginPage account, DestinationHubPage hub)
        {
            var canvas = CreateOverlayCanvas("ReviewsCanvas", 4);
            var blocker = Rect("InputBlocker", canvas.transform);
            Stretch(blocker);
            var image = blocker.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            image.raycastTarget = true;
            var main = Rect("Main", canvas.transform);
            Stretch(main);
            main.offsetMin = new Vector2(48, 40);
            main.offsetMax = new Vector2(-48, -40);
            Vertical(main, 18);
            var header = Horizontal("Header", main, 72);
            var title = Label(header, "PlaceName", "Traveler reviews", 40);
            Layout(title.gameObject, -1, 1);
            var close = Button(header, "CloseButton", "Close reviews", 240);
            var summary = Label(main, "Summary", "", 28);
            Layout(summary.gameObject, 46);
            var note = Label(main, "DemoNote", "Fictional demo reviews from this local catalog. Google Maps search results are separate.", 23);
            Layout(note.gameObject, 44);
            var status = Label(main, "Status", "", 23);
            Layout(status.gameObject, 68);
            var actions = Horizontal("Actions", main, 56);
            var maps = Button(actions, "GoogleMapsButton", "Google Maps search", 330);
            var retry = Button(actions, "RetryButton", "Retry", 160);
            // Explicit navigation keeps keyboard focus inside this modal screen.
            Navigation(close, retry, maps);
            Navigation(retry, maps, close);
            Navigation(maps, close, retry);
            var scroll = CreateScrollableList(main, 20);
            var controller = new GameObject("ReviewsController").AddComponent<ReviewsPage>();
            var fields = new SerializedObject(controller);
            Set(fields, "account", account);
            Set(fields, "hub", hub);
            Set(fields, "overlayCanvas", canvas);
            Set(fields, "placeName", title);
            Set(fields, "summary", summary);
            Set(fields, "status", status);
            Set(fields, "closeButton", close);
            Set(fields, "retryButton", retry);
            Set(fields, "mapsButton", maps);
            Set(fields, "scroll", scroll);
            Set(fields, "rowPrefab", CreateRow());
            fields.ApplyModifiedPropertiesWithoutUndo();
            var runner = controller.gameObject.AddComponent<ReviewSmokeRunner>();
            var runnerFields = new SerializedObject(runner);
            Set(runnerFields, "account", account);
            Set(runnerFields, "hub", hub);
            Set(runnerFields, "reviews", controller);
            Set(runnerFields, "authCanvas", GameObject.Find("Canvas"));
            Set(runnerFields, "flightCanvas",
                (GameObject)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<TravelPlanning.UI.Flights.FlightSearchPage>()).FindProperty(
                    "flightCanvas").objectReferenceValue);
            Set(runnerFields, "hubCanvas", (GameObject)new SerializedObject(hub).FindProperty("hubCanvas").objectReferenceValue);
            Set(runnerFields, "reviewsCanvas", canvas);
            runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("REVIEWS_SCENE_READY " + ScenePath);
        }

#endregion
#region UpgradePlaceCards
        private static void UpgradePlaceCards()
        {
            var root = PrefabUtility.LoadPrefabContents(DestinationHubSetup.PlaceCardPath);
            try
            {
                var card = root.GetComponent<PlaceCard>();
                var fields = new SerializedObject(card);
                if (!root.transform.Find("ReviewActions"))
                {
                    var actions = Horizontal("ReviewActions", root.transform, 48);
                    var button = Button(actions, "ViewReviewsButton", "View reviews", 250);
                    fields.FindProperty("viewReviewsButton").objectReferenceValue = button;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 292;
                }

                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    text.richText = false;
                PrefabUtility.SaveAsPrefabAsset(root, DestinationHubSetup.PlaceCardPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Reviews/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            if (MultiSceneSetup.IsMigrated)
            {
                ReleaseSetup.BuildWindows();
                return;
            }
            Prepare();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Reviews build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta"))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("REVIEWS_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

#endregion
#region CreateRow
        private static ReviewRow CreateRow()
        {
            if (File.Exists(RowPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<ReviewRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath));
            var root = Rect("ReviewRow", null);
            root.sizeDelta = new Vector2(1800, 220);
            var background = root.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(244, 247, 255, 255);
            background.raycastTarget = false;
            var layout = Vertical(root, 8);
            layout.padding = new RectOffset(20, 20, 16, 16);
            var traveler = Label(root, "TravelerName", "Traveler", 26);
            Layout(traveler.gameObject, 36);
            var ratings = Horizontal("Rating", root, 32);
            var stars = new StarGraphic[5];
            for (int i = 0; i < 5; i++)
            {
                var star = Rect("Star" + (i + 1), ratings);
                Layout(star.gameObject, 28, 0, 28);
                stars[i] = star.gameObject.AddComponent<StarGraphic>();
                stars[i].raycastTarget = false;
                stars[i].SetFilled(false);
            }

            var numericRating = Label(ratings, "RatingText", "", 24);
            Layout(numericRating.gameObject, -1, 1);
            var demo = Label(root, "DemoLabel", "", 21);
            Layout(demo.gameObject, 30);
            var body = Label(root, "Body", "", 24);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            // No fixed height on the body or row: the layout uses the full text's preferred height.
            var row = root.gameObject.AddComponent<ReviewRow>();
            var fields = new SerializedObject(row);
            Set(fields, "travelerName", traveler);
            Set(fields, "ratingText", numericRating);
            Set(fields, "demoLabel", demo);
            Set(fields, "body", body);
            var starFields = fields.FindProperty("stars");
            starFields.arraySize = 5;
            for (int i = 0; i < 5; i++)
                starFields.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<ReviewRow>();
        }

#endregion
#region Navigation
        private static void Navigation(UnityEngine.UI.Button button, UnityEngine.UI.Button previous, UnityEngine.UI.Button next)
        {
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnUp = previous,
                selectOnLeft = previous,
                selectOnDown = next,
                selectOnRight = next
            };
        }
#endregion
    }
}
