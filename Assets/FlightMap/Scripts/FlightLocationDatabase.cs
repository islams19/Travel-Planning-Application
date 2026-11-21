using System;
using System.Collections.Generic;

namespace TravelApp.FlightMap
{
    /// <summary>Small offline demonstration catalog. Extend this list or replace it with an approved data source.</summary>
    public static class FlightLocationDatabase
    {
        public static readonly IReadOnlyList<FlightLocation> Locations = new[]
        {
            new FlightLocation("JFK", "John F. Kennedy International Airport", "New York", "United States", 40.6413, -73.7781),
            new FlightLocation("LAX", "Los Angeles International Airport", "Los Angeles", "United States", 33.9416, -118.4085),
            new FlightLocation("MIA", "Miami International Airport", "Miami", "United States", 25.7959, -80.2870),
            new FlightLocation("ORD", "O'Hare International Airport", "Chicago", "United States", 41.9742, -87.9073),
            new FlightLocation("ATL", "Hartsfield-Jackson Atlanta International Airport", "Atlanta", "United States", 33.6407, -84.4277),
            new FlightLocation("SFO", "San Francisco International Airport", "San Francisco", "United States", 37.6213, -122.3790),
            new FlightLocation("SEA", "Seattle-Tacoma International Airport", "Seattle", "United States", 47.4502, -122.3088),
            new FlightLocation("DFW", "Dallas Fort Worth International Airport", "Dallas", "United States", 32.8998, -97.0403),
            new FlightLocation("BOS", "Boston Logan International Airport", "Boston", "United States", 42.3656, -71.0096),
            new FlightLocation("IAD", "Washington Dulles International Airport", "Washington", "United States", 38.9531, -77.4565),
            new FlightLocation("YYZ", "Toronto Pearson International Airport", "Toronto", "Canada", 43.6777, -79.6248),
            new FlightLocation("MEX", "Mexico City International Airport", "Mexico City", "Mexico", 19.4361, -99.0719),
            new FlightLocation("LHR", "Heathrow Airport", "London", "United Kingdom", 51.4700, -0.4543),
            new FlightLocation("CDG", "Charles de Gaulle Airport", "Paris", "France", 49.0097, 2.5479),
            new FlightLocation("FRA", "Frankfurt Airport", "Frankfurt", "Germany", 50.0379, 8.5622),
            new FlightLocation("DXB", "Dubai International Airport", "Dubai", "United Arab Emirates", 25.2532, 55.3657),
            new FlightLocation("DEL", "Indira Gandhi International Airport", "Delhi", "India", 28.5562, 77.1000),
            new FlightLocation("HND", "Haneda Airport", "Tokyo", "Japan", 35.5494, 139.7798),
            new FlightLocation("SIN", "Singapore Changi Airport", "Singapore", "Singapore", 1.3644, 103.9915),
            new FlightLocation("SYD", "Sydney Airport", "Sydney", "Australia", -33.9399, 151.1753),
            new FlightLocation("GRU", "São Paulo/Guarulhos International Airport", "São Paulo", "Brazil", -23.4356, -46.4731),
            new FlightLocation("JNB", "O. R. Tambo International Airport", "Johannesburg", "South Africa", -26.1337, 28.2420)
        };

        public static bool Matches(FlightLocation location, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return location.FullName.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
