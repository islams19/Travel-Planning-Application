using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SQLite;
using TravelPlanning.Data;
using UnityEditor;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Turns editable JSON into a checked SQLite template, never into a user's live file.</summary>
    public static class TravelSeedBuilder
    {
        public const string JsonPath = "Assets/TravelPlanning/SeedData/catalog.json";
        public const string DatabasePath = "Assets/StreamingAssets/Database/travel_seed.db";
        private static readonly string[] Kinds =
        {
            "hotel",
            "restaurant",
            "experience",
            "hotspot"
        };
#region BuildSeed
        [MenuItem("Travel Planning/Database/Build Seed Database")]
        public static void BuildSeed()
        {
            BuildFromJson(JsonPath, DatabasePath);
            AssetDatabase.Refresh();
        }

#endregion
#region BuildFromJson
        public static void BuildFromJson(string jsonPath, string outputPath)
        {
            TravelSeedData data = JsonUtility.FromJson<TravelSeedData>(File.ReadAllText(jsonPath));
            ValidateData(data);
            SqliteRuntime.Initialize();
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            string staging = outputPath + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var db = new SQLiteConnection(staging))
                {
                    TravelDatabaseSchema.Create(db);
                    db.RunInTransaction(() => InsertData(db, data));
                    TravelDatabaseSchema.Validate(db);
                    if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM users") != 0)
                        throw new InvalidDataException("A seed must not contain user accounts.");
                }

                // Validate completely before replacing the template. Its Unity .meta is untouched.
                if (File.Exists(outputPath))
                    File.Replace(staging, outputPath, null);
                else
                    File.Move(staging, outputPath);
                Debug.Log("TRAVEL_SEED_READY catalog=" + data.catalogVersion + " destinations=" + data.destinations.Length + " flights=" + data.flights.Length);
            }
            finally
            {
                if (File.Exists(staging))
                    File.Delete(staging);
            }
        }

#endregion
#region ValidateData
        // The importer validates data, not a fixed list of city names or a fixed count of cities.
        public static void ValidateData(TravelSeedData data)
        {
            if (data == null)
                throw new InvalidDataException("The catalog JSON is empty.");
            Required(data.catalogVersion, "catalogVersion");
            DateTime start = Date(data.startDate), end = Date(data.endDate);
            if (end < start)
                throw new InvalidDataException("The catalog date range is reversed.");
            if (data.destinations == null || data.airports == null || data.airlines == null || data.flights == null || data.places == null || data.reviews == null)
                throw new InvalidDataException("All catalog arrays must be present, even when empty.");
            var destinations = Ids(data.destinations.Select(x => x?.id), "destination");
            var airports = Ids(data.airports.Select(x => x?.id), "airport");
            var airlines = Ids(data.airlines.Select(x => x?.id), "airline");
            Ids(data.flights.Select(x => x?.id), "flight");
            var places = Ids(data.places.Select(x => x?.id), "place");
            Ids(data.reviews.Select(x => x?.id), "review");
            if (destinations.Count == 0)
                throw new InvalidDataException("At least one destination is required.");
            foreach (var d in data.destinations)
            {
                Required(d.name, d.id);
                Required(d.country, d.id);
                Required(d.region, d.id);
                Required(d.description, d.id);
            }

            foreach (var a in data.airlines)
                Required(a.name, a.id);
            var zones = new Dictionary<string, TimeZoneInfo>();
            foreach (var a in data.airports)
            {
                Reference(destinations, a.destinationId, a.id);
                Required(a.name, a.id);
                if (a.id.Length != 3 || a.id.Any(c => c < 'A' || c > 'Z'))
                    throw new InvalidDataException("Airport ID must be three uppercase letters: " + a.id);
                zones[a.id] = TimeZoneInfo.FindSystemTimeZoneById(a.timeZone);
            }

            foreach (var f in data.flights)
            {
                Reference(airports, f.originAirportId, f.id);
                Reference(airports, f.destinationAirportId, f.id);
                Reference(airlines, f.airlineId, f.id);
                Required(f.flightNumber, f.id);
                Price(f.priceCents, f.id);
                DateTime departure = Utc(f.departureUtc), arrival = Utc(f.arrivalUtc), local = Date(f.departureLocalDate);
                if (arrival <= departure || f.originAirportId == f.destinationAirportId || f.availableSeats < 0 || local < start || local > end)
                    throw new InvalidDataException("Invalid flight dates, route or seats: " + f.id);
                if (TimeZoneInfo.ConvertTimeFromUtc(departure, zones[f.originAirportId]).Date != local)
                    throw new InvalidDataException("Departure local date does not match its airport time zone: " + f.id);
            }

            var kinds = new Dictionary<string, string>();
            foreach (var p in data.places)
            {
                Kind(p.kind);
                kinds[p.id] = p.kind;
                Reference(destinations, p.destinationId, p.id);
                Required(p.name, p.id);
                Required(p.description, p.id);
                Required(p.address, p.id);
                Price(p.priceCents, p.id);
                if (!Uri.TryCreate(p.googleMapsUrl, UriKind.Absolute, out Uri url) || url.Scheme != "https" || !(url.Host == "www.google.com" ||
                    url.Host == "google.com") || url.AbsolutePath != "/maps/search/" || !url.Query.Contains("query="))
                    throw new InvalidDataException("Use an HTTPS Google Maps search reference: " + p.id);
            }

            foreach (var r in data.reviews)
            {
                Reference(places, r.placeId, r.id);
                Kind(r.kind);
                Required(r.travelerName, r.id);
                Required(r.body, r.id);
                if (r.rating < 1 || r.rating > 5 || kinds[r.placeId] != r.kind)
                    throw new InvalidDataException("Invalid review rating or target: " + r.id);
            }
        }

