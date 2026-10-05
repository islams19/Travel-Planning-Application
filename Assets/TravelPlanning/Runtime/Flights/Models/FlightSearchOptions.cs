using System;
using System.Collections.Generic;

namespace TravelPlanning.Flights
{
    public sealed class FlightSearchOptions
    {
        public IReadOnlyList<AirportOption> Airports { get; internal set; }
        public IReadOnlyList<AirlineOption> Airlines { get; internal set; }
        public string StartDate { get; internal set; }
        public string EndDate { get; internal set; }
    }
}
