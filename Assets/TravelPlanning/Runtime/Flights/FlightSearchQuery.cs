using System;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace TravelPlanning.Flights
{
    /// <summary>Builds only fixed SQL clauses; user values are always parameters.</summary>
    internal static class FlightSearchQuery
    {
        private const string RouteSql = "SELECT f.id AS Id, f.airline_id AS AirlineId, a.name AS AirlineName, f.flight_number AS FlightNumber, " +
            "f.origin_airport_id AS OriginAirportId, f.destination_airport_id AS DestinationAirportId, " +
            "od.name AS OriginCity, dd.name AS DestinationCity, oa.time_zone AS OriginTimeZoneId, " +
            "da.time_zone AS DestinationTimeZoneId, " +
            "f.departure_utc AS DepartureUtcText, f.arrival_utc AS ArrivalUtcText, f.price_cents AS PriceCents, " +
            "f.available_seats AS AvailableSeats, f.currency AS Currency " +
            "FROM flights f JOIN airlines a ON a.id=f.airline_id " +
            "JOIN airports oa ON oa.id=f.origin_airport_id JOIN airports da ON da.id=f.destination_airport_id " +
            "JOIN destinations od ON od.id=oa.destination_id JOIN destinations dd ON dd.id=da.destination_id " +
            "WHERE f.origin_airport_id=? AND f.destination_airport_id=? AND f.departure_local_date=?";
#region Build
        internal static string Build(FlightSearchRequest request, bool returning, out object[] arguments)
        {
            var values = new List<object>
            {
                returning ? request.DestinationAirportId : request.OriginAirportId,
                returning ? request.OriginAirportId : request.DestinationAirportId,
                returning ? request.ReturnDate : request.DepartureDate
            };
            string sql = RouteSql;
            if (request.MaxPriceCents.HasValue)
            {
                sql += " AND f.price_cents<=?";
                values.Add(request.MaxPriceCents.Value);
            }

            if (request.AirlineId != null)
            {
                sql += " AND f.airline_id=?";
                values.Add(request.AirlineId);
            }

            arguments = values.ToArray();
            return sql;
        }
#endregion

#region Convert Row
        internal static FlightOption ConvertRow(FlightRow row, Dictionary<string, TimeZoneInfo> zones)
        {
            DateTime departure = ParseUtc(row.DepartureUtcText);
            DateTime arrival = ParseUtc(row.ArrivalUtcText);
            return new FlightOption
            {
                Id = row.Id,
                AirlineId = row.AirlineId,
                AirlineName = row.AirlineName,
                FlightNumber = row.FlightNumber,
                OriginAirportId = row.OriginAirportId,
                DestinationAirportId = row.DestinationAirportId,
                OriginCity = row.OriginCity,
                DestinationCity = row.DestinationCity,
                OriginTimeZoneId = row.OriginTimeZoneId,
                DestinationTimeZoneId = row.DestinationTimeZoneId,
                DepartureUtc = departure,
                ArrivalUtc = arrival,
                DepartureLocal = TimeZoneInfo.ConvertTimeFromUtc(departure, Zone(row.OriginTimeZoneId, zones)),
                ArrivalLocal = TimeZoneInfo.ConvertTimeFromUtc(arrival, Zone(row.DestinationTimeZoneId, zones)),
                PriceCents = row.PriceCents,
                AvailableSeats = row.AvailableSeats,
                Currency = row.Currency
            };
        }
#endregion

#region Zone
        private static TimeZoneInfo Zone(string id, Dictionary<string, TimeZoneInfo> zones)
        {
            if (!zones.TryGetValue(id, out var zone))
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                zones.Add(id, zone);
            }

            return zone;
        }
#endregion

#region Parse Utc
        private static DateTime ParseUtc(string value) => DateTime.ParseExact(
            value,
            "yyyy-MM-ddTHH:mm:ssZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
#endregion
    }
}