#endregion
#region InsertData
        private static void InsertData(SQLiteConnection db, TravelSeedData data)
        {
            foreach (var pair in new[]
            {
                new[]
                {
                    "catalog_version",
                    data.catalogVersion
                },
                new[]
                {
                    "is_demo",
                    "1"
                },
                new[]
                {
                    "seed_start_date",
                    data.startDate
                },
                new[]
                {
                    "seed_end_date",
                    data.endDate
                }
            }

            )
                db.Execute("INSERT INTO metadata(key,value) VALUES (?,?)", pair[0], pair[1]);
            foreach (var d in data.destinations)
                db.Execute("INSERT INTO destinations VALUES (?,?,?,?,?)", d.id, d.name, d.country, d.region, d.description);
            foreach (var a in data.airports)
                db.Execute("INSERT INTO airports VALUES (?,?,?,?)", a.id, a.destinationId, a.name, a.timeZone);
            foreach (var a in data.airlines)
                db.Execute("INSERT INTO airlines VALUES (?,?)", a.id, a.name);
            foreach (var f in data.flights)
                db.Execute("INSERT INTO flights VALUES (?,?,?,?,?,?,?,?,?,?,?)", f.id, f.airlineId, f.flightNumber, f.originAirportId,
                    f.destinationAirportId, f.departureUtc, f.arrivalUtc, f.departureLocalDate, f.priceCents, f.availableSeats, "USD");
            foreach (var p in data.places)
                db.Execute("INSERT INTO " + Table(p.kind) + " VALUES (?,?,?,?,?,?,?,?)", p.id, p.destinationId, p.name, p.description, p.address,
                    p.priceCents, "USD", p.googleMapsUrl);
            foreach (var r in data.reviews)
                db.Execute("INSERT INTO reviews(id," + r.kind + "_id,traveler_name,rating,body,is_demo) VALUES (?,?,?,?,?,?)", r.id, r.placeId,
                    r.travelerName, r.rating, r.body, 1);
        }

#endregion
#region Ids
        private static HashSet<string> Ids(IEnumerable<string> ids, string label)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                Required(id, label);
                if (!result.Add(id))
                    throw new InvalidDataException("Duplicate " + label + " ID: " + id);
            }

            return result;
        }

#endregion
#region Required
        private static void Required(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
                throw new InvalidDataException("Missing or padded text: " + label);
        }

#endregion
#region Reference
        private static void Reference(HashSet<string> ids, string id, string owner)
        {
            if (id == null || !ids.Contains(id))
                throw new InvalidDataException("Unknown reference in " + owner + ": " + id);
        }

#endregion
#region Price
        private static void Price(int cents, string owner)
        {
            if (cents < 0)
                throw new InvalidDataException("Negative price: " + owner);
        }

#endregion
#region Kind
        private static void Kind(string kind)
        {
            if (!Kinds.Contains(kind))
                throw new InvalidDataException("Unknown venue kind: " + kind);
        }

#endregion
#region Table
        private static string Table(string kind)
        {
            Kind(kind);
            return kind == "hotspot" ? "hotspots" : kind + "s";
        }

#endregion
#region Date
        private static DateTime Date(string value)
        {
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                throw new InvalidDataException("Invalid date: " + value);
            return result;
        }

#endregion
#region Utc
        private static DateTime Utc(string value)
        {
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result))
                throw new InvalidDataException("Invalid UTC timestamp: " + value);
            return result;
        }
#endregion
    }
}
