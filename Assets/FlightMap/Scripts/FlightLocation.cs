using System;
using UnityEngine;

namespace TravelApp.FlightMap
{
    /// <summary>A searchable airport/city with real WGS84 coordinates.</summary>
    [Serializable]
    public sealed class FlightLocation
    {
        public string code;
        public string airport;
        public string city;
        public string country;
        public double latitude;
        public double longitude;
        public string BookingDestination;
        public string TimeZoneId;

        public string DisplayName => $"{code} — {city}";
        public string FullName => $"{airport} ({code}), {city}, {country}";

        public FlightLocation(string code, string airport, string city, string country, double latitude, double longitude, string bookingDestination = null, string timeZoneId = "Etc/UTC")
        {
            this.code = code; this.airport = airport; this.city = city; this.country = country;
            this.latitude = latitude; this.longitude = longitude; BookingDestination = bookingDestination; TimeZoneId = timeZoneId;
        }
    }
}
