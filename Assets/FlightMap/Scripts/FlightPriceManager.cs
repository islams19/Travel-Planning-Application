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

        void LateUpdate()
        {
            // The geographic anchor must follow the panned/zoomed map, but Expedia-style price
            // bubbles should remain a stable readable screen size rather than growing with zoom.
            float inverseZoom = map != null ? 1f / Mathf.Max(0.01f, map.Zoom) : 1f;
            Vector3 markerScale = new Vector3(inverseZoom, inverseZoom, 1f);
            foreach (FlightPriceMarker marker in markers)
                if (marker != null) marker.transform.localScale = markerScale;
        }

        void OnSelectionChanged(FlightLocation departure, FlightLocation destination)
        {
            if (departure != null && departure.code != loadedDepartureCode) Refresh(departure);
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
                ? "Demo fare unavailable for this destination"
                : $"Selected demo fare: {selected.PriceLabel} {selected.Currency} · not live";
        }
    }
}
