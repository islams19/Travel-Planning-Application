using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TravelApp.FlightMap
{
    // Airport IDs are IANA; OS TimeZoneInfo supplies date-aware rules. CLDR names cover Unity's
    // Windows/Mono runtime, which may only expose Windows time-zone identifiers.
    public static class AirportClock
    {
        static readonly Dictionary<string,TimeZoneInfo> cache = new Dictionary<string,TimeZoneInfo>();
        static readonly Dictionary<string,string> windowsIds = new Dictionary<string,string>
        {

        };

        public static TimeZoneInfo Zone(string airportCode)
        {
            var airport = TravelLocationCatalog.Find(airportCode) ?? throw new ArgumentException("Unknown airport: " + airportCode);
            string id = airport.TimeZoneId;
            if (cache.TryGetValue(id,out var zone)) return zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById(windowsIds[id]); }
            cache[id] = zone;
            return zone;
        }

        public static DateTime Local(DateTime utc,string airportCode) => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc,DateTimeKind.Utc),Zone(airportCode));

        public static DateTime ToUtc(DateTime local,string airportCode)
        {
            var zone=Zone(airportCode);
            local=DateTime.SpecifyKind(local,DateTimeKind.Unspecified);
            if(zone.IsInvalidTime(local)) throw new ArgumentException("This local departure time does not exist on the selected date.");
            // Pick the earlier occurrence of an ambiguous autumn clock time, deterministically.
            if(zone.IsAmbiguousTime(local)) return new DateTimeOffset(local,zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime;
            return TimeZoneInfo.ConvertTimeToUtc(local,zone);
        }

        public static string Format(DateTime utc,string airportCode,DateTime travelDate)
        {
            var local=Local(utc,airportCode);
            int days=(local.Date-travelDate.Date).Days;
            return local.ToString("h:mm tt",CultureInfo.InvariantCulture) + (days==0 ? "" :
                " ("+(days>0 ? "+" : "")+days+" day"+(Math.Abs(days)==1 ? "" : "s")+")");
        }
    }
}
