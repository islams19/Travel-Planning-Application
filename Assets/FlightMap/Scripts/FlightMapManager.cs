using TMPro;
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

        public void Configure(FlightLocationSelector s, FlightMapViewport m, FlightRouteRenderer r, AirplaneAnimationController a,
            FlightInformationManager i, RectTransform fromMarker, RectTransform toMarker, TMP_Text fromLabel, TMP_Text toLabel)
        { selector=s; map=m; route=r; airplane=a; information=i; departureMarker=fromMarker; destinationMarker=toMarker; departureLabel=fromLabel; destinationLabel=toLabel; }

        void Awake()
        {
            // Repair layout defects from the first serialized version without requiring users to
            // delete or recreate their scene. The editor builder now writes these values correctly.
            foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                RectTransform rect = text.rectTransform;
                if (text.GetComponentInParent<Button>() != null)
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
            selector.SelectionChanged += UpdateRoute;
            airplane.StateChanged += information.SetStatus;
            UpdateRoute(selector.Departure, selector.Destination);
        }

        void OnDestroy()
        {
            if (selector != null) selector.SelectionChanged -= UpdateRoute;
            if (airplane != null && information != null) airplane.StateChanged -= information.SetStatus;
        }

        void UpdateRoute(FlightLocation from, FlightLocation to)
        {
            if (from == null || to == null) return;
            departureMarker.anchoredPosition = map.CoordinateToLocal(from.latitude, from.longitude);
            destinationMarker.anchoredPosition = map.CoordinateToLocal(to.latitude, to.longitude);
            departureLabel.text = from.code; destinationLabel.text = to.code;
            route.SetRoute(from, to, map);
            map.FocusOnPoints(route.Points);
            airplane.SetRoute(route.Points);
            information.SetRoute(from, to, airplane.DemonstrationDurationSeconds);
            information.SetStatus(airplane.State, 0f);
        }
    }
}
