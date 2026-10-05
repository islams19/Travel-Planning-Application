using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Data;
using TravelPlanning.Destinations;
using TravelPlanning.Flights;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    /// <summary>Checks the real seeded SQLite catalog using isolated temporary copies.</summary>
    public sealed class DestinationHubTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private DestinationHubService service;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelDestinationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
            service = new DestinationHubService(database);
        }

#endregion
#region TearDown
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

#endregion
#region AllTwelveDestinationsHaveCorrectPlacesPricesAndSampleRatingsOffMainThread
        [UnityTest]
        public IEnumerator AllTwelveDestinationsHaveCorrectPlacesPricesAndSampleRatingsOffMainThread()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            Assert.That(options.Result.Count, Is.EqualTo(12));
            foreach (var destination in options.Result)
            {
                var load = service.LoadAsync(destination.Id);
                yield return Wait(load);
                var hub = load.Result;
                Assert.That(hub.Destination.Id, Is.EqualTo(destination.Id));
                Assert.That(hub.Destination.Name, Is.EqualTo(destination.Name));
                Assert.That(hub.Destination.Country, Is.Not.Empty);
                Assert.That(hub.Destination.Region, Is.Not.Empty);
                Assert.That(hub.Destination.Description, Is.Not.Empty);
                CheckCategory(hub.Hotels, destination.Id, PlaceCategory.Hotel, new[] { 16000, 24500, 33000 });
                CheckCategory(hub.Restaurants, destination.Id, PlaceCategory.Restaurant, new[] { 2500, 4700, 6900 });
                CheckCategory(hub.Experiences, destination.Id, PlaceCategory.Experience, new[] { 3000, 4800, 6600 });
                CheckCategory(hub.Hotspots, destination.Id, PlaceCategory.Hotspot, new[] { 0, 0, 0 });
                Assert.That(hub.WorkerThreadId, Is.Not.EqualTo(mainThread));
                Assert.That(hub.ElapsedMilliseconds, Is.LessThan(3000));
            }
        }

#endregion
#region UnknownPriceFreePlaceNoReviewsAndEmptyCategoryStayDistinct
        [UnityTest]
        public IEnumerator UnknownPriceFreePlaceNoReviewsAndEmptyCategoryStayDistinct()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.RunInTransaction(() =>
                {
                    connection.Execute("UPDATE restaurants SET price_cents=NULL WHERE id=?", "london-restaurant-1");
                    connection.Execute("UPDATE restaurants SET price_cents=0 WHERE id=?", "london-restaurant-2");
                    connection.Execute("DELETE FROM reviews WHERE restaurant_id=?", "london-restaurant-1");
                    connection.Execute("DELETE FROM reviews WHERE hotspot_id IN (SELECT id FROM hotspots WHERE destination_id=?)", "london");
                    connection.Execute("DELETE FROM hotspots WHERE destination_id=?", "london");
                });
                return 0;
            }, CancellationToken.None));
            var load = service.LoadAsync("london");
            yield return Wait(load);
            var unknown = load.Result.Restaurants.Single(place => place.Id == "london-restaurant-1");
            var free = load.Result.Restaurants.Single(place => place.Id == "london-restaurant-2");
            Assert.That(unknown.PriceCents, Is.Null);
            Assert.That(unknown.AverageRating, Is.Null);
            Assert.That(unknown.ReviewCount, Is.Zero);
            Assert.That(free.PriceCents, Is.EqualTo(0));
            Assert.That(free.AverageRating, Is.EqualTo(4.0));
            Assert.That(free.ReviewCount, Is.EqualTo(2));
            Assert.That(load.Result.Hotspots, Is.Empty);
            Assert.That(load.Result.Hotels.Count, Is.EqualTo(3));
        }

#endregion
#region UnknownAndSqlInjectionIdsAreRejected
        [UnityTest]
        public IEnumerator UnknownAndSqlInjectionIdsAreRejected()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (string id in new[]
            {
                "unknown",
                "london' OR 1=1 --",
                "london'; DELETE FROM destinations; --",
                "LONDON"
            }

            )
            {
                var load = service.LoadAsync(id);
                yield return Completion(load);
                Assert.That(load.IsFaulted, Is.True);
                Assert.That(load.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
            }

            Assert.Throws<ArgumentException>(() => service.LoadAsync(null));
            Assert.Throws<ArgumentException>(() => service.LoadAsync(""));
            Assert.Throws<ArgumentException>(() => service.LoadAsync("  "));
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            Assert.That(options.Result.Count, Is.EqualTo(12));
        }

