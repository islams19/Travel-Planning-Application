using System;
using System.Globalization;
using TMPro;
using TravelPlanning.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Flights
{
    /// <summary>Formats one seeded offer; every displayed time belongs to that airport's local time zone.</summary>
    public sealed class FlightResultRow : MonoBehaviour
    {
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text airlineText, routeText, priceText;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button saveButton;
        [SerializeField]
        private UnityEngine.UI.Button trackButton;
        private FlightOption shownFlight;
        private Action<FlightOption> saveRequested;
        private Action<FlightOption> trackRequested;
        private void Awake()
        {
            if (saveButton)
            {
                saveButton.onClick.AddListener(Save);
            }

            if (trackButton)
            {
                trackButton.onClick.AddListener(Track);
            }
        }

        public void Show(FlightOption flight, Action<FlightOption> onSave = null, Action<FlightOption> onTrack = null)
        {
            shownFlight = flight;
            saveRequested = onSave;
            if (saveButton)
            {
                saveButton.interactable = onSave != null;
            }

            trackRequested = onTrack;
            if (trackButton)
            {
                trackButton.interactable = onTrack != null;
            }

            airlineText.text = flight.AirlineName + " • " + flight.FlightNumber;
            routeText.text = flight.OriginAirportId + " " + flight.DepartureLocal.ToString("dd MMM HH:mm",
                CultureInfo.InvariantCulture) + " → " + flight.DestinationAirportId + " " + flight.ArrivalLocal.ToString("dd MMM HH:mm",
                CultureInfo.InvariantCulture) + "\nTimes local to each airport • " + flight.AvailableSeats + " sample seats";
            priceText.text = "USD " + (flight.PriceCents / 100m).ToString("N2", CultureInfo.InvariantCulture);
        }

        private void Save() => saveRequested?.Invoke(shownFlight);
        private void Track() => trackRequested?.Invoke(shownFlight);
        private void OnDestroy()
        {
            if (saveButton)
            {
                saveButton.onClick.RemoveListener(Save);
            }

            if (trackButton)
            {
                trackButton.onClick.RemoveListener(Track);
            }
        }
    }
}
