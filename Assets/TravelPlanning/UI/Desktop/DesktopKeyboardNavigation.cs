using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TravelPlanning.UI.Desktop
{
    /// <summary>Provides desktop Tab and Escape navigation using the legacy Input Manager.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class DesktopKeyboardNavigation : MonoBehaviour
    {
        [Serializable]
        public struct ScreenBinding
        {
            public Canvas canvas;
            public UnityEngine.UI.Button backButton;
            public UnityEngine.UI.Selectable defaultFocus;
        }

        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private ScreenBinding[] screens;
        private readonly Dictionary<Canvas, GameObject> rememberedFocus = new Dictionary<Canvas, GameObject>();
        private Canvas previousScreen;
        private TMP_Dropdown previousExpanded;
        public Canvas ActiveScreen => FindActiveScreen();

        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            string[] back = { null, "Right Panel/RegistrationCard/Create an account", "Right Panel/SignedInCard/LogoutButton", "Main/Header/BackButton", "Main/Header/BackButton", "Main/Header/CloseButton", "Main/Header/CloseButton", "Main/Header/CloseButton" };
            string[] focus = { "Right Panel/LoginCard/EmailInput", "Right Panel/RegistrationCard/EmailInput", "Right Panel/SignedInCard/SearchFlightsButton", "Main/RouteFields/Origin/Dropdown", "Main/DestinationSelection/Dropdown", "Main/Header/CloseButton", "Main/TripSelection/TripChoice", "Main/WatchSelection/Dropdown" };
            screens = scene.Canvases.Select((canvas, index) => new ScreenBinding {
                canvas = canvas.GetComponent<Canvas>(),
                backButton = back[index] == null ? null : canvas.transform.Find(back[index]).GetComponent<UnityEngine.UI.Button>(),
                defaultFocus = canvas.transform.Find(focus[index]).GetComponent<UnityEngine.UI.Selectable>()
            }).ToArray();
        }

        private void OnEnable()
        {
            if (account)
            {
                account.ExternalKeyboardNavigation = true;
            }
        }

        private void Start()
        {
            if (!account || screens == null || screens.Length == 0 || screens.Any(binding => !binding.canvas))
            {
                Debug.LogError("DesktopKeyboardNavigation: assign the account and desktop screen bindings.");
                enabled = false;
                return;
            }

            account.ExternalKeyboardNavigation = true;
        }

        private void LateUpdate()
        {
            if (!EventSystem.current)
            {
                return;
            }

            UpdateScreenFocus();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape();
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            }

            RememberCurrentFocus();
            previousExpanded = FindExpanded(FindActiveScreen());
        }

        public void RememberCurrentFocus()
        {
            if (!EventSystem.current)
            {
                return;
            }

            var screen = FindActiveScreen();
            var selected = EventSystem.current.currentSelectedGameObject;
            if (screen && IsEligible(selected, screen))
            {
                rememberedFocus[screen] = selected;
            }
        }

        public void UpdateScreenFocus()
        {
            var screen = FindActiveScreen();
            if (!screen || !EventSystem.current)
            {
                return;
            }

            if (screen == previousScreen && IsEligible(EventSystem.current.currentSelectedGameObject, screen))
            {
                return;
            }

            previousScreen = screen;
            if (IsEligible(EventSystem.current.currentSelectedGameObject, screen))
            {
                return;
            }

            if (rememberedFocus.TryGetValue(screen, out GameObject remembered) && IsEligible(remembered, screen))
            {
                Select(remembered.GetComponent<UnityEngine.UI.Selectable>());
                return;
            }

            var binding = screens.First(item => item.canvas == screen);
            if (binding.defaultFocus && IsEligible(binding.defaultFocus.gameObject, screen))
            {
                Select(binding.defaultFocus);
            }
            else
            {
                var order = GetTabOrder(screen);
                if (order.Count > 0)
                {
                    Select(order[0]);
                }
            }
        }

        public void MoveFocus(bool backwards)
        {
            var screen = FindActiveScreen();
            if (!screen || FindExpanded(screen) || !EventSystem.current)
            {
                return;
            }

            var order = GetTabOrder(screen);
            if (order.Count == 0)
            {
                return;
            }

            int current = order.FindIndex(item => item.gameObject == EventSystem.current.currentSelectedGameObject);
            int next = current < 0 ? backwards ? order.Count - 1 : 0 : (current + (backwards ? -1 : 1) + order.Count) % order.Count;
            Select(order[next]);
        }

        public void HandleEscape()
        {
            var screen = FindActiveScreen();
            if (!screen)
            {
                return;
            }

            // StandaloneInputModule may already have hidden the popup earlier in this frame.
            var expanded = FindExpanded(screen);
            if (!expanded && previousExpanded && previousExpanded.transform.IsChildOf(screen.transform))
            {
                expanded = previousExpanded;
            }

            if (expanded)
            {
                expanded.Hide();
                Select(expanded);
                previousExpanded = null;
                return;
            }

            var binding = screens.First(item => item.canvas == screen);
            if (binding.backButton && binding.backButton.gameObject.activeInHierarchy && binding.backButton.IsInteractable())
            {
                binding.backButton.onClick.Invoke();
                UpdateScreenFocus();
            }
        }

        private Canvas FindActiveScreen()
        {
            if (screens == null)
            {
                return null;
            }

            Canvas top = null;
            foreach (var binding in screens)
            {
                if (binding.canvas && binding.canvas.enabled && binding.canvas.gameObject.activeInHierarchy && (!top || binding.canvas.sortingOrder > top.sortingOrder))
                {
                    top = binding.canvas;
                }
            }

            return top;
        }

        private static TMP_Dropdown FindExpanded(Canvas screen) => screen ? screen.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(dropdown => dropdown.IsExpanded) : null;
        public static List<UnityEngine.UI.Selectable> GetTabOrder(Canvas screen)
        {
            if (!screen)
            {
                return new List<UnityEngine.UI.Selectable>();
            }

            // Positions are measured in reference-canvas units, keeping the order stable as the window resizes.
            return screen.GetComponentsInChildren<UnityEngine.UI.Selectable>()
                .Where(item => IsEligible(item.gameObject, screen))
                .OrderByDescending(item => Mathf.RoundToInt(CanvasPosition(item, screen).y / 8f))
                .ThenBy(item => CanvasPosition(item, screen).x)
                .ThenBy(item => item.GetInstanceID())
                .ToList();
        }

        private static Vector3 CanvasPosition(UnityEngine.UI.Selectable item, Canvas screen)
        {
            var rect = (RectTransform)item.transform;
            return screen.transform.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        }

        private static bool IsEligible(GameObject obj, Canvas screen)
        {
            if (!obj || !screen || !obj.activeInHierarchy || !obj.transform.IsChildOf(screen.transform))
            {
                return false;
            }

            var selectable = obj.GetComponent<UnityEngine.UI.Selectable>();
            return selectable && selectable.IsActive() && selectable.IsInteractable() && selectable.navigation.mode != UnityEngine.UI.Navigation.Mode.None;
        }

        private static void Select(UnityEngine.UI.Selectable selectable)
        {
            if (!selectable || !EventSystem.current)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            if (selectable is TMP_InputField input)
            {
                input.ActivateInputField();
            }

            Reveal(selectable);
        }

        public static void Reveal(UnityEngine.UI.Selectable selectable)
        {
            var scroll = selectable ? selectable.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (!scroll || !scroll.viewport || !scroll.content || !selectable.transform.IsChildOf(scroll.content))
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selectable.transform);
            var view = scroll.viewport.rect;
            Vector2 position = scroll.content.anchoredPosition;
            if (scroll.vertical)
            {
                if (bounds.max.y > view.yMax)
                {
                    position.y -= bounds.max.y - view.yMax;
                }
                else if (bounds.min.y < view.yMin)
                {
                    position.y += view.yMin - bounds.min.y;
                }
            }

            if (scroll.horizontal)
            {
                if (bounds.min.x < view.xMin)
                {
                    position.x += view.xMin - bounds.min.x;
                }
                else if (bounds.max.x > view.xMax)
                {
                    position.x -= bounds.max.x - view.xMax;
                }
            }

            scroll.content.anchoredPosition = position;
        }

        private void OnDisable()
        {
            if (account)
            {
                account.ExternalKeyboardNavigation = false;
            }

            rememberedFocus.Clear();
            previousScreen = null;
            previousExpanded = null;
        }
    }
}
