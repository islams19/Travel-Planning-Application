using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TravelApp.FlightMap
{
    /// <summary>Orchestrates selectors, geographic overlays, animation, labels, and information panel.</summary>
    public sealed class FlightMapManager : MonoBehaviour
    {
        [SerializeField] FlightLocationSelector selector;
        [SerializeField] FlightMapViewport map;
        [SerializeField] FlightRouteRenderer route;
        [SerializeField] AirplaneAnimationController airplane;
        [SerializeField] FlightInformationManager information;
        [SerializeField] RectTransform departureMarker, destinationMarker;
        [SerializeField] TMP_Text departureLabel, destinationLabel;

        [SerializeField] bool automaticallyAnimateRoutes;
        string activeRouteKey;
        readonly List<RectTransform> cityLabels = new();

        public void Configure(FlightLocationSelector s, FlightMapViewport m, FlightRouteRenderer r, AirplaneAnimationController a,
            FlightInformationManager i, RectTransform fromMarker, RectTransform toMarker, TMP_Text fromLabel, TMP_Text toLabel)
        { selector=s; map=m; route=r; airplane=a; information=i; departureMarker=fromMarker; destinationMarker=toMarker; departureLabel=fromLabel; destinationLabel=toLabel; }

        void Awake()
        {
            // Repair layout defects from the first serialized version without requiring users to
            // delete or recreate their scene. The editor builder now writes these values correctly.
            // The selector and viewport share the map Content root, including in DesktopLayout.
            // Never repair text elsewhere on the application's shared Canvas.
            Transform layoutRoot = selector != null ? selector.transform.parent : transform;
            foreach (TMP_Text text in layoutRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                RectTransform rect = text.rectTransform;
                Button button = text.GetComponentInParent<Button>();
                // Price bubbles contain separate fare/code labels; retain their individual layout.
                if (button != null && button.GetComponentsInChildren<TMP_Text>(true).Length == 1)
                {
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                }
                else if (Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y) && rect.sizeDelta.y < 0f)
                {
                    Vector2 min = rect.offsetMin, max = rect.offsetMax;
                    (min.y, max.y) = (max.y, min.y);
                    rect.offsetMin = min; rect.offsetMax = max;
                }
            }
        }

        void Start()
        {
            if (selector == null || map == null || route == null || airplane == null || information == null)
            { Debug.LogError("FlightMapManager is missing required scene references.", this); enabled = false; return; }
            departureMarker.sizeDelta = destinationMarker.sizeDelta = new Vector2(18,24);
            airplane.Visual.sizeDelta = new Vector2(30,30);
            foreach (var label in new[] { departureLabel, destinationLabel })
            {
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f,.5f);
                label.rectTransform.pivot = new Vector2(0,.5f);
                label.rectTransform.sizeDelta = new Vector2(90,24);
                label.rectTransform.anchoredPosition = new Vector2(14,2);
                label.fontSize = 14;
            }
            foreach (FlightLocation location in FlightLocationDatabase.Locations)
            {
                var labelRoot = map.ContentRect.Find("CityLabel_" + location.code) as RectTransform;
                if (labelRoot != null)
                {
                    labelRoot.anchoredPosition = map.CoordinateToLocal(location.latitude, location.longitude);
                    cityLabels.Add(labelRoot);
                }
            }
            selector.SelectionChanged += UpdateRoute;
            airplane.StateChanged += information.SetStatus;
            UpdateRoute(selector.Departure, selector.Destination);
        }

        void LateUpdate()
        {
            if (map == null) return;
            Vector3 scale = Vector3.one / Mathf.Max(.01f,map.Zoom); scale.z = 1;
            departureMarker.localScale = destinationMarker.localScale = airplane.Visual.localScale = scale;
            foreach (var label in cityLabels)
            {
                label.localScale = scale;
                label.gameObject.SetActive(map.Zoom >= .65f);
            }
        }

        void OnDestroy()
        {
            if (selector != null) selector.SelectionChanged -= UpdateRoute;
            if (airplane != null && information != null) airplane.StateChanged -= information.SetStatus;
        }

        void UpdateRoute(FlightLocation from, FlightLocation to)
        {
            bool valid = from != null && to != null && from.code != to.code;
            foreach (Button button in selector.GetComponentsInChildren<Button>(true))
                if (button.name == "StartFlightButton" || button.name == "PauseResumeButton" || button.name == "RestartFlightButton")
                    button.interactable = valid;
            string routeKey = valid ? from.code + ">" + to.code : null;
            // Search notifications for the same endpoints must not restart the preview or reset map pan.
            if (automaticallyAnimateRoutes && valid && activeRouteKey == routeKey) return;
            activeRouteKey = routeKey;
            departureMarker.gameObject.SetActive(valid);
            destinationMarker.gameObject.SetActive(valid);
            if (!valid)
            {
                route.ClearRoute();
                airplane.ClearRoute();
                departureLabel.text = destinationLabel.text = "";
                information.ShowInvalidRoute(from != null && to != null
                    ? "Choose different departure and destination airports."
                    : "Select a valid departure and destination airport.");
                return;
            }
            departureMarker.anchoredPosition = map.CoordinateToLocal(from.latitude, from.longitude);
            destinationMarker.anchoredPosition = map.CoordinateToLocal(to.latitude, to.longitude);
            departureLabel.text = from.code; destinationLabel.text = to.code;
            route.SetRoute(from, to, map);
            map.FocusOnPoints(route.Points);
            airplane.SetRoute(route.Points,map.ContentRect.rect.width);
            information.SetRoute(from, to, airplane.DemonstrationDurationSeconds);
            information.SetStatus(airplane.State, 0f);
            if (automaticallyAnimateRoutes) airplane.StartFlight();
        }
    }
}
