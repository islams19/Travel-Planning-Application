using System;
using System.Collections.Generic;

namespace TravelPlanning.Flights
{
    public sealed class AirportOption
    {
        public string Id { get; set; }
        public string DestinationId { get; set; }
        public string City { get; set; }
        public string Name { get; set; }
        public string TimeZoneId { get; set; }
    }
}
