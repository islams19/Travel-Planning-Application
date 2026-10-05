using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Trips;
using TravelPlanning.UI.Tracking;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds local price watches and notifications to the existing feature screens.</summary>
    public abstract class TrackingSetup : EditorUiBuilderBase
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/PriceTracking/TravelPlannerTracking.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Tracking/PriceNoticeRow.prefab";
#region Prepare
        [MenuItem("Travel Planning/Price Tracking/1 - Prepare Price Tracking")]
        public static void Prepare()
        {
            if (MultiSceneSetup.OpenMigratedProject()) return;
            TripSetup.Prepare();
            UpgradeCards();
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>();
            var hub = UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>();
            var authCanvas = GameObject.Find("Canvas");
            var flightCanvas = (GameObject)new SerializedObject(flights).FindProperty("flightCanvas").objectReferenceValue;
            var hubCanvas = (GameObject)new SerializedObject(hub).FindProperty("hubCanvas").objectReferenceValue;
            var existing = UnityEngine.Object.FindFirstObjectByType<PriceTrackingPage>();
            if (existing)
            {
                AssignGroups(existing, authCanvas, flightCanvas, hubCanvas);
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log("PRICE_TRACKING_SCENE_READY " + ScenePath);
                return;
            }

            CreateScreen(account, flights, hub, authCanvas, flightCanvas, hubCanvas);
        }

#endregion
#region CreateScreen
        // Creation runs only when this feature has not already been prepared.
        private static void CreateScreen(LoginPage account, FlightSearchPage flights, DestinationHubPage hub, GameObject authCanvas, GameObject flightCanvas, GameObject hubCanvas)
        {
            var homeRoot = authCanvas.transform.Find("Right Panel/SignedInCard");
            var logout = homeRoot.Find("LogoutButton");
            var home = UnityEngine.Object.Instantiate(logout.gameObject, homeRoot).GetComponent<UnityEngine.UI.Button>();
            home.name = "NotificationsButton";
            home.GetComponentInChildren<TMP_Text>().text = "Notifications";
            home.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -540);
            logout.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -620);
            var flightEntry = Button(flightCanvas.transform.Find("Main/Header"), "NotificationsButton", "Notifications", 240);
            flightEntry.transform.SetSiblingIndex(3);
            var hubEntry = Button(hubCanvas.transform.Find("Main/Header"), "NotificationsButton", "Notifications", 240);
            hubEntry.transform.SetSiblingIndex(2);
            var badgeLabels = new[]
            {
                Badge(home),
                Badge(flightEntry),
                Badge(hubEntry)
            };
            var canvas = CreateOverlayCanvas("PriceTrackingCanvas", 6);
            var blocker = Rect("InputBlocker", canvas.transform);
            Stretch(blocker);
            blocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvas.transform);
            Stretch(main);
            main.offsetMin = new Vector2(40, 32);
            main.offsetMax = new Vector2(-40, -32);
            Vertical(main, 12);
            var header = Horizontal("Header", main, 64);
            var title = Label(header, "Title", "Price tracking and notifications", 40);
            Layout(title.gameObject, -1, 1);
            var close = Button(header, "CloseButton", "Close", 180);
            var preview = Label(main, "TargetPreview", "", 25);
            Layout(preview.gameObject, 80);
            var selection = Horizontal("WatchSelection", main, 56);
            var selectionLabel = Label(selection, "Label", "Your watches", 24);
            Layout(selectionLabel.gameObject, -1, 0, 200);
            var dropdownObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/RouteFields/DepartureDate/Dropdown").gameObject, selection);
            dropdownObject.name = "Dropdown";
            Layout(dropdownObject, 54, 1);
            var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            foreach (var text in dropdownObject.GetComponentsInChildren<TMP_Text>(true))
                text.richText = false;
            var refresh = Button(selection, "RefreshButton", "Refresh", 160);
            var actions = Horizontal("TargetActions", main, 48);
            var track = Button(actions, "TrackButton", "Track price", 250);
            var readAll = Button(actions, "ReadAllButton", "Mark all read", 250);
            var demo = Rect("DemoPanel", main);
            Vertical(demo, 8);
            Layout(demo.gameObject, 152);
            var demoTitle = Label(demo, "Title", "DEMO ONLY — change a shared sample price on this installation", 25);
            Layout(demoTitle.gameObject, 34);
            var demoControls = Horizontal("Controls", demo, 54);
            var priceObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/FilterFields/MaximumPrice/Input").gameObject, demoControls);
            priceObject.name = "PriceInput";
            Layout(priceObject, 54, 1);
            var priceInput = priceObject.GetComponent<TMP_InputField>();
            ((TMP_Text)priceInput.placeholder).text = "New sample price in USD";
            foreach (var text in priceObject.GetComponentsInChildren<TMP_Text>(true))
                text.richText = false;
            var apply = Button(demoControls, "ApplyButton", "Apply demo price", 300);
            var demoFeedback = Label(demo, "Feedback", "", 22);
            Layout(demoFeedback.gameObject, 48);
            var status = Label(main, "Status", "", 23);
            Layout(status.gameObject, 58);
            var unread = Label(main, "UnreadSummary", "", 25);
            Layout(unread.gameObject, 36);
            var scroll = CreateScrollableList(main, 16);
            var root = new GameObject("PriceTrackingController");
            var controller = root.AddComponent<PriceTrackingPage>();
            var badge = root.AddComponent<NotificationBadge>();
            var badgeFields = new SerializedObject(badge);
            Set(badgeFields, "account", account);
            var labels = badgeFields.FindProperty("labels");
            labels.arraySize = 3;
            for (int i = 0; i < 3; i++)
                labels.GetArrayElementAtIndex(i).objectReferenceValue = badgeLabels[i];
            badgeFields.ApplyModifiedPropertiesWithoutUndo();
            var fields = new SerializedObject(controller);
            Set(fields, "account", account);
            Set(fields, "flights", flights);
            Set(fields, "hub", hub);
            Set(fields, "reviews", UnityEngine.Object.FindFirstObjectByType<ReviewsPage>());
            Set(fields, "trips", UnityEngine.Object.FindFirstObjectByType<SavedTripsPage>());
            Set(fields, "badge", badge);
            Set(fields, "modalCanvas", canvas);
            Set(fields, "demoPanel", demo.gameObject);
            Set(fields, "homeButton", home);
            Set(fields, "flightButton", flightEntry);
            Set(fields, "hubButton", hubEntry);
            Set(fields, "closeButton", close);
            Set(fields, "refreshButton", refresh);
            Set(fields, "trackButton", track);
            Set(fields, "readAllButton", readAll);
            Set(fields, "applyButton", apply);
            Set(fields, "watchChoice", dropdown);
            Set(fields, "demoPrice", priceInput);
            Set(fields, "targetPreview", preview);
            Set(fields, "status", status);
            Set(fields, "unreadSummary", unread);
            Set(fields, "trackLabel", track.GetComponentInChildren<TMP_Text>());
            Set(fields, "demoFeedback", demoFeedback);
            Set(fields, "scroll", scroll);
            Set(fields, "rowPrefab", CreateRow());
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssignGroups(controller, authCanvas, flightCanvas, hubCanvas);
            var runner = root.AddComponent<TrackingSmokeRunner>();
            var runnerFields = new SerializedObject(runner);
            Set(runnerFields, "account", account);
            Set(runnerFields, "flights", flights);
            Set(runnerFields, "hub", hub);
            Set(runnerFields, "tracking", controller);
            Set(runnerFields, "badge", badge);
            Set(runnerFields, "authCanvas", authCanvas);
            Set(runnerFields, "flightCanvas", flightCanvas);
            Set(runnerFields, "hubCanvas", hubCanvas);
            Set(runnerFields, "trackingCanvas", canvas);
            runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("PRICE_TRACKING_SCENE_READY " + ScenePath);
        }

