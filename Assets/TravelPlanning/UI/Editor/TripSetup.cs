using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Trips;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds saved-trip controls while preserving the existing login and feature screens.</summary>
    public abstract class TripSetup : EditorUiBuilderBase
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/SavedTrips/TravelPlannerTrips.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Trips/SavedTripRow.prefab";
#region Prepare
        [MenuItem("Travel Planning/Saved Trips/1 - Prepare Saved Trips")]
        public static void Prepare()
        {
            if (MultiSceneSetup.OpenMigratedProject()) return;
            ReviewSetup.Prepare();
            UpgradeCards();
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>();
            var hub = UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>();
            var reviews = UnityEngine.Object.FindFirstObjectByType<ReviewsPage>();
            var authCanvas = GameObject.Find("Canvas");
            var flightCanvas = (GameObject)new SerializedObject(flights).FindProperty("flightCanvas").objectReferenceValue;
            var hubCanvas = (GameObject)new SerializedObject(hub).FindProperty("hubCanvas").objectReferenceValue;
            var existing = UnityEngine.Object.FindFirstObjectByType<SavedTripsPage>();
            if (existing)
            {
                AssignUnderlyingGroups(existing, authCanvas, flightCanvas, hubCanvas);
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log("SAVED_TRIPS_SCENE_READY " + ScenePath);
                return;
            }

            CreateScreen(account, flights, hub, reviews, authCanvas, flightCanvas, hubCanvas);
        }

#endregion
#region CreateScreen
        // Creation runs only when this feature has not already been prepared.
        private static void CreateScreen(LoginPage account, FlightSearchPage flights, DestinationHubPage hub, ReviewsPage reviews,
            GameObject authCanvas, GameObject flightCanvas, GameObject hubCanvas)
        {
            var signedIn = authCanvas.transform.Find("Right Panel/SignedInCard");
            var logout = signedIn.Find("LogoutButton");
            var home = UnityEngine.Object.Instantiate(logout.gameObject, signedIn).GetComponent<UnityEngine.UI.Button>();
            home.name = "SavedTripsButton";
            home.GetComponentInChildren<TMP_Text>().text = "Saved trips";
            home.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -460);
            logout.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -540);
            var flightEntry = Button(flightCanvas.transform.Find("Main/Header"), "SavedTripsButton", "Saved trips", 210);
            flightEntry.transform.SetSiblingIndex(2);
            var hubEntry = Button(hubCanvas.transform.Find("Main/Header"), "SavedTripsButton", "Saved trips", 210);
            hubEntry.transform.SetSiblingIndex(1);
            var canvas = CreateOverlayCanvas("SavedTripsCanvas", 5);
            var blocker = Rect("InputBlocker", canvas.transform);
            Stretch(blocker);
            blocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvas.transform);
            Stretch(main);
            main.offsetMin = new Vector2(40, 32);
            main.offsetMax = new Vector2(-40, -32);
            Vertical(main, 12);
            var header = Horizontal("Header", main, 64);
            var title = Label(header, "Title", "Saved trips", 40);
            Layout(title.gameObject, -1, 1);
            var close = Button(header, "CloseButton", "Close", 180);
            var preview = Label(main, "TargetPreview", "", 25);
            Layout(preview.gameObject, 50);
            var selection = Horizontal("TripSelection", main, 56);
            var tripLabel = Label(selection, "Label", "Your trip", 24);
            Layout(tripLabel.gameObject, -1, 0, 150);
            var dropdownTemplate = flightCanvas.transform.Find("Main/RouteFields/DepartureDate/Dropdown").gameObject;
            var choice = Dropdown(dropdownTemplate, selection, "TripChoice");
            Layout(choice.gameObject, 54, 1);
            var retry = Button(selection, "RetryButton", "Retry", 160);
            var form = Horizontal("CreateForm", main, 88);
            var nameCell = Cell(form, "TripName", "New trip name (1-60 characters)");
            var nameObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/FilterFields/MaximumPrice/Input").gameObject, nameCell);
            nameObject.name = "Input";
            var name = nameObject.GetComponent<TMP_InputField>();
            name.characterLimit = 60;
            name.contentType = TMP_InputField.ContentType.Standard;
            ((TMP_Text)name.placeholder).text = "For example: Summer in London";
            foreach (var text in nameObject.GetComponentsInChildren<TMP_Text>(true))
                text.richText = false;
            Layout(nameObject, 54);
            var start = Dropdown(dropdownTemplate, Cell(form, "StartDate", "Planning start date"), "Dropdown");
            var end = Dropdown(dropdownTemplate, Cell(form, "EndDate", "Planning end date"), "Dropdown");
            var create = Button(Cell(form, "CreateAction", "Create a new plan"), "CreateButton", "Create trip", 220);
            var note = Label(main, "PlanningNote", "Planning dates do not change flight dates. Current sample prices; saving does not reserve or purchase anything.", 22);
            Layout(note.gameObject, 44);
            var actions = Horizontal("SaveActions", main, 48);
            var save = Button(actions, "SaveSelectedButton", "Save selected item", 300);
            var status = Label(main, "Status", "", 23);
            Layout(status.gameObject, 50);
            var info = Label(main, "TripInfo", "", 23);
            Layout(info.gameObject, 44);
            var scroll = CreateScrollableList(main, 16);
            var controller = new GameObject("SavedTripsController").AddComponent<SavedTripsPage>();
            var fields = new SerializedObject(controller);
            Set(fields, "account", account);
            Set(fields, "flights", flights);
            Set(fields, "hub", hub);
            Set(fields, "reviews", reviews);
            Set(fields, "modalCanvas", canvas);
            Set(fields, "homeButton", home);
            Set(fields, "flightButton", flightEntry);
            Set(fields, "hubButton", hubEntry);
            Set(fields, "closeButton", close);
            Set(fields, "createButton", create);
            Set(fields, "saveButton", save);
            Set(fields, "retryButton", retry);
            Set(fields, "tripName", name);
            Set(fields, "tripChoice", choice);
            Set(fields, "startDate", start);
            Set(fields, "endDate", end);
            Set(fields, "targetPreview", preview);
            Set(fields, "status", status);
            Set(fields, "tripInfo", info);
            Set(fields, "scroll", scroll);
            Set(fields, "rowPrefab", CreateRow());
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssignUnderlyingGroups(controller, authCanvas, flightCanvas, hubCanvas);
            var runner = controller.gameObject.AddComponent<TripSmokeRunner>();
            var runnerFields = new SerializedObject(runner);
            Set(runnerFields, "account", account);
            Set(runnerFields, "flights", flights);
            Set(runnerFields, "hub", hub);
            Set(runnerFields, "trips", controller);
            Set(runnerFields, "authCanvas", authCanvas);
            Set(runnerFields, "flightCanvas", flightCanvas);
            Set(runnerFields, "hubCanvas", hubCanvas);
            Set(runnerFields, "tripsCanvas", canvas);
            runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("SAVED_TRIPS_SCENE_READY " + ScenePath);
        }