#endregion
#region CancellationWhileWaitingForDatabaseStopsHubLoad
        [UnityTest]
        public IEnumerator CancellationWhileWaitingForDatabaseStopsHubLoad()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection =>
                {
                    entered.Set();
                    return release.Wait(TimeSpan.FromSeconds(15));
                }, CancellationToken.None);
                while (!entered.IsSet)
                    yield return null;
                Task<DestinationHubResult> pending;
                Task<IReadOnlyList<DestinationOption>> options;
                try
                {
                    pending = service.LoadAsync("london", cancellation.Token);
                    options = service.LoadOptionsAsync(cancellation.Token);
                    cancellation.Cancel();
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                yield return Completion(pending);
                yield return Completion(options);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(options.IsCanceled, Is.True);
            }
        }

#endregion
#region BrowsingPreservesExistingUserTripAndAllDatabaseBytes
        [UnityTest]
        public IEnumerator BrowsingPreservesExistingUserTripAndAllDatabaseBytes()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute(
                    "INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                    "owner", "owner@example.com", "testfixture", "testfixture", 600000, "2026-09-29T00:00:00Z");
                connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)", "trip",
                    "owner", "Keep my trip", "2027-06-15", "2027-06-22", "2026-09-29T00:00:00Z", "2026-09-29T00:00:00Z");
                return 0;
            }, CancellationToken.None));
            byte[] before = Hash();
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            foreach (var destination in options.Result)
                yield return Wait(service.LoadAsync(destination.Id));
            Assert.That(Hash(), Is.EqualTo(before));
        }

#endregion
#region NewDestinationNeedsOnlyDataAndAirportOptionsExposeDestinationId
        [UnityTest]
        public IEnumerator NewDestinationNeedsOnlyDataAndAirportOptionsExposeDestinationId()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("INSERT INTO destinations (id,name,country,region,description) VALUES (?,?,?,?,?)", "extra", "Extra City",
                    "Example", "Demo", "New destination fixture");
                connection.Execute("INSERT INTO airports (id,destination_id,name,time_zone) VALUES (?,?,?,?)", "EXT", "extra", "Extra Airport", "UTC");
                return 0;
            }, CancellationToken.None));
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            Assert.That(options.Result.Count, Is.EqualTo(13));
            var hub = service.LoadAsync("extra");
            yield return Wait(hub);
            Assert.That(hub.Result.Destination.Name, Is.EqualTo("Extra City"));
            Assert.That(hub.Result.Hotels, Is.Empty);
            Assert.That(hub.Result.Restaurants, Is.Empty);
            Assert.That(hub.Result.Experiences, Is.Empty);
            Assert.That(hub.Result.Hotspots, Is.Empty);
            var flightOptions = new FlightSearchService(database).LoadOptionsAsync();
            yield return Wait(flightOptions);
            Assert.That(flightOptions.Result.Airports.Single(airport => airport.Id == "LHR").DestinationId, Is.EqualTo("london"));
            Assert.That(flightOptions.Result.Airports.Single(airport => airport.Id == "EXT").DestinationId, Is.EqualTo("extra"));
        }

#endregion
#region CheckCategory
        private static void CheckCategory(IReadOnlyList<PlaceOption> places, string destinationId, PlaceCategory category, int[] prices)
        {
            Assert.That(places.Count, Is.EqualTo(3));
            Assert.That(places.Select(place => place.Id).Distinct().Count(), Is.EqualTo(3));
            var orderedById = places.OrderBy(place => place.Id, StringComparer.Ordinal).ToArray();
            for (int index = 0; index < orderedById.Length; index++)
            {
                var place = orderedById[index];
                Assert.That(place.DestinationId, Is.EqualTo(destinationId));
                Assert.That(place.Category, Is.EqualTo(category));
                Assert.That(place.PriceCents, Is.EqualTo(prices[index]));
                Assert.That(place.Currency, Is.EqualTo("USD"));
                Assert.That(place.AverageRating, Is.EqualTo(4.5 - index * 0.5));
                Assert.That(place.ReviewCount, Is.EqualTo(2));
                Assert.That(place.Name, Is.Not.Empty);
                Assert.That(place.Description, Is.Not.Empty);
                Assert.That(place.Address, Is.Not.Empty);
                Assert.That(place.GoogleMapsUrl, Does.StartWith("https://www.google.com/maps/search/"));
            }
        }

#endregion
#region Hash
        private byte[] Hash()
        {
            using (var hash = SHA256.Create())
                return hash.ComputeHash(File.ReadAllBytes(path));
        }

#endregion
#region Wait
        private static IEnumerator Wait(Task task)
        {
            yield return Completion(task);
            task.GetAwaiter().GetResult();
        }

#endregion
#region Completion
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Destination operation timed out.");
                yield return null;
            }
        }
#endregion
    }
}
