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
using TravelPlanning.Flights;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class FlightSearchTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private FlightSearchService service;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelFlightTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
            service = new FlightSearchService(database);
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
#region RoundTripReturnsTwoAirlinesPerLegWithinThreeSecondsOffMainThread
        [UnityTest]
        public IEnumerator RoundTripReturnsTwoAirlinesPerLegWithinThreeSecondsOffMainThread()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var task = service.SearchAsync(Request());
            yield return Wait(task);
            var result = task.Result;
            Assert.That(result.Outbound.Count, Is.EqualTo(2));
            Assert.That(result.Return.Count, Is.EqualTo(2));
            Assert.That(result.Outbound.Select(flight => flight.AirlineId), Is.EquivalentTo(new[] { "BA", "AA" }));
            Assert.That(result.Return.Select(flight => flight.AirlineId), Is.EquivalentTo(new[] { "BA", "AA" }));
            Assert.That(result.Outbound.All(flight => flight.OriginAirportId == "JFK" && flight.DestinationAirportId == "LHR" &&
                flight.DepartureLocal.Date == new DateTime(2027, 6, 15)), Is.True);
            Assert.That(result.Return.All(flight => flight.OriginAirportId == "LHR" && flight.DestinationAirportId == "JFK" &&
                flight.DepartureLocal.Date == new DateTime(2027, 6, 22)), Is.True);
            var overnight = result.Outbound.Single(flight => flight.AirlineId == "AA");
            Assert.That(overnight.ArrivalLocal, Is.EqualTo(new DateTime(2027, 6, 16, 5, 0, 0)));
            Assert.That(overnight.DepartureLocal, Is.EqualTo(new DateTime(2027, 6, 15, 17, 0, 0)));
            Assert.That(result.WorkerThreadId, Is.Not.EqualTo(mainThread));
            Assert.That(result.ElapsedMilliseconds, Is.LessThan(3000));
        }

#endregion
#region AllSortsApplyIndependentlyToBothLegs
        [UnityTest]
        public IEnumerator AllSortsApplyIndependentlyToBothLegs()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (FlightSort sort in Enum.GetValues(typeof(FlightSort)))
            {
                var request = Request();
                request.Sort = sort;
                var task = service.SearchAsync(request);
                yield return Wait(task);
                foreach (var leg in new[]
                {
                    task.Result.Outbound,
                    task.Result.Return
                }

                )
                {
                    IEnumerable<FlightOption> expected;
                    switch (sort)
                    {
                        case FlightSort.PriceDescending:
                            expected = leg.OrderByDescending(flight => flight.PriceCents);
                            break;
                        case FlightSort.DepartureEarliest:
                            expected = leg.OrderBy(flight => flight.DepartureLocal);
                            break;
                        case FlightSort.DepartureLatest:
                            expected = leg.OrderByDescending(flight => flight.DepartureLocal);
                            break;
                        case FlightSort.AirlineName:
                            expected = leg.OrderBy(flight => flight.AirlineName, StringComparer.OrdinalIgnoreCase);
                            break;
                        default:
                            expected = leg.OrderBy(flight => flight.PriceCents);
                            break;
                    }

                    Assert.That(leg.Select(flight => flight.Id), Is.EqualTo(expected.Select(flight => flight.Id)), sort.ToString());
                }
            }
        }

#endregion
#region CombinedFiltersUseEachOriginsLocalClockAndInclusivePriceLimit
        [UnityTest]
        public IEnumerator CombinedFiltersUseEachOriginsLocalClockAndInclusivePriceLimit()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var request = Request();
            request.AirlineId = "BA";
            request.MaxPriceCents = 52700;
            request.TimeBand = DepartureTimeBand.Morning;
            var morning = service.SearchAsync(request);
            yield return Wait(morning);
            Assert.That(morning.Result.Outbound.Single().AirlineId, Is.EqualTo("BA"));
            Assert.That(morning.Result.Return.Single().AirlineId, Is.EqualTo("BA"));
            Assert.That(morning.Result.Outbound.Single().DepartureLocal.Hour, Is.EqualTo(8));
            Assert.That(morning.Result.Outbound.Single().DepartureUtc.Hour, Is.EqualTo(12));
            Assert.That(morning.Result.Return.Single().DepartureLocal.Hour, Is.EqualTo(8));
            request.MaxPriceCents = 52699;
            var tooCheap = service.SearchAsync(request);
            yield return Wait(tooCheap);
            Assert.That(tooCheap.Result.Outbound, Is.Empty);
            Assert.That(tooCheap.Result.Return, Is.Empty);
            request.MaxPriceCents = null;
            request.AirlineId = "AA";
            request.TimeBand = DepartureTimeBand.Afternoon;
            var afternoon = service.SearchAsync(request);
            yield return Wait(afternoon);
            Assert.That(afternoon.Result.Outbound.Single().DepartureLocal.Hour, Is.EqualTo(17));
            Assert.That(afternoon.Result.Outbound.Single().DepartureUtc.Hour, Is.EqualTo(21));
            Assert.That(afternoon.Result.Return.Single().DepartureLocal.Hour, Is.EqualTo(17));
            foreach (var band in new[]
            {
                DepartureTimeBand.Night,
                DepartureTimeBand.Evening
            }

            )
            {
                request.AirlineId = null;
                request.TimeBand = band;
                var empty = service.SearchAsync(request);
                yield return Wait(empty);
                Assert.That(empty.Result.Outbound, Is.Empty);
                Assert.That(empty.Result.Return, Is.Empty);
            }
        }

