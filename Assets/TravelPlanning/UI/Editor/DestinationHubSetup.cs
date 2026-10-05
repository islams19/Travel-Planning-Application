using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Builds the destination screen separately from the team's existing login and flight screens.</summary>
    public abstract class DestinationHubSetup : EditorUiBuilderBase
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/DestinationHub/TravelPlannerDestinations.exe";
        public const string PlaceCardPath = "Assets/TravelPlanning/Prefabs/Destinations/PlaceCard.prefab";
        private new static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

#region Prepare
        [MenuItem("Travel Planning/Destination Hub/1 - Prepare Destination Hub")]
        public static void Prepare()
        {
            if (MultiSceneSetup.OpenMigratedProject()) return;
            FlightSearchSetup.Prepare();
            if (UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>() != null)
                return;
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>();
            var flightFields = new SerializedObject(flights);
            var flightCanvas = (GameObject)flightFields.FindProperty("flightCanvas").objectReferenceValue;
            var flightHeader = flightCanvas.transform.Find("Main/Header");
            var open = CloneButton(flightHeader.Find("BackButton").gameObject, flightHeader, "ExploreDestinationButton", "Explore destination", 300);
            open.transform.SetSiblingIndex(1);
            var canvas = new GameObject("DestinationCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 3;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0;
            var background = Rect("Background", canvas.transform);
            Stretch(background);
            background.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvas.transform);
            Stretch(main);
            main.offsetMin = new Vector2(40, 32);
            main.offsetMax = new Vector2(-40, -32);
            Vertical(main, 16);
            var header = Horizontal("Header", main, 68);
            var title = Label(header, "Title", "Explore your destination", 42);
            Layout(title.gameObject, -1, 1);
            var back = CloneButton(flightHeader.Find("BackButton").gameObject, header, "BackButton", "Back to flights", 250);
            var logout = CloneButton(flightHeader.Find("LogoutButton").gameObject, header, "LogoutButton", "Log out", 160);
            var selection = Horizontal("DestinationSelection", main, 60);
            var label = Label(selection, "Label", "Destination", 26);
            Layout(label.gameObject, -1, 0, 180);
            var dropdownObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/RouteFields/Destination/Dropdown").gameObject, selection);
            dropdownObject.name = "Dropdown";
            Layout(dropdownObject, 54, 1);
            var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            var retry = CloneButton(flightHeader.Find("BackButton").gameObject, selection, "RetryButton", "Retry", 160);
            var description = Label(main, "Description", "", 24);
            Layout(description.gameObject, 64);
            var note = Label(main, "DemoNote", "USD sample prices • Fictional demo reviews • Public hotspots are free; optional services are excluded", 22);
            Layout(note.gameObject, 34);
            var status = Label(main, "Status", "", 22);
            Layout(status.gameObject, 46);
            var scrollObject = Rect("ScrollView", main);
            Layout(scrollObject.gameObject, -1, 1);
            var scroll = scrollObject.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            var viewport = Rect("Viewport", scrollObject);
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            Vertical(content, 28);
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            var hotels = Section(content, "Hotels", "Hotels — sample room rates per night");
            var restaurants = Section(content, "Restaurants", "Restaurants — sample meal budgets per person");
            var experiences = Section(content, "Experiences", "Experiences — sample activities per adult");
            var hotspots = Section(content, "Hotspots", "Hotspots — public places to explore");
            var controller = new GameObject("DestinationController").AddComponent<DestinationHubPage>();
            var fields = new SerializedObject(controller);
            Set(fields, "account", account);
            Set(fields, "flights", flights);
            Set(fields, "hubCanvas", canvas);
            Set(fields, "destination", dropdown);
            Set(fields, "description", description);
            Set(fields, "status", status);
            Set(fields, "openButton", open);
            Set(fields, "backButton", back);
            Set(fields, "logoutButton", logout);
            Set(fields, "retryButton", retry);
            Set(fields, "scroll", scroll);
            Set(fields, "hotels", hotels);
            Set(fields, "restaurants", restaurants);
            Set(fields, "experiences", experiences);
            Set(fields, "hotspots", hotspots);
            Set(fields, "cardPrefab", CreateCard());
            fields.ApplyModifiedPropertiesWithoutUndo();
            var runner = controller.gameObject.AddComponent<DestinationSmokeRunner>();
            var runnerFields = new SerializedObject(runner);
            Set(runnerFields, "account", account);
            Set(runnerFields, "flights", flights);
            Set(runnerFields, "hub", controller);
            Set(runnerFields, "authCanvas", GameObject.Find("Canvas"));
            Set(runnerFields, "flightCanvas", flightCanvas);
            Set(runnerFields, "hubCanvas", canvas);
            runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("DESTINATION_HUB_SCENE_READY " + ScenePath);
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Destination Hub/2 - Build Windows x64")]
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
                throw new InvalidOperationException("Destination hub build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta"))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("DESTINATION_HUB_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

#endregion
#region Section
        private static PlaceSection Section(Transform parent, string name, string title)
        {
            var root = Rect(name, parent);
            Vertical(root, 12);
            var heading = Label(root, "Heading", title, 30);
            Layout(heading.gameObject, 46);
            var empty = Label(root, "EmptyMessage", "No " + name.ToLowerInvariant() + " have been added here yet.", 24);
            Layout(empty.gameObject, 44);
            var items = Rect("Items", root);
            Vertical(items, 14);
            var section = root.gameObject.AddComponent<PlaceSection>();
            var fields = new SerializedObject(section);
            Set(fields, "emptyMessage", empty);
            Set(fields, "items", items);
            fields.ApplyModifiedPropertiesWithoutUndo();
            empty.gameObject.SetActive(false);
            return section;
        }

#endregion
#region CreateCard
        private static PlaceCard CreateCard()
        {
            if (File.Exists(PlaceCardPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(PlaceCardPath).GetComponent<PlaceCard>();
            Directory.CreateDirectory(Path.GetDirectoryName(PlaceCardPath));
            var root = Rect("PlaceCard", null);
            root.sizeDelta = new Vector2(1800, 232);
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            Layout(root.gameObject, 232);
            var layout = Vertical(root, 8);
            layout.padding = new RectOffset(20, 20, 16, 16);
            var top = Horizontal("Top", root, 40);
            var name = Label(top, "PlaceName", "Place", 28);
            Layout(name.gameObject, -1, 1);
            var price = Label(top, "Price", "", 26);
            Layout(price.gameObject, -1, 0, 520);
            price.alignment = TextAlignmentOptions.Right;
            var rating = Label(root, "Rating", "", 22);
            Layout(rating.gameObject, 28);
            var description = Label(root, "Description", "", 22);
            Layout(description.gameObject, 64);
            var address = Label(root, "Address", "", 22);
            Layout(address.gameObject, 34);
            var card = root.gameObject.AddComponent<PlaceCard>();
            var fields = new SerializedObject(card);
            Set(fields, "placeName", name);
            Set(fields, "price", price);
            Set(fields, "rating", rating);
            Set(fields, "description", description);
            Set(fields, "address", address);
            fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PlaceCardPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<PlaceCard>();
        }

#endregion
#region CloneButton
        private static UnityEngine.UI.Button CloneButton(GameObject template, Transform parent, string name, string label, float width)
        {
            var obj = UnityEngine.Object.Instantiate(template, parent);
            obj.name = name;
            obj.GetComponentInChildren<TMP_Text>().text = label;
            Layout(obj, 56, 0, width);
            return obj.GetComponent<UnityEngine.UI.Button>();
        }

#endregion
#region Horizontal
        private new static RectTransform Horizontal(string name, Transform parent, float height)
        {
            var rect = Rect(name, parent);
            Layout(rect.gameObject, height);
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return rect;
        }

#endregion
#region Vertical
        private new static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform rect, int spacing)
        {
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

#endregion
#region Label
        private new static TMP_Text Label(Transform parent, string name, string text, int size)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = new Color32(51, 51, 51, 255);
            label.raycastTarget = false;
            return label;
        }
#endregion
    }
}