#endregion
#region AssignGroups
        private static void AssignGroups(PriceTrackingPage controller, params GameObject[] sources)
        {
            var groups = new CanvasGroup[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                var group = sources[i].GetComponent<CanvasGroup>();
                if (!group)
                    group = sources[i].AddComponent<CanvasGroup>();
                groups[i] = group;
            }

            var fields = new SerializedObject(controller);
            var property = fields.FindProperty("underlyingGroups");
            property.arraySize = groups.Length;
            for (int i = 0; i < groups.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = groups[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

#endregion
#region UpgradeCards
        private static void UpgradeCards()
        {
            UpgradeCard(FlightSearchSetup.RowPath, true);
            UpgradeCard(DestinationHubSetup.PlaceCardPath, false);
        }

#endregion
#region UpgradeCard
        private static void UpgradeCard(string path, bool flight)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var fields = new SerializedObject(flight ? (UnityEngine.Object)root.GetComponent<FlightResultRow>() : root.GetComponent<PlaceCard>());
                if (!fields.FindProperty("trackButton").objectReferenceValue)
                {
                    var actions = root.transform.Find(flight ? "SaveActions" : "ReviewActions");
                    var button = Button(actions, "TrackPriceButton", "Track price", 230);
                    Set(fields, "trackButton", button);
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Price Tracking/2 - Build Windows x64")]
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
                throw new InvalidOperationException("Price tracking build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta"))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("PRICE_TRACKING_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

#endregion
#region CreateRow
        private static PriceNoticeRow CreateRow()
        {
            if (File.Exists(RowPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<PriceNoticeRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath));
            var root = Rect("PriceNoticeRow", null);
            root.sizeDelta = new Vector2(1800, 210);
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            var layout = Vertical(root, 8);
            layout.padding = new RectOffset(20, 20, 16, 16);
            var top = Horizontal("Top", root, 48);
            var title = Label(top, "Title", "", 26);
            Layout(title.gameObject, -1, 1);
            var read = Button(top, "ReadButton", "Mark read", 230);
            var body = Label(root, "Body", "", 24);
            body.textWrappingMode = TextWrappingModes.Normal;
            var timestamp = Label(root, "Timestamp", "", 21);
            Layout(timestamp.gameObject, 30);
            var row = root.gameObject.AddComponent<PriceNoticeRow>();
            var fields = new SerializedObject(row);
            Set(fields, "title", title);
            Set(fields, "body", body);
            Set(fields, "timestamp", timestamp);
            Set(fields, "readButton", read);
            Set(fields, "actionLabel", read.GetComponentInChildren<TMP_Text>());
            fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<PriceNoticeRow>();
        }

#endregion
#region Badge
        private static TMP_Text Badge(UnityEngine.UI.Button button)
        {
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (caption)
            {
                var offset = caption.rectTransform.offsetMax;
                offset.x = -44;
                caption.rectTransform.offsetMax = offset;
            }

            var badge = Label(button.transform, "UnreadBadge", "", 20);
            badge.color = Color.white;
            badge.alignment = TextAlignmentOptions.Center;
            var rect = badge.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.sizeDelta = new Vector2(38, 32);
            rect.anchoredPosition = new Vector2(-25, 0);
            badge.gameObject.SetActive(false);
            return badge;
        }
#endregion
    }
}
