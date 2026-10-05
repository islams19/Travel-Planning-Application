using System;
using System.Collections.Generic;

namespace TravelPlanning.Flights
{
    /// <summary>The form values. SearchAsync takes a copy so editing the form cannot change a running search.</summary>
    public sealed class FlightSearchRequest
    {
        public string OriginAirportId { get; set; }
        public string DestinationAirportId { get; set; }
        public string DepartureDate { get; set; }
        public string ReturnDate { get; set; }
        public int? MaxPriceCents { get; set; }
        public string AirlineId { get; set; }
        public FlightSort Sort { get; set; }
        public DepartureTimeBand TimeBand { get; set; }

        internal FlightSearchRequest Snapshot() => (FlightSearchRequest)MemberwiseClone();
    }
}
