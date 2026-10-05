using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using SQLite;

namespace TravelPlanning.Trips
{
    /// <summary>Fixed catalog queries used to resolve saved references without one query per item.</summary>
    internal static class TripCatalog
    {
#region Target
        internal static void Target(SavedItemKind kind, out string table, out string column, out string unit)
        {
            switch (kind)
            {
                case SavedItemKind.Flight:
                    table = "flights";
                    column = "flight_id";
                    unit = "/ flight";
                    break;
                case SavedItemKind.Hotel:
                    table = "hotels";
                    column = "hotel_id";
                    unit = "/ room / night";
                    break;
                case SavedItemKind.Restaurant:
                    table = "restaurants";
                    column = "restaurant_id";
                    unit = "/ person (meal)";
                    break;
                case SavedItemKind.Experience:
                    table = "experiences";
                    column = "experience_id";
                    unit = "/ adult";
                    break;
                case SavedItemKind.Hotspot:
                    table = "hotspots";
                    column = "hotspot_id";
                    unit = "/ visit";
                    break;
                default:
                    throw new ArgumentException("Choose a valid item category.", nameof(kind));
            }
        }
#endregion

#region Read Items
        internal static IReadOnlyList<SavedTripItem> ReadItems(
            SQLiteConnection connection,
            string owner,
            string tripId,
            CancellationToken token)
        {
            var rows = new List<SavedCatalogRow>();
            foreach (SavedItemKind kind in Enum.GetValues(typeof(SavedItemKind)))
            {
                token.ThrowIfCancellationRequested();
                Target(kind, out string table, out string column, out string unit);
                string sql = "SELECT s.id AS Id, s.created_utc AS SavedUtc, p.id AS TargetId, p.price_cents AS PriceCents, " +
                "p.currency AS Currency, ";
                if (kind == SavedItemKind.Flight)
                    sql += "a.name AS AirlineName, p.flight_number AS FlightNumber, p.origin_airport_id AS OriginAirportId, " +
                    "p.destination_airport_id AS DestinationAirportId, " +
                        "p.departure_utc AS DepartureUtc, p.arrival_utc AS ArrivalUtc, oa.time_zone AS OriginZone, " +
                        "da.time_zone AS DestinationZone " +
                        "FROM saved_items s JOIN flights p ON p.id=s.flight_id JOIN airlines a ON a.id=p.airline_id " +
                        "JOIN airports oa ON oa.id=p.origin_airport_id JOIN airports da ON da.id=p.destination_airport_id ";
                else
                    sql += "p.name AS Title, p.address AS Address, p.description AS Description, d.name AS DestinationName " +
                        "FROM saved_items s JOIN " +
                        table +
                        " p ON p.id=s." +
                        column +
                        " JOIN destinations d ON d.id=p.destination_id ";
                sql += "WHERE s.user_id=? AND s.trip_id=?";
                var categoryRows = connection.Query<SavedCatalogRow>(sql, owner, tripId);
                foreach (var row in categoryRows)
                {
                    row.Kind = kind;
                    row.PriceUnit = unit;
                }

                rows.AddRange(categoryRows);
            }

            var items = new List<SavedTripItem>();
            foreach (var row in rows.OrderBy(row => row.SavedUtc, StringComparer.Ordinal).ThenBy(row => row.Id, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                bool flight = row.Kind == SavedItemKind.Flight;
                items.Add(
                    new SavedTripItem {
                    Id = row.Id,
                    TargetId = row.TargetId,
                    Kind = row.Kind,
                    PriceCents = row.PriceCents,
                    Currency = row.Currency,
                    PriceUnit = row.PriceUnit,
                    Title = flight
                    ? row.AirlineName +
                    " " +
                    row.FlightNumber
                    : row.Title,
                    Details = flight
                    ? row.OriginAirportId +
                    " to " +
                    row.DestinationAirportId +
                    "\nDepart " +
                    Local(row.DepartureUtc, row.OriginZone) +
                    " (" +
                    row.OriginAirportId +
                    " local)" +
                    "\nArrive " +
                    Local(row.ArrivalUtc, row.DestinationZone) +
                    " (" +
                    row.DestinationAirportId +
                    " local)"
                    : row.DestinationName +
                    "\n" +
                    row.Address +
                    "\n" +
                    row.Description
                });
            }

            return items.AsReadOnly();
        }
#endregion

#region Local
        private static string Local(string utc, string zone)
        {
            var parsed = DateTime.ParseExact(
                utc,
                "yyyy-MM-ddTHH:mm:ssZ",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return TimeZoneInfo.ConvertTimeFromUtc(parsed, TimeZoneInfo.FindSystemTimeZoneById(zone))
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
#endregion
    }
}
