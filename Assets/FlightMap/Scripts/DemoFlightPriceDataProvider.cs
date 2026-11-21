using System.Collections.Generic;
using UnityEngine;

namespace TravelApp.FlightMap
{
    /// <summary>
    /// Offline, deterministic example fares. Values vary by origin and distance, but are
    /// intentionally synthetic and must always be displayed with the Demo/not-live label.
    /// The filename matches the MonoBehaviour class so Unity can serialize its scene reference.
    /// </summary>
    public sealed class DemoFlightPriceDataProvider : MonoBehaviour, IFlightPriceDataProvider
    {
        static readonly string[] DisplayDestinations =
        { "LAX", "MIA", "ORD", "ATL", "SFO", "SEA", "DFW", "MEX", "LHR", "YYZ" };

        public IReadOnlyList<FlightPriceOffer> GetOffers(FlightLocation departure)
        {
            var offers = new List<FlightPriceOffer>();
            if (departure == null) return offers;
            foreach (string code in DisplayDestinations)
            {
                FlightLocation destination = Find(code);
                if (destination == null || destination.code == departure.code) continue;
                offers.Add(CreateOffer(departure, destination));
            }
            return offers;
        }

        public static FlightPriceOffer CreateOffer(FlightLocation departure, FlightLocation destination)
        {
            double distance = FlightGeoMath.GreatCircleDistanceKm(departure, destination);
            int stableVariation = StableCodeValue(departure.code + destination.code) % 55;
            int raw = Mathf.RoundToInt(64f + (float)distance * 0.045f + stableVariation);
            int rounded = Mathf.Max(79, Mathf.RoundToInt(raw / 5f) * 5);
            return new FlightPriceOffer(destination, rounded, "USD", true);
        }

        static FlightLocation Find(string code)
        {
            foreach (FlightLocation location in FlightLocationDatabase.Locations)
                if (location.code == code) return location;
            return null;
        }

        static int StableCodeValue(string value)
        {
            int result = 17;
            foreach (char character in value) result = result * 31 + character;
            return Mathf.Abs(result);
        }
    }
}
