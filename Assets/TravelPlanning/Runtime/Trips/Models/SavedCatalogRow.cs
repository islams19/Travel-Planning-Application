using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using SQLite;

namespace TravelPlanning.Trips
{
    // Simple public properties allow sqlite-net to map the SQL column aliases.
    public sealed class SavedCatalogRow
    {
        public string Id { get; set; }
        public string SavedUtc { get; set; }
        public string TargetId { get; set; }
        public int? PriceCents { get; set; }
        public string Currency { get; set; }
        public string Title { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public string DestinationName { get; set; }
        public string AirlineName { get; set; }
        public string FlightNumber { get; set; }
        public string OriginAirportId { get; set; }
        public string DestinationAirportId { get; set; }
        public string DepartureUtc { get; set; }
        public string ArrivalUtc { get; set; }
        public string OriginZone { get; set; }
        public string DestinationZone { get; set; }
        public SavedItemKind Kind { get; set; }
        public string PriceUnit { get; set; }
    }
}
