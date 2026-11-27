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
        public IReadOnlyList<FlightPriceOffer> GetOffers(FlightLocation departure)
        {
            var offers = new List<FlightPriceOffer>();
            if (departure == null) return offers;
            foreach (FlightLocation destination in FlightLocationDatabase.Locations)
            {
                if (destination == null || destination.code == departure.code) continue;
                offers.Add(CreateOffer(departure, destination));
            }
            return offers;
        }

        public static FlightPriceOffer CreateOffer(FlightLocation departure, FlightLocation destination)
        {
            return new FlightPriceOffer(destination,MockTravelPricing.PlanningEstimate(departure,destination),"USD",true);
        }
    }
}
