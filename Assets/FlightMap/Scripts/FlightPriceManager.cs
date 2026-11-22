using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TravelApp.FlightMap
{
    /// <summary>
    /// Requests offers from the configured provider, positions bubbles with real coordinates,
    /// and sends a clicked destination back through FlightLocationSelector.
    /// </summary>
    public sealed class FlightPriceManager : MonoBehaviour
    {
        [SerializeField] FlightLocationSelector selector;
        [SerializeField] FlightMapViewport map;
        [SerializeField] RectTransform markerLayer;
        [SerializeField] FlightPriceMarker markerTemplate;
        [SerializeField] TMP_Text selectedPriceText;
        [SerializeField] MonoBehaviour providerBehaviour;
        readonly List<FlightPriceMarker> markers = new();
        IReadOnlyList<FlightPriceOffer> offers = new List<FlightPriceOffer>();
        IFlightPriceDataProvider provider;
        string loadedDepartureCode;

        public IReadOnlyList<FlightPriceMarker> Markers => markers;
        public int OfferCount => offers.Count;
        public string LoadedDepartureCode => loadedDepartureCode;

        public void Configure(FlightLocationSelector locationSelector, FlightMapViewport viewport,
            RectTransform layer, FlightPriceMarker template, TMP_Text selectedPrice, MonoBehaviour dataProvider)
        { selector=locationSelector; map=viewport; markerLayer=layer; markerTemplate=template; selectedPriceText=selectedPrice; providerBehaviour=dataProvider; }

        void Start()
        {
            provider = providerBehaviour as IFlightPriceDataProvider;
            if (selector == null || map == null || markerLayer == null || markerTemplate == null || selectedPriceText == null || provider == null)
            { Debug.LogError("FlightPriceManager is missing required scene references.", this); enabled = false; return; }
            markerTemplate.gameObject.SetActive(false);
            selector.SelectionChanged += OnSelectionChanged;
            Refresh(selector.Departure);
            UpdateSelectedPrice(selector.Destination);
        }

        void OnDestroy()
        { if (selector != null) selector.SelectionChanged -= OnSelectionChanged; }

        // Deterministic screen-space packing. Selection wins, then stable airport order. Hidden
        // bubbles retain their offer and return when panning/zooming provides enough room.
        void LateUpdate()
        {
            if (map == null || markerLayer == null) return;
            float inverseZoom = 1f / Mathf.Max(.01f,map.Zoom);
            var accepted = new List<Rect>();
            var viewport = map.ViewportRect;
            var bounds = viewport.rect;
            int visible = 0, limit = map.Zoom < .45f ? 12 : map.Zoom < .9f ? 20 : 32;
            var ordered = new List<FlightPriceMarker>(markers);
            string selected = selector.Destination?.code;
            ordered.Sort((a,b) =>
            {
                int priorityA = a.Offer.Destination.code == selected ? 0 : 1;
                int priorityB = b.Offer.Destination.code == selected ? 0 : 1;
                return priorityA != priorityB ? priorityA.CompareTo(priorityB) : markers.IndexOf(a).CompareTo(markers.IndexOf(b));
            });
            foreach (var marker in ordered)
            {
                var rect = marker.GetComponent<RectTransform>();
                rect.localScale = new Vector3(inverseZoom,inverseZoom,1);
                bool isSelected = marker.Offer.Destination.code == selected;
                marker.SetSelected(isSelected);
                Vector2 geographic = map.CoordinateToLocal(marker.Offer.Destination.latitude,marker.Offer.Destination.longitude);
                Vector2 point = viewport.InverseTransformPoint(map.ContentRect.TransformPoint(geographic));
                bool show = false;
                if (bounds.Contains(point) && visible < limit)
                {
                    foreach (Vector2 offset in BubbleOffsets)
                    {
                        var box = new Rect(point.x+offset.x-47,point.y+offset.y,94,64);
                        if (box.xMin < bounds.xMin+4 || box.xMax > bounds.xMax-4 || box.yMin < bounds.yMin+4 || box.yMax > bounds.yMax-4) continue;
                        var padded = new Rect(box.xMin-6,box.yMin-5,box.width+12,box.height+10);
                        if (accepted.Exists(other=>other.Overlaps(padded))) continue;
                        // Leave the fixed fare banner and zoom controls unobstructed.
                        if (box.Overlaps(new Rect(bounds.xMin+8,bounds.yMax-96,480,88)) ||
                            box.Overlaps(new Rect(bounds.xMax-84,bounds.yMax-152,80,148))) continue;
                        rect.anchoredPosition = geographic + offset * inverseZoom;
                        accepted.Add(padded); visible++; show=true; break;
                    }
                }
                marker.gameObject.SetActive(show);
            }
            markerLayer.SetAsLastSibling();
        }
        static readonly Vector2[] BubbleOffsets =
        {
            new Vector2(0,18),new Vector2(100,18),new Vector2(-100,18),
            new Vector2(0,94),new Vector2(100,94),new Vector2(-100,94)
        };

        void OnSelectionChanged(FlightLocation departure, FlightLocation destination)
        {
            if (departure?.code != loadedDepartureCode) Refresh(departure);
            UpdateSelectedPrice(destination);
        }

        public void Refresh(FlightLocation departure)
        {
            // Disable first because Destroy is deferred until the end of the frame. Without this,
            // rapidly changing departure airports can leave stale buttons in the UI raycast stack
            // for one frame and make a newly displayed price bubble appear unresponsive.
            foreach (FlightPriceMarker marker in markers)
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                    Destroy(marker.gameObject);
                }
            markers.Clear();
            loadedDepartureCode = departure?.code;
            offers = provider?.GetOffers(departure) ?? new List<FlightPriceOffer>();
            foreach (FlightPriceOffer offer in offers)
            {
                FlightPriceMarker marker = Instantiate(markerTemplate, markerLayer);
                marker.gameObject.SetActive(true);
                marker.GetComponent<RectTransform>().anchoredPosition = map.CoordinateToLocal(offer.Destination.latitude, offer.Destination.longitude);
                marker.Bind(offer, SelectOffer);
                markers.Add(marker);
            }
        }

        void SelectOffer(FlightPriceOffer offer)
        {
            if (offer == null) return;
            selector.SelectDestinationByCode(offer.Destination.code);
            UpdateSelectedPrice(offer.Destination);
        }

        void UpdateSelectedPrice(FlightLocation destination)
        {
            FlightPriceOffer selected = null;
            foreach (FlightPriceOffer offer in offers)
                if (offer.Destination == destination || offer.Destination.code == destination?.code) { selected = offer; break; }
            selectedPriceText.text = selected == null
                ? "Choose different valid airports to see fares"
                : $"{selected.PriceLabel} {selected.Currency} · {selected.Destination.code} — {selected.Destination.city}";
        }
    }
}
