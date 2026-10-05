using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Flights
{
    /// <summary>Reads the offline catalog on a worker thread. Search never changes the database.</summary>
    public sealed class FlightSearchService : DatabaseServiceBase
    {
        public FlightSearchService(TravelDatabase database) : base(database)
        {
        }

#region Load Options Async
        public Task<FlightSearchOptions> LoadOptionsAsync(CancellationToken cancellationToken = default)
        {
            return base.ExecuteAsync(connection =>
            {
                var options = LoadOptions(connection);
                cancellationToken.ThrowIfCancellationRequested();
                return options;
            }, cancellationToken);
        }
#endregion

#region Search Async
        public Task<FlightSearchResult> SearchAsync(
            FlightSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentException("Choose the route and travel dates first.", nameof(request));
            var snapshot = request.Snapshot();
            var elapsed = Stopwatch.StartNew();
            return base.ExecuteAsync(connection =>
            {
                Validate(snapshot, LoadOptions(connection));
                var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);
                var outbound = FindLeg(connection, snapshot, false, zones, cancellationToken);
                var returning = FindLeg(connection, snapshot, true, zones, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new FlightSearchResult
                {
                    Outbound = outbound.AsReadOnly(),
                    Return = returning.AsReadOnly(),
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
            }, cancellationToken);
        }
#endregion

#region Explain Outbound Query Async
        /// <summary>Developer diagnostic: asks SQLite to explain the actual outbound search SQL.</summary>
        public Task<IReadOnlyList<string>> ExplainOutboundQueryAsync(
            FlightSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentException("Choose the route and travel dates first.", nameof(request));
            var snapshot = request.Snapshot();
            return base.ExecuteAsync<IReadOnlyList<string>>(connection =>
            {
                Validate(snapshot, LoadOptions(connection));
                string sql = FlightSearchQuery.Build(snapshot, false, out var arguments);
                var details = connection.Query<FlightQueryPlanRow>("EXPLAIN QUERY PLAN " + sql, arguments)
                    .Select(row => row.Detail)
                    .ToList();
                cancellationToken.ThrowIfCancellationRequested();
                return details.AsReadOnly();
            }, cancellationToken);
        }
#endregion

#region Load Options
        private static FlightSearchOptions LoadOptions(SQLiteConnection connection)
        {
            var airports = connection.Query<AirportOption>(
                "SELECT a.id AS Id, a.destination_id AS DestinationId, d.name AS City, a.name AS Name, " +
                "a.time_zone AS TimeZoneId FROM airports a JOIN destinations d ON d.id=a.destination_id ORDER BY " +
                "d.name, a.id");
            var airlines = connection.Query<AirlineOption>("SELECT id AS Id, name AS Name FROM airlines ORDER BY name, id");
            string start = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_start_date");
            string end = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_end_date");
            if (!TryDate(start, out var first) || !TryDate(end, out var last) || last < first)
                throw new InvalidOperationException("The travel catalog has missing or invalid date information.");
            if (airports.Count == 0 || airlines.Count == 0)
                throw new InvalidOperationException("The travel catalog is missing airports or airlines.");
            return new FlightSearchOptions
            {
                Airports = airports.AsReadOnly(),
                Airlines = airlines.AsReadOnly(),
                StartDate = start,
                EndDate = end
            };
        }
#endregion

#region Validate
        private static void Validate(FlightSearchRequest request, FlightSearchOptions options)
        {
            if (!options.Airports.Any(airport => airport.Id == request.OriginAirportId))
                throw new ArgumentException("Choose a valid origin airport.");
            if (!options.Airports.Any(airport => airport.Id == request.DestinationAirportId))
                throw new ArgumentException("Choose a valid destination airport.");
            if (request.OriginAirportId == request.DestinationAirportId)
                throw new ArgumentException("Choose different origin and destination airports.");
            if (!TryDate(request.DepartureDate, out var departure) || !TryDate(request.ReturnDate, out var returning))
                throw new ArgumentException("Enter both dates as YYYY-MM-DD.");
            if (returning < departure)
                throw new ArgumentException("The return date must be on or after the departure date.");
            TryDate(options.StartDate, out var first);
            TryDate(options.EndDate, out var last);
            if (departure < first || returning > last)
                throw new ArgumentException(
                    "Choose dates from " +
                    options.StartDate +
                    " through " +
                    options.EndDate +
                    ".");
            if (request.MaxPriceCents.HasValue && request.MaxPriceCents.Value < 0)
                throw new ArgumentException("The maximum price cannot be negative.");
            if (request.AirlineId != null && !options.Airlines.Any(airline => airline.Id == request.AirlineId))
                throw new ArgumentException("Choose a valid airline, or all airlines.");
            if (!Enum.IsDefined(typeof(FlightSort), request.Sort) ||
                !Enum.IsDefined(typeof(DepartureTimeBand), request.TimeBand))
                throw new ArgumentException("Choose a valid sorting and departure-time option.");
        }
#endregion

#region Try Date
        private static bool TryDate(string value, out DateTime date) => DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
#endregion
#region Find Leg
        private static List<FlightOption> FindLeg(
            SQLiteConnection connection,
            FlightSearchRequest request,
            bool returning,
            Dictionary<string, TimeZoneInfo> zones,
            CancellationToken token)
        {
            string sql = FlightSearchQuery.Build(request, returning, out var arguments);
            var rows = connection.Query<FlightRow>(sql, arguments);
            token.ThrowIfCancellationRequested();
            var flights = new List<FlightOption>();
            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                var flight = FlightSearchQuery.ConvertRow(row, zones);
                // Every leg uses its departure airport's local clock, including the return leg.
                if (MatchesTime(flight.DepartureLocal.Hour, request.TimeBand))
                    flights.Add(flight);
            }

            flights.Sort((left, right) => Compare(left, right, request.Sort));
            return flights;
        }
#endregion

#region Matches Time
        private static bool MatchesTime(int hour, DepartureTimeBand band)
        {
            switch (band)
            {
                case DepartureTimeBand.Night:
                    return hour < 6;
                case DepartureTimeBand.Morning:
                    return hour >= 6 && hour < 12;
                case DepartureTimeBand.Afternoon:
                    return hour >= 12 && hour < 18;
                case DepartureTimeBand.Evening:
                    return hour >= 18;
                default:
                    return true;
            }
        }
#endregion

#region Compare
        private static int Compare(FlightOption left, FlightOption right, FlightSort sort)
        {
            int result;
            switch (sort)
            {
                case FlightSort.PriceDescending:
                    result = right.PriceCents.CompareTo(left.PriceCents);
                    break;
                case FlightSort.DepartureEarliest:
                    result = left.DepartureLocal.CompareTo(right.DepartureLocal);
                    break;
                case FlightSort.DepartureLatest:
                    result = right.DepartureLocal.CompareTo(left.DepartureLocal);
                    break;
                case FlightSort.AirlineName:
                    result = StringComparer.OrdinalIgnoreCase.Compare(left.AirlineName, right.AirlineName);
                    break;
                default:
                    result = left.PriceCents.CompareTo(right.PriceCents);
                    break;
            }

            // Stable tie-breaking makes repeated searches display the same order.
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.Id, right.Id);
        }
#endregion
    }
}
