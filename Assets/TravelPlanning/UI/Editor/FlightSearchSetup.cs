using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Flights;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds the independent flight screen without changing the login form.</summary>
    public abstract class FlightSearchSetup : EditorUiBuilderBase
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/FlightSearch/TravelPlannerFlights.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Flights/FlightResultRow.prefab";
        private static readonly Color Blue = new Color32(49, 87, 255, 255);
        private new static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

#region Prepare
        [MenuItem("Travel Planning/Flight Search/1 - Prepare Flight Search")]
        public static void Prepare()
        {
            if (MultiSceneSetup.OpenMigratedProject()) return;
            AuthSetup.Prepare();
            if (UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>() != null)
                return;
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            var authCanvas = GameObject.Find("Canvas");
            var accountFields = new SerializedObject(account);
            var signedIn = ((GameObject)accountFields.FindProperty("signedInPanel").objectReferenceValue).transform;
            var next = signedIn.Find("NextMilestone").GetComponent<TMP_Text>();
            next.text = "Search sample flights for your next trip.";
            next.rectTransform.anchoredPosition = new Vector2(0, -270);
            next.rectTransform.sizeDelta = new Vector2(580, 65);
            var open = UnityEngine.Object.Instantiate(signedIn.Find("LogoutButton").gameObject, signedIn).GetComponent<UnityEngine.UI.Button>();
            open.name = "SearchFlightsButton";
            open.GetComponentInChildren<TMP_Text>().text = "Search flights";
            var openRect = open.GetComponent<RectTransform>();
            openRect.anchoredPosition = new Vector2(openRect.anchoredPosition.x, -380);
            var canvasObject = new GameObject("FlightCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 2;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0;
            var background = Rect("Background", canvasObject.transform);
            Stretch(background);
            background.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvasObject.transform);
            Stretch(main);
            main.offsetMin = new Vector2(40, 32);
            main.offsetMax = new Vector2(-40, -32);
            Vertical(main, 18);
            var header = Horizontal("Header", main, 68);
            var title = Label(header, "Title", "Flight search", 42);
            Layout(title.gameObject, -1, 1);
            var back = Button(header, "BackButton", "Back", 160);
            var logout = Button(header, "LogoutButton", "Log out", 160);
            var note = Label(main, "CatalogNote", "Sample catalog • Fixed June 2027 dates • Prices in USD per flight • Times local to each airport", 22);
            Layout(note.gameObject, 36);
            var route = Horizontal("RouteFields", main, 90);
            var origin = Dropdown(route, "Origin", "Origin airport");
            var destination = Dropdown(route, "Destination", "Destination airport");
            var departure = Dropdown(route, "DepartureDate", "Departure date");
            var returning = Dropdown(route, "ReturnDate", "Return date");
            var filters = Horizontal("FilterFields", main, 90);
            var priceCell = Cell(filters, "MaximumPrice", "Maximum USD per flight");
            var maxPriceObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            maxPriceObject.name = "Input";
            maxPriceObject.transform.SetParent(priceCell, false);
            Layout(maxPriceObject, 54);
            var maxPrice = maxPriceObject.GetComponent<TMP_InputField>();
            maxPrice.characterLimit = 20;
            maxPrice.contentType = TMP_InputField.ContentType.Standard;
            maxPriceObject.GetComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            ((TMP_Text)maxPrice.placeholder).text = "Any price";
            Style(maxPriceObject);
            var airline = Dropdown(filters, "Airline", "Airline");
            var time = Dropdown(filters, "TimeBand", "Departure time");
            var sort = Dropdown(filters, "Sort", "Sort both directions");
            var actions = Horizontal("Actions", main, 56);
            var search = Button(actions, "SearchButton", "Search flights", 250);
            var clear = Button(actions, "ClearFiltersButton", "Clear filters", 220);
            var help = Label(actions, "ApplyHint", "Change the filters, then choose Search flights.", 22);
            Layout(help.gameObject, -1, 1);
            var status = Label(main, "Status", "", 23);
            Layout(status.gameObject, 60);
            var lists = Horizontal("Results", main, -1);
            Layout(lists.gameObject, -1, 1);
            var outbound = Results(lists, "Outbound", out TMP_Text outboundHeading, out RectTransform outboundContent);
            var inbound = Results(lists, "Return", out TMP_Text returnHeading, out RectTransform returnContent);
            var controller = new GameObject("FlightController").AddComponent<FlightSearchPage>();
            var fields = new SerializedObject(controller);
            Set(fields, "account", account);
            Set(fields, "authCanvas", authCanvas);
            Set(fields, "flightCanvas", canvasObject);
            Set(fields, "origin", origin);
            Set(fields, "destination", destination);
            Set(fields, "departureDate", departure);
            Set(fields, "returnDate", returning);
            Set(fields, "airline", airline);
            Set(fields, "timeBand", time);
            Set(fields, "sort", sort);
            Set(fields, "maximumPrice", maxPrice);
            Set(fields, "status", status);
            Set(fields, "outboundHeading", outboundHeading);
            Set(fields, "returnHeading", returnHeading);
            Set(fields, "openButton", open);
            Set(fields, "searchButton", search);
            Set(fields, "clearButton", clear);
            Set(fields, "backButton", back);
            Set(fields, "logoutButton", logout);
            Set(fields, "outboundContent", outboundContent);
            Set(fields, "returnContent", returnContent);
            Set(fields, "outboundScroll", outbound);
            Set(fields, "returnScroll", inbound);
            Set(fields, "rowPrefab", CreateRow());
            fields.ApplyModifiedPropertiesWithoutUndo();
            var smoke = controller.gameObject.AddComponent<FlightSmokeRunner>();
            var smokeFields = new SerializedObject(smoke);
            Set(smokeFields, "account", account);
            Set(smokeFields, "flights", controller);
            Set(smokeFields, "authCanvas", authCanvas);
            Set(smokeFields, "flightCanvas", canvasObject);
            smokeFields.ApplyModifiedPropertiesWithoutUndo();
            canvasObject.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("FLIGHT_SEARCH_SCENE_READY " + ScenePath);
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Flight Search/2 - Build Windows x64")]
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
                throw new InvalidOperationException("Flight search build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta"))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("FLIGHT_SEARCH_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

#endregion
#region CreateRow
        private static FlightResultRow CreateRow()
        {
            if (File.Exists(RowPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<FlightResultRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath));
            var root = Rect("FlightResultRow", null);
            root.sizeDelta = new Vector2(850, 140);
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            var layout = Vertical(root, 8);
            layout.padding = new RectOffset(18, 18, 14, 14);
            Layout(root.gameObject, 140);
            var top = Horizontal("Top", root, 38);
            var airline = Label(top, "Airline", "Airline", 26);
            Layout(airline.gameObject, -1, 1);
            var price = Label(top, "Price", "USD 0.00", 26);
            Layout(price.gameObject, -1, 0, 220);
            price.alignment = TextAlignmentOptions.Right;
            var route = Label(root, "Route", "Route", 22);
            Layout(route.gameObject, 64);
            var row = root.gameObject.AddComponent<FlightResultRow>();
            var fields = new SerializedObject(row);
            Set(fields, "airlineText", airline);
            Set(fields, "priceText", price);
            Set(fields, "routeText", route);
            fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<FlightResultRow>();
        }

#endregion
#region Results
        private static UnityEngine.UI.ScrollRect Results(Transform parent, string name, out TMP_Text heading, out RectTransform content)
        {
            var column = Rect(name, parent);
            Layout(column.gameObject, -1, 1);
            Vertical(column, 12);
            heading = Label(column, "Heading", name, 24);
            Layout(heading.gameObject, 48);
            var scroll = Rect("ScrollView", column);
            Layout(scroll.gameObject, -1, 1);
            var component = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            component.horizontal = false;
            component.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            component.scrollSensitivity = 40;
            var viewport = Rect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            Vertical(content, 12);
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            component.content = content;
            component.viewport = viewport;
            return component;
        }

#endregion
#region Dropdown
        private static TMP_Dropdown Dropdown(Transform parent, string name, string caption)
        {
            var cell = Cell(parent, name, caption);
            var obj = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            obj.name = "Dropdown";
            obj.transform.SetParent(cell, false);
            Layout(obj, 54);
            Style(obj);
            obj.GetComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            var arrow = obj.transform.Find("Arrow");
            UnityEngine.Object.DestroyImmediate(arrow.GetComponent<UnityEngine.UI.Image>());
            var arrowText = arrow.gameObject.AddComponent<TextMeshProUGUI>();
            arrowText.font = Font;
            arrowText.text = "v";
            arrowText.fontSize = 20;
            arrowText.color = Blue;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.raycastTarget = false;
            var dropdown = obj.GetComponent<TMP_Dropdown>();
            var template = dropdown.template;
            template.sizeDelta = new Vector2(template.sizeDelta.x, 280);
            var item = template.Find("Viewport/Content/Item").GetComponent<RectTransform>();
            item.sizeDelta = new Vector2(0, 40);
            template.Find("Viewport/Content").GetComponent<RectTransform>().sizeDelta = new Vector2(0, 48);
            var mark = item.Find("Item Checkmark").GetComponent<UnityEngine.UI.Image>();
            mark.color = Blue;
            mark.rectTransform.sizeDelta = new Vector2(10, 10);
            return dropdown;
        }

#endregion
#region Cell
        private static RectTransform Cell(Transform parent, string name, string caption)
        {
            var cell = Rect(name, parent);
            Layout(cell.gameObject, -1, 1);
            Vertical(cell, 4);
            var label = Label(cell, "Label", caption, 22);
            Layout(label.gameObject, 30);
            return cell;
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
#region Button
        private new static UnityEngine.UI.Button Button(Transform parent, string name, string text, float width)
        {
            var rect = Rect(name, parent);
            Layout(rect.gameObject, 56, 0, width);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Blue;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var label = Label(rect, "Label", text, 24);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            return button;
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
#region Style
        private static void Style(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = Font;
                text.fontSize = 22;
                text.color = new Color32(51, 51, 51, 255);
            }
        }
#endregion
    }
}
