using System;
using System.Collections.Generic;

namespace TravelPlanning.Flights
{
    /// <summary>One flight and its one-way USD fare. Local times belong to the corresponding airport.</summary>
    public sealed class FlightOption
    {
        public string Id { get; internal set; }
        public string AirlineId { get; internal set; }
        public string AirlineName { get; internal set; }
        public string FlightNumber { get; internal set; }
        public string OriginAirportId { get; internal set; }
        public string DestinationAirportId { get; internal set; }
        public string OriginCity { get; internal set; }
        public string DestinationCity { get; internal set; }
        public string OriginTimeZoneId { get; internal set; }
        public string DestinationTimeZoneId { get; internal set; }
        public DateTime DepartureUtc { get; internal set; }
        public DateTime ArrivalUtc { get; internal set; }
        public DateTime DepartureLocal { get; internal set; }
        public DateTime ArrivalLocal { get; internal set; }
        public int PriceCents { get; internal set; }
        public int AvailableSeats { get; internal set; }
        public string Currency { get; internal set; }
    }
}