#endregion
#region ValidUnseededRouteReturnsEmptyLists
        [UnityTest]
        public IEnumerator ValidUnseededRouteReturnsEmptyLists()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var request = Request();
            request.OriginAirportId = "LHR";
            request.DestinationAirportId = "HND";
            var task = service.SearchAsync(request);
            yield return Wait(task);
            Assert.That(task.Result.Outbound, Is.Empty);
            Assert.That(task.Result.Return, Is.Empty);
        }

#endregion
#region InvalidRequestValuesAreRejectedBeforeSearch
        [UnityTest]
        public IEnumerator InvalidRequestValuesAreRejectedBeforeSearch()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var changes = new Action<FlightSearchRequest>[]
            {
                request => request.OriginAirportId = "XXX",
                request => request.DestinationAirportId = "JFK",
                request => request.ReturnDate = "2027-06-14",
                request => request.DepartureDate = "2027-05-31",
                request => request.ReturnDate = "2027-07-01",
                request => request.DepartureDate = "06/15/2027",
                request => request.ReturnDate = "2027-06-31",
                request => request.DepartureDate = null,
                request => request.OriginAirportId = "JFK' OR 1=1 --",
                request => request.AirlineId = "BA' OR 1=1 --",
                request => request.MaxPriceCents = -1,
                request => request.Sort = (FlightSort)999,
                request => request.TimeBand = (DepartureTimeBand)(-1)
            };
            foreach (var change in changes)
            {
                var request = Request();
                change(request);
                var task = service.SearchAsync(request);
                yield return Completion(task);
                Assert.That(task.IsFaulted, Is.True);
                Assert.That(task.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
            }

            Assert.Throws<ArgumentException>(() => service.SearchAsync(null));
        }

#endregion
#region CatalogOptionsComeFromDatabaseAndMissingMetadataIsAnError
        [UnityTest]
        public IEnumerator CatalogOptionsComeFromDatabaseAndMissingMetadataIsAnError()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var original = service.LoadOptionsAsync();
            yield return Wait(original);
            Assert.That(original.Result.Airports.Count, Is.EqualTo(12));
            Assert.That(original.Result.StartDate, Is.EqualTo("2027-06-01"));
            Assert.That(original.Result.EndDate, Is.EqualTo("2027-06-30"));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("INSERT INTO destinations (id,name,country,region,description) VALUES (?,?,?,?,?)", "extra", "Extra City", "Example", "Demo", "Test");
                connection.Execute("INSERT INTO airports (id,destination_id,name,time_zone) VALUES (?,?,?,?)", "EXT", "extra", "Extra Airport", "UTC");
                return 0;
            }, CancellationToken.None));
            var expanded = service.LoadOptionsAsync();
            yield return Wait(expanded);
            Assert.That(expanded.Result.Airports.Count, Is.EqualTo(original.Result.Airports.Count + 1));
            Assert.That(expanded.Result.Airports.Any(airport => airport.Id == "EXT" && airport.City == "Extra City"), Is.True);
            yield return Wait(database.ExecuteAsync(connection => connection.Execute("DELETE FROM metadata WHERE key=?", "seed_start_date"), CancellationToken.None));
            var missing = service.LoadOptionsAsync();
            yield return Completion(missing);
            Assert.That(missing.IsFaulted, Is.True);
            Assert.That(missing.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
        }

#endregion
#region ActualSearchQueryUsesRouteDateIndex
        [UnityTest]
        public IEnumerator ActualSearchQueryUsesRouteDateIndex()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var request = Request();
            request.AirlineId = "BA";
            request.MaxPriceCents = 55000;
            var plan = service.ExplainOutboundQueryAsync(request);
            yield return Wait(plan);
            Assert.That(plan.Result.Any(detail => detail.Contains("flights_search") && detail.Contains("origin_airport_id") &&
                detail.Contains("departure_local_date")), Is.True, string.Join("\n", plan.Result));
        }

#endregion
#region SearchesDoNotChangeExistingUserTripOrDatabaseBytes
        [UnityTest]
        public IEnumerator SearchesDoNotChangeExistingUserTripOrDatabaseBytes()
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
            yield return Wait(service.LoadOptionsAsync());
            yield return Wait(service.SearchAsync(Request()));
            yield return Wait(service.ExplainOutboundQueryAsync(Request()));
            Assert.That(Hash(), Is.EqualTo(before));
        }

#endregion
#region RequestIsSnapshottedAndCancellationIsHonored
        [UnityTest]
        public IEnumerator RequestIsSnapshottedAndCancellationIsHonored()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            {
                var blocker = database.ExecuteAsync(connection =>
                {
                    entered.Set();
                    return release.Wait(TimeSpan.FromSeconds(15));
                }, CancellationToken.None);
                while (!entered.IsSet)
                    yield return null;
                Task<FlightSearchResult> search;
                try
                {
                    var request = Request();
                    search = service.SearchAsync(request);
                    request.OriginAirportId = "Changed after starting";
                    request.DepartureDate = "invalid";
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                yield return Wait(search);
                Assert.That(search.Result.Outbound.Count, Is.EqualTo(2));
            }

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var search = service.SearchAsync(Request(), cancelled.Token);
                var options = service.LoadOptionsAsync(cancelled.Token);
                yield return Completion(search);
                yield return Completion(options);
                Assert.That(search.IsCanceled, Is.True);
                Assert.That(options.IsCanceled, Is.True);
            }
        }

#endregion
#region Request
        private static FlightSearchRequest Request() => new FlightSearchRequest
        {
            OriginAirportId = "JFK",
            DestinationAirportId = "LHR",
            DepartureDate = "2027-06-15",
            ReturnDate = "2027-06-22"
        };
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
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Flight operation timed out.");
                yield return null;
            }
        }
#endregion
    }
}
