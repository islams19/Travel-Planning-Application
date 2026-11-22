using System;
using System.Collections.Generic;
namespace TravelApp.FlightMap
{
    // Compatibility adapter for the existing map architecture; no independent airport list.
    public static class FlightLocationDatabase
    {
        public static IReadOnlyList<FlightLocation> Locations => TravelLocationCatalog.Airports;
        public static bool Matches(FlightLocation location, string query) => string.IsNullOrWhiteSpace(query) ||
            location.FullName.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
