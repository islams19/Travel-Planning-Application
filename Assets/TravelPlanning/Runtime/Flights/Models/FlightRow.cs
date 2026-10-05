using System;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace TravelPlanning.Flights
{
    // Public setters/constructors let sqlite-net map SQL aliases onto simple rows.
    public sealed class FlightRow
    {
        public string Id { get; set; }
        public string AirlineId { get; set; }
        public string AirlineName { get; set; }
        public string FlightNumber { get; set; }
        public string OriginAirportId { get; set; }
        public string DestinationAirportId { get; set; }
        public string OriginCity { get; set; }
        public string DestinationCity { get; set; }
        public string OriginTimeZoneId { get; set; }
        public string DestinationTimeZoneId { get; set; }
        public string DepartureUtcText { get; set; }
        public string ArrivalUtcText { get; set; }
        public int PriceCents { get; set; }
        public int AvailableSeats { get; set; }
        public string Currency { get; set; }
    }
}
