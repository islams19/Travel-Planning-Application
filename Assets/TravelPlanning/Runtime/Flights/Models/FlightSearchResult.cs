using System;
using System.Collections.Generic;

namespace TravelPlanning.Flights
{
    public sealed class FlightSearchResult
    {
        public IReadOnlyList<FlightOption> Outbound { get; internal set; }
        public IReadOnlyList<FlightOption> Return { get; internal set; }
        // Diagnostics for tests and logs; these are not labels intended for the app screen.
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
