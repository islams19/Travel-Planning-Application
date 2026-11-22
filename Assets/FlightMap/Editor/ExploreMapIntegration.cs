#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TravelApp.FlightMap.Editor
{
    // Repackages the existing, wired scene content; does not recreate map behavior or app navigation.
    public static class ExploreMapIntegration
    {
        const string MapScene = "Assets/FlightMap/Scenes/FlightMapScene.unity";
        const string DesktopScene = "Assets/Scenes/DesktopLayout.unity";
        const string PrefabPath = "Assets/FlightMap/Prefabs/ExploreWorldMap.prefab";
        [MenuItem("Tools/Flight Map/Integrate Explore World")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var desktop = SceneManager.GetSceneByPath(DesktopScene);
            if (!desktop.isLoaded) desktop = EditorSceneManager.OpenScene(DesktopScene);
            var page = desktop.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .Single(rect => rect.name == "exploreworld_page");
            var source = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Additive);
            try
            {
                var canvas = source.GetRootGameObjects().Single(root => root.name == "FlightMapCanvas");
                var content = (RectTransform)canvas.transform.Find("Background/Content");
                var manager = source.GetRootGameObjects().Single(root => root.name == "FlightMapManager");
                var root = new GameObject("ExploreWorldMap", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, source);
                var rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = new Vector2(40,30); rootRect.offsetMax = new Vector2(-40,-30);
                content.SetParent(rootRect, false);
                content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
                content.offsetMin = content.offsetMax = Vector2.zero;
                manager.transform.SetParent(rootRect, false);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = page.gameObject.layer;
                // Reuse the current app palette, with the shared header left entirely untouched.
                var controls = content.Find("ControlsAndInformation").GetComponent<Image>();
                var mapPanel = content.Find("MapPanel").GetComponent<Image>();
                var rounded = RoundedSprite();
                controls.sprite = mapPanel.sprite = rounded;
                controls.type = mapPanel.type = Image.Type.Sliced;
                controls.color = mapPanel.color = Color.white;
                foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    var image = button.GetComponent<Image>();
                    if (image != null) { image.sprite = rounded; image.type = Image.Type.Sliced; }
                    if (button.name == "StartFlightButton") image.color = new Color32(47,80,247,255);
                }
                foreach (var input in controls.GetComponentsInChildren<TMP_InputField>(true))
                {
                    var image = input.GetComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced;
                }
                foreach (var dropdown in controls.GetComponentsInChildren<TMP_Dropdown>(true))
                {
                    FlightLocationSelector.RepairDropdownTemplate(dropdown);
                    var image = dropdown.GetComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced;
                    image.color = new Color32(241,244,249,255);
                    dropdown.captionText.rectTransform.offsetMax += new Vector2(-26,0);
                    var arrow = new GameObject("Dropdown indicator", typeof(RectTransform), typeof(TextMeshProUGUI));
                    arrow.transform.SetParent(dropdown.transform, false);
                    var text = arrow.GetComponent<TextMeshProUGUI>(); text.font = dropdown.captionText.font;
                    text.text = "v"; text.fontSize = 16; text.color = new Color32(47,80,247,255);
                    text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                    text.rectTransform.anchorMin = new Vector2(1,0); text.rectTransform.anchorMax = Vector2.one;
                    text.rectTransform.offsetMin = new Vector2(-34,0); text.rectTransform.offsetMax = new Vector2(-8,0);
                }
                var viewport = (RectTransform)content.Find("MapPanel/MapViewport");
                var plane = viewport.Find("MapContent/Airplane").GetComponent<Image>();
                plane.sprite = TopDownPlaneSprite(); plane.color = new Color32(29,65,117,255);
                var banner = new GameObject("Demo fare banner",typeof(RectTransform),typeof(Image));
                banner.layer = page.gameObject.layer; banner.transform.SetParent(viewport,false);
                var bannerRect=(RectTransform)banner.transform;
                bannerRect.anchorMin=bannerRect.anchorMax=bannerRect.pivot=Vector2.up;
                bannerRect.anchoredPosition=new Vector2(12,-12);bannerRect.sizeDelta=new Vector2(470,78);
                var bannerImage=banner.GetComponent<Image>();bannerImage.sprite=rounded;bannerImage.type=Image.Type.Sliced;
                bannerImage.color=new Color(1,1,1,.97f);bannerImage.raycastTarget=false;
                var badge=viewport.GetComponentsInChildren<TMP_Text>(true).Single(text=>text.text.StartsWith("Flight prices"));
                var priceFields=new SerializedObject(manager.GetComponent<FlightPriceManager>());
                var selectedPrice=(TMP_Text)priceFields.FindProperty("selectedPriceText").objectReferenceValue;
                PlaceBannerText(badge,banner.transform,10,8,450,24,15,new Color32(47,80,247,255));
                PlaceBannerText(selectedPrice,banner.transform,10,36,450,30,16,new Color32(29,40,59,255));
                var infoFields=new SerializedObject(controls.GetComponent<FlightInformationManager>());
                var arrival=(TMP_Text)infoFields.FindProperty("arrivalText").objectReferenceValue;
                arrival.rectTransform.anchorMin=arrival.rectTransform.anchorMax=new Vector2(.5f,0);
                arrival.rectTransform.pivot=new Vector2(.5f,0);arrival.rectTransform.anchoredPosition=new Vector2(0,44);
                arrival.rectTransform.sizeDelta=new Vector2(500,44);
                // Keep the existing animation system wired, but expose only route planning UI on Explore.
                foreach (string name in new[] { "StartFlightButton", "PauseResumeButton", "RestartFlightButton", "FlightProgress", "AnimationInfo", "StatusInfo" })
                    controls.GetComponentsInChildren<Transform>(true).Single(child => child.name == name).gameObject.SetActive(false);
                arrival.gameObject.SetActive(false);
                foreach (string name in new[] { "DepartureInfo", "DestinationInfo", "DistanceInfo", "DurationInfo" })
                {
                    var rect = (RectTransform)controls.transform.Find(name);
                    rect.anchoredPosition += new Vector2(0, 140);
                }
                infoFields.FindProperty("compactRouteDetails").boolValue = true;
                infoFields.ApplyModifiedPropertiesWithoutUndo();
                var managerFields = new SerializedObject(manager.GetComponent<FlightMapManager>());
                managerFields.FindProperty("automaticallyAnimateRoutes").boolValue = true;
                managerFields.ApplyModifiedPropertiesWithoutUndo();
                var animationFields = new SerializedObject(mapPanel.GetComponent<AirplaneAnimationController>());
                animationFields.FindProperty("demonstrationDurationSeconds").floatValue = 3f;
                animationFields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.CloseScene(source, true); }
            // Replace only an earlier instance from this integrator, never unrelated page content.
            var previous = page.Find("ExploreWorldMap");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), page);
            var background = page.GetComponent<Image>();
            background.sprite = null; background.color = new Color32(245,247,250,255);
            EditorSceneManager.MarkSceneDirty(desktop);
            EditorSceneManager.SaveScene(desktop);
        }

        static void PlaceBannerText(TMP_Text text,Transform parent,float x,float y,float width,float height,float size,Color color)
        {
            text.transform.SetParent(parent,false);
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=Vector2.up;
            text.rectTransform.anchoredPosition=new Vector2(x,-y);text.rectTransform.sizeDelta=new Vector2(width,height);
            text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Left;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;text.raycastTarget=false;
        }
        static Sprite TopDownPlaneSprite()
        {
            const string path="Assets/FlightMap/Sprites/TopDownAirplane.png";
            const int size=192;
            var outline=new[]{new Vector2(0,.94f),new Vector2(.065f,.78f),new Vector2(.075f,.22f),
                new Vector2(.78f,-.08f),new Vector2(.78f,-.2f),new Vector2(.08f,-.05f),
                new Vector2(.065f,-.61f),new Vector2(.31f,-.79f),new Vector2(.31f,-.9f),
                new Vector2(.04f,-.78f),new Vector2(0,-.83f),new Vector2(-.04f,-.78f),
                new Vector2(-.31f,-.9f),new Vector2(-.31f,-.79f),new Vector2(-.065f,-.61f),
                new Vector2(-.08f,-.05f),new Vector2(-.78f,-.2f),new Vector2(-.78f,-.08f),
                new Vector2(-.075f,.22f),new Vector2(-.065f,.78f)};
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Vector2 p=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1);bool inside=false;
                for(int i=0,j=outline.Length-1;i<outline.Length;j=i++)
                    if((outline[i].y>p.y)!=(outline[j].y>p.y) && p.x<(outline[j].x-outline[i].x)*(p.y-outline[i].y)/(outline[j].y-outline[i].y)+outline[i].x)inside=!inside;
                pixels[y*size+x]=new Color32(255,255,255,inside?(byte)255:(byte)0);
            }
            texture.SetPixels32(pixels);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite RoundedSprite()
        {
            const string path = "Assets/FlightMap/Sprites/ExplorePanel.png";
            const int size = 64, radius = 14;
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels = new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=x-Mathf.Clamp(x,radius,size-1-radius),dy=y-Mathf.Clamp(y,radius,size-1-radius);
                pixels[y*size+x]=new Color32(255,255,255,dx*dx+dy*dy<=radius*radius?(byte)255:(byte)0);
            }
            texture.SetPixels32(pixels);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=new Vector4(16,16,16,16);
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
