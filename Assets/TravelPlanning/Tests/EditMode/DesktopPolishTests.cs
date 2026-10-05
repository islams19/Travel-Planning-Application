using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TravelPlanning.UI.Desktop;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class DesktopPolishTests
    {
        private static int backCalls;
#region CountBack
        private static void CountBack() => backCalls++;
#endregion
#region TabSkipsUnavailableControlsWrapsAndRestoresScreenFocus
        [UnityTest]
        public IEnumerator TabSkipsUnavailableControlsWrapsAndRestoresScreenFocus()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            // EventSystem registers itself in OnEnable during Play mode.
            yield return new EnterPlayMode();
            new GameObject("EventSystem", typeof(EventSystem));
            var baseCanvas = Canvas("Base", 0);
            var modal = Canvas("Modal", 6);
            var first = Button(baseCanvas.transform, "First", new Vector2(-100, 100));
            var second = Button(baseCanvas.transform, "Second", new Vector2(100, 100));
            var disabled = Button(baseCanvas.transform, "Disabled", Vector2.zero);
            disabled.interactable = false;
            var hidden = Button(baseCanvas.transform, "Hidden", new Vector2(0, -100));
            hidden.gameObject.SetActive(false);
            var close = Button(modal.transform, "Close", Vector2.zero);
            var navigation = Navigator(new[] { baseCanvas, modal }, new[] { first, close });
            modal.gameObject.SetActive(false);
            CollectionAssert.AreEqual(new[] { first, second }, DesktopKeyboardNavigation.GetTabOrder(baseCanvas));
            navigation.UpdateScreenFocus();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            navigation.MoveFocus(true);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            navigation.RememberCurrentFocus();
            modal.gameObject.SetActive(true);
            navigation.UpdateScreenFocus();
            navigation.MoveFocus(false);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
            modal.gameObject.SetActive(false);
            navigation.UpdateScreenFocus();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            second.gameObject.SetActive(false);
            navigation.UpdateScreenFocus();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(first.gameObject), "Same-canvas panel changes must repair inactive focus.");
            var group = baseCanvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            Assert.That(DesktopKeyboardNavigation.GetTabOrder(baseCanvas), Is.Empty);
            Object.Destroy(navigation.gameObject);
            Object.Destroy(baseCanvas.gameObject);
            Object.Destroy(modal.gameObject);
            Object.Destroy(EventSystem.current.gameObject);
            yield return null;
            yield return new ExitPlayMode();
        }

#endregion
#region KeyboardSelectionRevealsAnOffscreenRow
        [Test]
        public void KeyboardSelectionRevealsAnOffscreenRow()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var canvas = Canvas("Base", 0);
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(canvas.transform, false);
            viewport.sizeDelta = new Vector2(400, 200);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(400, 800);
            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            var row = Button(content, "Far row", new Vector2(0, -650));
            var rect = (RectTransform)row.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            DesktopKeyboardNavigation.Reveal(row);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row.transform);
            Assert.That(content.anchoredPosition.y, Is.GreaterThan(400));
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 1));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 1));
        }

#endregion
#region ExpandedReferenceLayoutKeepsScrollViewportsInsideCanvas
        [TestCase(1000, 700)]
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(1920, 600)]
        public void ExpandedReferenceLayoutKeepsScrollViewportsInsideCanvas(int width, int height)
        {
            ApplicationSceneFixture.Open();
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(canvas => canvas.transform.parent == null).ToArray();
            Assert.That(canvases.Length, Is.EqualTo(8));
            // Simulate Expand's logical canvas extent; Windows capture validation checks physical pixels separately.
            float scale = Mathf.Min(width / 1920f, height / 1080f);
            foreach (var canvas in canvases)
            {
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                Assert.That(scaler.screenMatchMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.gameObject.SetActive(true);
                var rect = (RectTransform)canvas.transform;
                rect.sizeDelta = new Vector2(width / scale, height / scale);
                rect.localScale = Vector3.one;
                UnityEngine.Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                foreach (var scroll in canvas.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Where(item => !item.GetComponentInParent<TMP_Dropdown>()))
                {
                    Assert.That(scroll.viewport, Is.Not.Null);
                    Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200), canvas.name);
                    Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100), canvas.name);
                    // Measure the viewport itself; scroll content is intentionally allowed outside it.
                    var corners = new Vector3[4];
                    scroll.viewport.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = rect.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(rect.rect.xMin - 1, rect.rect.xMax + 1), canvas.name);
                        Assert.That(local.y, Is.InRange(rect.rect.yMin - 1, rect.rect.yMax + 1), canvas.name);
                    }
                }
            }
        }

#endregion
#region EscapeClosesDropdownBeforeInvokingScreenBack
        [UnityTest]
        public IEnumerator EscapeClosesDropdownBeforeInvokingScreenBack()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            yield return new EnterPlayMode();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = Canvas("Base", 0);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            // A captured local would live in a compiler closure lost across EnterPlayMode's domain reload.
            var back = Button(canvas.transform, "Back", Vector2.zero);
            backCalls = 0;
            back.onClick.AddListener(CountBack);
            var dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            dropdownObject.transform.SetParent(canvas.transform, false);
            var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            var navigation = Navigator(new[] { canvas }, new[] { back });
            yield return null;
            dropdown.Show();
            yield return null;
            Assert.That(dropdown.IsExpanded, Is.True);
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            navigation.MoveFocus(false);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject), "Tab must not leave an expanded dropdown.");
            navigation.HandleEscape();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(dropdown.IsExpanded, Is.False);
            Assert.That(backCalls, Is.Zero);
            navigation.HandleEscape();
            Assert.That(backCalls, Is.EqualTo(1));
            Object.Destroy(navigation.gameObject);
            Object.Destroy(canvas.gameObject);
            Object.Destroy(EventSystem.current.gameObject);
            yield return null;
            yield return new ExitPlayMode();
        }

#endregion
#region Canvas
        private static Canvas Canvas(string name, int order)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = order;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            return canvas;
        }

#endregion
#region Button
        private static UnityEngine.UI.Button Button(Transform parent, string name, Vector2 position)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(100, 40);
            rect.anchoredPosition = position;
            return button;
        }

#endregion
#region Navigator
        private static DesktopKeyboardNavigation Navigator(Canvas[] canvases, UnityEngine.UI.Button[] defaults)
        {
            var navigation = new GameObject("Navigation").AddComponent<DesktopKeyboardNavigation>();
            navigation.enabled = false;
            var fields = new SerializedObject(navigation);
            var screens = fields.FindProperty("screens");
            screens.arraySize = canvases.Length;
            for (int i = 0; i < canvases.Length; i++)
            {
                var screen = screens.GetArrayElementAtIndex(i);
                screen.FindPropertyRelative("canvas").objectReferenceValue = canvases[i];
                screen.FindPropertyRelative("defaultFocus").objectReferenceValue = defaults[i];
                screen.FindPropertyRelative("backButton").objectReferenceValue = defaults[i];
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            return navigation;
        }
#endregion
    }
}