#endregion
#region AssignUnderlyingGroups
        private static void AssignUnderlyingGroups(SavedTripsPage controller, params GameObject[] sources)
        {
            var components = new CanvasGroup[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                // Unity objects have their own null check; a missing native component can retain a managed wrapper.
                var group = sources[i].GetComponent<CanvasGroup>();
                if (!group)
                    group = sources[i].AddComponent<CanvasGroup>();
                if (!group)
                    throw new InvalidOperationException("Could not add the input group to " + sources[i].name);
                components[i] = group;
            }

            var fields = new SerializedObject(controller);
            var groups = fields.FindProperty("underlyingGroups");
            groups.arraySize = components.Length;
            for (int i = 0; i < components.Length; i++)
                groups.GetArrayElementAtIndex(i).objectReferenceValue = components[i];
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
                var component = flight ? (UnityEngine.Object)root.GetComponent<FlightResultRow>() : root.GetComponent<PlaceCard>();
                var fields = new SerializedObject(component);
                if (!fields.FindProperty("saveButton").objectReferenceValue)
                {
                    var actions = flight ? Horizontal("SaveActions", root.transform, 48) : root.transform.Find("ReviewActions").GetComponent<RectTransform>();
                    var save = Button(actions, "SaveToTripButton", "Save to trip", 230);
                    Set(fields, "saveButton", save);
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    if (flight)
                        root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 200;
                }

                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    text.richText = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Saved Trips/2 - Build Windows x64")]
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
                throw new InvalidOperationException("Saved trips build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta"))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("SAVED_TRIPS_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

#endregion
#region CreateRow
        private static SavedTripRow CreateRow()
        {
            if (File.Exists(RowPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<SavedTripRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath));
            var root = Rect("SavedTripRow", null);
            root.sizeDelta = new Vector2(1800, 210);
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            var layout = Vertical(root, 8);
            layout.padding = new RectOffset(20, 20, 16, 16);
            var top = Horizontal("Top", root, 48);
            var title = Label(top, "Title", "", 27);
            Layout(title.gameObject, -1, 1);
            var remove = Button(top, "RemoveButton", "Remove item", 240);
            var price = Label(root, "Price", "", 24);
            Layout(price.gameObject, 34);
            var details = Label(root, "Details", "", 23);
            details.textWrappingMode = TextWrappingModes.Normal;
            var row = root.gameObject.AddComponent<SavedTripRow>();
            var fields = new SerializedObject(row);
            Set(fields, "title", title);
            Set(fields, "details", details);
            Set(fields, "price", price);
            Set(fields, "removeButton", remove);
            fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<SavedTripRow>();
        }

#endregion
#region Dropdown
        private static TMP_Dropdown Dropdown(GameObject template, Transform parent, string name)
        {
            var obj = UnityEngine.Object.Instantiate(template, parent);
            obj.name = name;
            Layout(obj, 54);
            foreach (var text in obj.GetComponentsInChildren<TMP_Text>(true))
                text.richText = false;
            var dropdown = obj.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            return dropdown;
        }

#endregion
#region Cell
        private static RectTransform Cell(Transform parent, string name, string caption)
        {
            var cell = Rect(name, parent);
            Layout(cell.gameObject, -1, 1);
            Vertical(cell, 4);
            var label = Label(cell, "Label", caption, 21);
            Layout(label.gameObject, 30);
            return cell;
        }
#endregion
    }
}
