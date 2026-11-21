#if UNITY_EDITOR
using System.IO;
using TMPro;
using TravelApp.FlightMap;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TravelApp.FlightMap.Editor
{
    /// <summary>Builds only Assets/FlightMap assets. Existing scenes and project settings are never touched; pricing markers are included.</summary>
    public static class FlightMapSceneBuilder
    {
        const string Root = "Assets/FlightMap";
        const string RepairRequest = Root + "/Tests/RunRepair.request";
        static TMP_FontAsset font;

        [InitializeOnLoadMethod]
        static void RunRequestedRepair()
        {
            // Asset import workers also load editor assemblies; only the interactive Editor may
            // claim this request and serialize the scene.
            if (Application.isBatchMode || !File.Exists(RepairRequest)) return;
            // Keep the request until the build actually begins. Unity can perform a second
            // startup domain reload; deleting here used to lose the queued repair between reloads.
            EditorApplication.update += ExecuteRepairWhenReady;
        }

        static void ExecuteRepairWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= ExecuteRepairWhenReady;
            if (!File.Exists(RepairRequest)) return;
            File.Delete(RepairRequest);
            Build();
        }

        [MenuItem("Tools/Flight Map/Build Flight Map Scene")]
        public static void Build()
        {
            EnsureFolders();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new FileNotFoundException("TextMesh Pro Essentials font is required.");
            ConfigureMapTexture();
            Sprite planeSprite = CreatePlaneSprite(); Sprite markerSprite = CreateMarkerSprite();
            Sprite priceBubbleSprite = CreateRoundedPriceSprite(); Sprite pricePointerSprite = CreatePricePointerSprite();
            CreatePrefabs(planeSprite, markerSprite);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "FlightMapScene";
            var camera = new GameObject("FlightMapCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("#EAF2F7"); camera.orthographic = true;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvas = new GameObject("FlightMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas flightCanvas = canvas.GetComponent<Canvas>();
            flightCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Align overlay geometry to physical pixels so TMP glyphs and UI edges stay crisp.
            flightCanvas.pixelPerfect = true;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;

            RectTransform background = Rect("Background", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.gameObject.AddComponent<UnityEngine.UI.Image>().color = Hex("#DDEAF0");
            RectTransform header = Rect("Header", background, new Vector2(0,1), Vector2.one, new Vector2(0,-64), Vector2.zero);
            header.gameObject.AddComponent<UnityEngine.UI.Image>().color = Hex("#FFFFFF");
            Text(header, "Flight navigation", 26, FontStyles.Bold, Hex("#101828"), new Vector2(24,-7), new Vector2(-24,-35));
            Text(header, "Plan a route and run a visual flight simulation", 15, FontStyles.Normal, Hex("#475467"), new Vector2(25,-34), new Vector2(-24,-58));

            RectTransform content = Rect("Content", background, Vector2.zero, Vector2.one, new Vector2(16,12), new Vector2(-16,-76));
            RectTransform sidebar = Rect("ControlsAndInformation", content, new Vector2(0,0), new Vector2(0,1), new Vector2(0,0), new Vector2(360,0));
            sidebar.gameObject.AddComponent<UnityEngine.UI.Image>().color = Hex("#FFFFFF");
            RectTransform mapPanel = Rect("MapPanel", content, new Vector2(0,0), Vector2.one, new Vector2(376,0), Vector2.zero);
            mapPanel.gameObject.AddComponent<UnityEngine.UI.Image>().color = Hex("#B7DAE5");

            Text(sidebar, "Choose locations", 22, FontStyles.Bold, Hex("#101828"), new Vector2(20,-14), new Vector2(-20,-44));
            Text(sidebar, "Departure search", 16, FontStyles.Bold, Hex("#344054"), new Vector2(20,-52), new Vector2(-20,-74));
            TMP_InputField fromSearch = Input(sidebar, "DepartureSearch", "Airport, code, or city", -82);
            TMP_Dropdown fromDrop = Dropdown(sidebar, "DepartureDropdown", -134);
            Text(sidebar, "Destination search", 16, FontStyles.Bold, Hex("#344054"), new Vector2(20,-190), new Vector2(-20,-212));
            TMP_InputField toSearch = Input(sidebar, "DestinationSearch", "Airport, code, or city", -220);
            TMP_Dropdown toDrop = Dropdown(sidebar, "DestinationDropdown", -272);

            RectTransform actions = Rect("FlightActions", sidebar, new Vector2(0,1), Vector2.one, new Vector2(20,-378), new Vector2(-20,-330));
            var start = ButtonAnchored(actions, "StartFlightButton", "Start Flight", new Vector2(0,0), new Vector2(.48f,1), Hex("#1769E0"));
            var pause = ButtonAnchored(actions, "PauseResumeButton", "Pause / Resume", new Vector2(.5f,0), new Vector2(1,1), Hex("#344054"));
            var restart = Button(sidebar, "RestartFlightButton", "Restart Flight", new Vector2(20,-434), new Vector2(-20,-388), Hex("#667085"));
            Slider progress = Slider(sidebar, -452);

            TMP_Text fromInfo = Info(sidebar, "DepartureInfo", -478, false); TMP_Text toInfo = Info(sidebar, "DestinationInfo", -478, true);
            TMP_Text distanceInfo = Info(sidebar, "DistanceInfo", -536, false); TMP_Text durationInfo = Info(sidebar, "DurationInfo", -536, true);
            TMP_Text animationInfo = Info(sidebar, "AnimationInfo", -594, false); TMP_Text statusInfo = Info(sidebar, "StatusInfo", -594, true);

            RectTransform viewport = Rect("MapViewport", mapPanel, Vector2.zero, Vector2.one, new Vector2(18,18), new Vector2(-18,-18));
            var viewportImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); viewportImage.color = Hex("#C7E8F2");
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform mapContent = Rect("MapContent", viewport, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(-2048,-1024), new Vector2(2048,1024));
            var raw = mapContent.gameObject.AddComponent<RawImage>(); raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/MapAssets/NaturalEarthWorld.jpg"); raw.color = Color.white; raw.raycastTarget = false;
            foreach (FlightLocation location in FlightLocationDatabase.Locations) MapCityLabel(mapContent, location);

            RectTransform routeRect = Rect("GreatCircleRoute", mapContent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            routeRect.gameObject.AddComponent<CanvasRenderer>();
            FlightRouteRenderer route = routeRect.gameObject.AddComponent<FlightRouteRenderer>(); route.color = Hex("#16B8D4"); route.raycastTarget = false;
            RectTransform priceLayer = Rect("PriceMarkerLayer", mapContent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            FlightPriceMarker priceTemplate = PriceMarkerTemplate(priceLayer, priceBubbleSprite, pricePointerSprite);
            RectTransform fromMarker = Marker(mapContent, "DepartureMarker", markerSprite, Hex("#1769E0"), out TMP_Text fromLabel);
            RectTransform toMarker = Marker(mapContent, "DestinationMarker", markerSprite, Hex("#E84A5F"), out TMP_Text toLabel);
            RectTransform plane = Rect("Airplane", mapContent, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(-32,-32), new Vector2(32,32));
            var planeImage = plane.gameObject.AddComponent<UnityEngine.UI.Image>(); planeImage.sprite = planeSprite; planeImage.color = Hex("#14213D"); planeImage.raycastTarget = false;

            RectTransform zoomControls = Rect("ZoomControls", viewport, new Vector2(1,1), new Vector2(1,1), new Vector2(-72,-136), new Vector2(-16,-16));
            var zoomIn = ButtonAnchored(zoomControls, "ZoomInButton", "+", new Vector2(0,.52f), Vector2.one, Color.white, Hex("#14213D"));
            var zoomOut = ButtonAnchored(zoomControls, "ZoomOutButton", "−", Vector2.zero, new Vector2(1,.48f), Color.white, Hex("#14213D"));
            TMP_Text attribution = Text(viewport, "Map: Natural Earth (public domain) · Visual simulation only", 13, FontStyles.Normal, Hex("#374151"), new Vector2(14,8), new Vector2(600,32));
            attribution.alignment = TextAlignmentOptions.BottomLeft;
            TMP_Text arrival = Text(viewport, "Arrived at destination", 20, FontStyles.Bold, Hex("#087A55"), new Vector2(260,-58), new Vector2(-260,-14));
            arrival.alignment = TextAlignmentOptions.Center;
            TMP_Text demoPriceBadge = Text(viewport, "DEMO PRICES · NOT LIVE", 14, FontStyles.Bold, Hex("#344054"), new Vector2(14,-42), new Vector2(250,-14));
            demoPriceBadge.alignment = TextAlignmentOptions.Center;
            demoPriceBadge.gameObject.AddComponent<UnityEngine.UI.Outline>().effectColor = new Color(1f,1f,1f,.85f);
            TMP_Text selectedPrice = Text(viewport, "Selected demo fare: loading", 14, FontStyles.Bold, Hex("#101828"), new Vector2(14,-72), new Vector2(390,-44));
            selectedPrice.alignment = TextAlignmentOptions.MidlineLeft;

            FlightMapViewport viewportController = viewport.gameObject.AddComponent<FlightMapViewport>(); viewportController.Configure(viewport, mapContent);
            FlightLocationSelector selector = sidebar.gameObject.AddComponent<FlightLocationSelector>(); selector.Configure(fromSearch, fromDrop, toSearch, toDrop);
            AirplaneAnimationController animator = mapPanel.gameObject.AddComponent<AirplaneAnimationController>(); animator.Configure(plane, progress);
            FlightInformationManager info = sidebar.gameObject.AddComponent<FlightInformationManager>(); info.Configure(fromInfo,toInfo,distanceInfo,durationInfo,animationInfo,statusInfo,arrival);
            GameObject managerObject = new GameObject("FlightMapManager");
            FlightMapManager manager = managerObject.AddComponent<FlightMapManager>(); manager.Configure(selector,viewportController,route,animator,info,fromMarker,toMarker,fromLabel,toLabel);
            DemoFlightPriceDataProvider priceProvider = managerObject.AddComponent<DemoFlightPriceDataProvider>();
            FlightPriceManager priceManager = managerObject.AddComponent<FlightPriceManager>();
            priceManager.Configure(selector, viewportController, priceLayer, priceTemplate, selectedPrice, priceProvider);

            // AddListener creates a runtime-only callback. Because this code runs in Edit Mode,
            // those callbacks disappear after the scene is saved and reopened. Persistent
            // listeners serialize the target component and method into FlightMapScene.unity.
            UnityEventTools.AddPersistentListener(start.onClick, animator.StartFlight);
            UnityEventTools.AddPersistentListener(pause.onClick, animator.TogglePause);
            UnityEventTools.AddPersistentListener(restart.onClick, animator.RestartFlight);
            UnityEventTools.AddPersistentListener(zoomIn.onClick, viewportController.ZoomIn);
            UnityEventTools.AddPersistentListener(zoomOut.onClick, viewportController.ZoomOut);

            // Some uGUI setup calls can mark the root Canvas dirty and restore its default.
            // Apply this last so the serialized scene always retains pixel-aligned rendering.
            flightCanvas.pixelPerfect = true;
            EditorUtility.SetDirty(flightCanvas);
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/FlightMapScene.unity");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("FlightMapScene created at Assets/FlightMap/Scenes/FlightMapScene.unity");
        }

        static void EnsureFolders()
        {
            foreach (string folder in new[]{"Scenes","Prefabs","Sprites","MapAssets","Documentation","Editor","Tests"})
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
        }

        static void ConfigureMapTexture()
        {
            var importer = AssetImporter.GetAtPath(Root + "/MapAssets/NaturalEarthWorld.jpg") as TextureImporter;
            if (importer == null) return; importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = true; importer.mipMapBias = -0.5f; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.compressionQuality = 100;
            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true; standalone.maxTextureSize = 4096; standalone.textureCompression = TextureImporterCompression.Uncompressed; standalone.compressionQuality = 100;
            importer.SetPlatformTextureSettings(standalone); importer.SaveAndReimport();
        }

        static Sprite CreatePlaneSprite() => CreateSprite("AirplaneIcon", 96, (x,y) =>
        {
            float nx=(x-47.5f)/48f, ny=(y-47.5f)/48f;
            bool body=Mathf.Abs(nx)<.10f && ny>-.78f && ny<.82f; bool wings=Mathf.Abs(ny)<.10f && Mathf.Abs(nx)<(.8f-Mathf.Abs(ny)*2f);
            bool tail=ny<-.48f && ny>-.72f && Mathf.Abs(nx)<.33f; return body||wings||tail;
        });

        static Sprite CreateMarkerSprite() => CreateSprite("LocationMarker", 64, (x,y) =>
        {
            float nx=(x-31.5f)/32f, ny=(y-34f)/32f; float circle=nx*nx+ny*ny;
            bool head=circle<.55f; bool tail=y<29 && y>2 && Mathf.Abs(nx)<(y/64f); bool hole=circle<.12f; return (head||tail)&&!hole;
        });

        static Sprite CreatePricePointerSprite() => CreateSprite("PricePointer", 32, (x,y) =>
        {
            float halfWidth = (31f - y) * .5f;
            return y >= 4 && y <= 30 && Mathf.Abs(x - 15.5f) <= halfWidth;
        });

        static Sprite CreateRoundedPriceSprite()
        {
            const int size = 64, radius = 14;
            Sprite sprite = CreateSprite("PriceBubble", size, (x,y) =>
            {
                float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                float dx = x - cx, dy = y - cy;
                return dx * dx + dy * dy <= radius * radius;
            });
            string path = $"{Root}/Sprites/PriceBubble.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spriteBorder = new Vector4(radius, radius, radius, radius);
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite CreateSprite(string name, int size, System.Func<int,int,bool> inside)
        {
            string path=$"{Root}/Sprites/{name}.png"; var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color32[size*size]; for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=inside(x,y)?new Color32(255,255,255,255):new Color32(255,255,255,0);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=size; importer.mipmapEnabled=false; importer.alphaIsTransparency=true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void CreatePrefabs(Sprite plane, Sprite marker)
        {
            var p=new GameObject("Airplane",typeof(RectTransform),typeof(UnityEngine.UI.Image)); p.GetComponent<UnityEngine.UI.Image>().sprite=plane; p.GetComponent<RectTransform>().sizeDelta=new Vector2(44,44);
            PrefabUtility.SaveAsPrefabAsset(p,Root+"/Prefabs/Airplane.prefab"); Object.DestroyImmediate(p);
            var m=new GameObject("LocationMarker",typeof(RectTransform),typeof(UnityEngine.UI.Image)); m.GetComponent<UnityEngine.UI.Image>().sprite=marker; m.GetComponent<RectTransform>().sizeDelta=new Vector2(36,44);
            PrefabUtility.SaveAsPrefabAsset(m,Root+"/Prefabs/LocationMarker.prefab"); Object.DestroyImmediate(m);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        { var go=new GameObject(name,typeof(RectTransform)); var r=go.GetComponent<RectTransform>(); r.SetParent(parent,false); r.anchorMin=anchorMin;r.anchorMax=anchorMax;r.offsetMin=offsetMin;r.offsetMax=offsetMax; return r; }

        static TMP_Text Text(Transform parent,string value,float size,FontStyles style,Color color,Vector2 min,Vector2 max)
        {
            if (min == Vector2.zero && max == Vector2.zero)
            {
                var fill = Rect("Text_" + value.Split('\n')[0], parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var fillText = fill.gameObject.AddComponent<TextMeshProUGUI>(); fillText.font=font;fillText.text=value;fillText.fontSize=size;fillText.fontStyle=style;fillText.color=color;fillText.alignment=TextAlignmentOptions.MidlineLeft;fillText.raycastTarget=false;fillText.textWrappingMode=TextWrappingModes.Normal;return fillText;
            }
            // Top-anchored fixed-height controls use negative Y offsets. Normalize callers that
            // provide visual top then bottom so RectTransform never receives a negative height.
            if (min.y > max.y) (min.y, max.y) = (max.y, min.y);
            var r=Rect("Text_"+value.Split('\n')[0],parent,new Vector2(0,1),new Vector2(1,1),min,max); var t=r.gameObject.AddComponent<TextMeshProUGUI>(); t.font=font;t.text=value;t.fontSize=size;t.fontStyle=style;t.color=color;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;
        }

        static TMP_InputField Input(Transform parent,string name,string placeholder,float top)
        {
            var r=Rect(name,parent,new Vector2(0,1),new Vector2(1,1),new Vector2(24,top-44),new Vector2(-24,top)); var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=Hex("#F5F7FA");
            var field=r.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic=img;
            var text=InsetText(r,"",19,FontStyles.Normal,Hex("#101828"),12,12,5,5); var ph=InsetText(r,placeholder,17,FontStyles.Normal,Hex("#667085"),12,12,5,5);
            field.textComponent=text;field.placeholder=ph;field.lineType=TMP_InputField.LineType.SingleLine;return field;
        }

        static TMP_Dropdown Dropdown(Transform parent,string name,float top)
        {
            var r=Rect(name,parent,new Vector2(0,1),new Vector2(1,1),new Vector2(24,top-46),new Vector2(-24,top));var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=Color.white;
            var d=r.gameObject.AddComponent<TMP_Dropdown>();d.targetGraphic=img;var label=InsetText(r,"Select location",18,FontStyles.Normal,Hex("#101828"),12,32,4,4);d.captionText=label;
            var template=Rect("Template",r,new Vector2(0,0),new Vector2(1,0),new Vector2(0,-250),new Vector2(0,0));template.gameObject.AddComponent<UnityEngine.UI.Image>().color=Color.white;var scroll=template.gameObject.AddComponent<ScrollRect>();
            var viewport=Rect("Viewport",template,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color=Color.white;viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport,new Vector2(0,1),Vector2.one,new Vector2(0,-250),Vector2.zero);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            var item=Rect("Item",content,new Vector2(0,1),Vector2.one,new Vector2(0,-36),Vector2.zero);var toggle=item.gameObject.AddComponent<Toggle>();
            var itemLabel=InsetText(item,"Option",17,FontStyles.Normal,Hex("#101828"),10,8,2,2);toggle.targetGraphic=itemLabel.GetComponent<UnityEngine.UI.Graphic>();d.template=template;d.itemText=itemLabel;template.gameObject.SetActive(false);return d;
        }

        static UnityEngine.UI.Button ButtonAnchored(Transform parent,string name,string label,Vector2 aMin,Vector2 aMax,Color bg,Color? fg=null)
        { var r=Rect(name,parent,aMin,aMax,Vector2.zero,Vector2.zero);var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=bg;var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=img;var t=Text(r,label,17,FontStyles.Bold,fg??Color.white,Vector2.zero,Vector2.zero);t.alignment=TextAlignmentOptions.Center;return b; }
        static UnityEngine.UI.Button Button(Transform parent,string name,string label,Vector2 min,Vector2 max,Color bg)
        { var r=Rect(name,parent,new Vector2(0,1),new Vector2(1,1),min,max);var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=bg;var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=img;var t=Text(r,label,17,FontStyles.Bold,Color.white,Vector2.zero,Vector2.zero);t.alignment=TextAlignmentOptions.Center;return b; }

        static Slider Slider(Transform parent,float top)
        { var r=Rect("FlightProgress",parent,new Vector2(0,1),new Vector2(1,1),new Vector2(24,top-18),new Vector2(-24,top));var bg=r.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=Hex("#D8DEE8");var fill=Rect("Fill",r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);fill.gameObject.AddComponent<UnityEngine.UI.Image>().color=Hex("#1769E0");var s=r.gameObject.AddComponent<Slider>();s.fillRect=fill;s.targetGraphic=bg;s.interactable=false;return s; }
        static TMP_Text InsetText(Transform parent,string value,float size,FontStyles style,Color color,float left,float right,float bottom,float top)
        { var t=Text(parent,value,size,style,color,Vector2.zero,Vector2.zero);t.rectTransform.offsetMin=new Vector2(left,bottom);t.rectTransform.offsetMax=new Vector2(-right,-top);return t; }
        static TMP_Text Info(Transform parent,string name,float top,bool right)
        { float left=right?182:20,rightEdge=right?20:182;var t=Text(parent,name.Replace("Info",""),16,FontStyles.Normal,Hex("#1D2939"),new Vector2(left,top-50),new Vector2(-rightEdge,top));t.gameObject.name=name;return t;}
        static void MapCityLabel(Transform parent, FlightLocation location)
        {
            Vector2 uv=FlightGeoMath.Equirectangular01(location.latitude,location.longitude);
            var r=Rect("CityLabel_"+location.code,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-70,-14),new Vector2(70,14));
            r.anchoredPosition=new Vector2((uv.x-.5f)*4096f,(uv.y-.5f)*2048f);
            var t=Text(r,location.city+" · "+location.code,13,FontStyles.Bold,Hex("#475467"),Vector2.zero,Vector2.zero);t.alignment=TextAlignmentOptions.Center;
        }
        static FlightPriceMarker PriceMarkerTemplate(Transform parent, Sprite bubbleSprite, Sprite pointerSprite)
        {
            var root = Rect("DemoPriceTemplate", parent, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, Vector2.zero);
            root.pivot = new Vector2(.5f,0f); root.sizeDelta = new Vector2(94,64); root.anchoredPosition = Vector2.zero;

            var shadow = Rect("Shadow", root, Vector2.zero, Vector2.one, new Vector2(6,9), new Vector2(-2,-5));
            var shadowImage = shadow.gameObject.AddComponent<UnityEngine.UI.Image>(); shadowImage.sprite=bubbleSprite; shadowImage.type=UnityEngine.UI.Image.Type.Sliced; shadowImage.color=new Color(0.06f,0.09f,0.16f,.22f); shadowImage.raycastTarget=false;
            var pointerShadow = Rect("PointerShadow", root, new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(-9,1), new Vector2(9,15));
            var pointerShadowImage=pointerShadow.gameObject.AddComponent<UnityEngine.UI.Image>();pointerShadowImage.sprite=pointerSprite;pointerShadowImage.color=new Color(0.06f,0.09f,0.16f,.22f);pointerShadowImage.raycastTarget=false;
            var pointer = Rect("Pointer", root, new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(-8,4), new Vector2(8,17));
            var pointerImage=pointer.gameObject.AddComponent<UnityEngine.UI.Image>();pointerImage.sprite=pointerSprite;pointerImage.color=Color.white;pointerImage.raycastTarget=false;

            var bubble = Rect("PriceBubble", root, Vector2.zero, Vector2.one, new Vector2(4,12), new Vector2(-4,-2));
            var image=bubble.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=bubbleSprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;
            var outline=bubble.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=Hex("#667085");outline.effectDistance=new Vector2(1,-1);
            var button=bubble.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            TMP_Text price=Text(bubble,"$000",18,FontStyles.Bold,Hex("#101828"),Vector2.zero,Vector2.zero);price.alignment=TextAlignmentOptions.Center;price.rectTransform.anchorMin=new Vector2(0,.34f);price.rectTransform.anchorMax=Vector2.one;price.rectTransform.offsetMin=Vector2.zero;price.rectTransform.offsetMax=Vector2.zero;
            TMP_Text code=Text(bubble,"---",12,FontStyles.Bold,Hex("#475467"),Vector2.zero,Vector2.zero);code.alignment=TextAlignmentOptions.Center;code.rectTransform.anchorMin=Vector2.zero;code.rectTransform.anchorMax=new Vector2(1,.42f);code.rectTransform.offsetMin=Vector2.zero;code.rectTransform.offsetMax=Vector2.zero;
            FlightPriceMarker marker=root.gameObject.AddComponent<FlightPriceMarker>();marker.ConfigureVisual(button,price,code);root.gameObject.SetActive(false);return marker;
        }
        static RectTransform Marker(Transform parent,string name,Sprite sprite,Color color,out TMP_Text label)
        { var r=Rect(name,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-24,-29),new Vector2(24,29));var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.sprite=sprite;i.color=color;i.raycastTarget=false;label=Text(r,"---",18,FontStyles.Bold,Hex("#101828"),new Vector2(32,8),new Vector2(116,36));return r; }
        static Color Hex(string value){ColorUtility.TryParseHtmlString(value,out Color c);return c;}
    }
}
#endif
