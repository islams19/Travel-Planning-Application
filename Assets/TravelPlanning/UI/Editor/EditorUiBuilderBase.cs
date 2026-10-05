using TMPro;
using UnityEditor;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Shared creation primitives; screen builders still own their hierarchy and wiring.</summary>
    public abstract class EditorUiBuilderBase
    {
        /// <summary>Create the standard screen surface; callers provide its content.</summary>
        protected static GameObject CreateOverlayCanvas(string name, int sortingOrder)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = sortingOrder;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0;
            return canvas;
        }

        /// <summary>Create a masked list whose content grows with its child rows.</summary>
        protected static UnityEngine.UI.ScrollRect CreateScrollableList(Transform parent, int spacing)
        {
            var root = Rect("ScrollView", parent);
            Layout(root.gameObject, -1, 1);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 45;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            Vertical(content, spacing);
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            return scroll;
        }

        protected static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        #region Label
        protected static TMP_Text Label(Transform parent, string name, string text, int size)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = new Color32(51, 51, 51, 255);
            label.raycastTarget = false;
            label.richText = false;
            return label;
        }
        #endregion

        #region Rect
        protected static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }
        #endregion

        #region Stretch
        protected static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        #endregion

        #region Layout
        protected static void Layout(GameObject obj, float height = -1, float flexible = 0, float width = -1)
        {
            var layout = obj.GetComponent<UnityEngine.UI.LayoutElement>();
            if (!layout) layout = obj.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleHeight = flexible;
            layout.flexibleWidth = flexible;
            layout.preferredWidth = width;
        }
        #endregion

#region Button
        protected static UnityEngine.UI.Button Button(Transform parent, string name, string text, float width)
        {
            var rect = Rect(name, parent);
            Layout(rect.gameObject, 48, 0, width);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color32(49, 87, 255, 255);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var label = Label(rect, "Label", text, 24);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            return button;
        }

#endregion
#region Horizontal
        protected static RectTransform Horizontal(string name, Transform parent, float height)
        {
            var rect = Rect(name, parent);
            Layout(rect.gameObject, height);
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return rect;
        }

#endregion
#region Vertical
        protected static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform rect, int spacing)
        {
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

#endregion




#region Set
        protected static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
#endregion
    }
}
