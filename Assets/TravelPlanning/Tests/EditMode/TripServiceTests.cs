using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Data;
using TravelPlanning.Trips;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TripServiceTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private TripService owner;
        private TripService other;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelTripTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = OpenDatabase();
            owner = new TripService(database, "owner");
            other = new TripService(database, "other");
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
#region ListsAndEveryTripOperationRespectOwner
        [UnityTest]
        public IEnumerator ListsAndEveryTripOperationRespectOwner()
        {
            yield return Ready();
            var first = owner.CreateAsync("My trip", "2027-06-15", "2027-06-22");
            var second = other.CreateAsync("Other trip", "2027-06-15", "2027-06-22");
            yield return Wait(first);
            yield return Wait(second);
            var saved = owner.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-1");
            yield return Wait(saved);
            var ownList = owner.ListAsync();
            var otherList = other.ListAsync();
            yield return Wait(ownList);
            yield return Wait(otherList);
            Assert.That(ownList.Result.Trips.Single().Id, Is.EqualTo(first.Result.Id));
            Assert.That(otherList.Result.Trips.Single().Id, Is.EqualTo(second.Result.Id));
            Assert.That(ownList.Result.StartDate, Is.EqualTo("2027-06-01"));
            Assert.That(ownList.Result.EndDate, Is.EqualTo("2027-06-30"));
            var owned = owner.LoadAsync(first.Result.Id);
            yield return Wait(owned);
            var missing = other.LoadAsync("missing");
            var forbidden = other.LoadAsync(first.Result.Id);
            yield return Failure(missing);
            yield return Failure(forbidden);
            Assert.That(forbidden.Exception.GetBaseException().Message, Is.EqualTo(missing.Exception.GetBaseException().Message));
            yield return Failure(other.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-2"));
            yield return Failure(other.RemoveAsync(first.Result.Id, owned.Result.Items.Single().Id));
            var cannotRemoveThroughOwnTrip = other.RemoveAsync(second.Result.Id, owned.Result.Items.Single().Id);
            yield return Wait(cannotRemoveThroughOwnTrip);
            Assert.That(cannotRemoveThroughOwnTrip.Result, Is.False);
            var preserved = owner.LoadAsync(first.Result.Id);
            yield return Wait(preserved);
            Assert.That(preserved.Result.Items.Single().TargetId, Is.EqualTo("london-hotel-1"));
        }

#endregion
#region AllFiveTypesHaveCurrentDetailsUnitsAndOwnFlightDates
        [UnityTest]
        public IEnumerator AllFiveTypesHaveCurrentDetailsUnitsAndOwnFlightDates()
        {
            yield return Ready();
            var created = owner.CreateAsync(" Planning labels ", "2027-06-01", "2027-06-02");
            yield return Wait(created);
            Assert.That(created.Result.Name, Is.EqualTo("Planning labels"));
            string[] targets =
            {
                "JFK-LHR-20270615-AA",
                "london-hotel-1",
                "london-restaurant-1",
                "london-experience-1",
                "london-hotspot-1"
            };
            foreach (SavedItemKind kind in Enum.GetValues(typeof(SavedItemKind)))
            {
                var save = owner.SaveAsync(created.Result.Id, kind, targets[(int)kind]);
                yield return Wait(save);
                Assert.That(save.Result, Is.True);
            }

            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var loaded = owner.LoadAsync(created.Result.Id);
            yield return Wait(loaded);
            var details = loaded.Result;
            Assert.That(details.Items.Count, Is.EqualTo(5));
            Assert.That(details.Trip.ItemCount, Is.EqualTo(5));
            Assert.That(details.WorkerThreadId, Is.Not.EqualTo(mainThread));
            string[] units =
            {
                "/ flight",
                "/ room / night",
                "/ person (meal)",
                "/ adult",
                "/ visit"
            };
            int[] prices =
            {
                56200,
                16000,
                2500,
                3000,
                0
            };
            foreach (var item in details.Items)
            {
                Assert.That(item.TargetId, Is.EqualTo(targets[(int)item.Kind]));
                Assert.That(item.Title, Is.Not.Empty);
                Assert.That(item.Details, Is.Not.Empty);
                Assert.That(item.Currency, Is.EqualTo("USD"));
                Assert.That(item.PriceUnit, Is.EqualTo(units[(int)item.Kind]));
                Assert.That(item.PriceCents, Is.EqualTo(prices[(int)item.Kind]));
            }

            var flight = details.Items.Single(item => item.Kind == SavedItemKind.Flight);
            Assert.That(flight.Details, Does.Contain("JFK to LHR"));
            Assert.That(flight.Details, Does.Contain("2027-06-15 17:00 (JFK local)"));
            Assert.That(flight.Details, Does.Contain("2027-06-16 05:00 (LHR local)"));
            var list = owner.ListAsync();
            yield return Wait(list);
            Assert.That(list.Result.WorkerThreadId, Is.Not.EqualTo(mainThread));
            Assert.That(list.Result.Trips.Single().ItemCount, Is.EqualTo(5));
        }

#endregion
#region DuplicateRaceSavesOnceButSameTargetCanBelongToTwoTrips
        [UnityTest]
        public IEnumerator DuplicateRaceSavesOnceButSameTargetCanBelongToTwoTrips()
        {
            yield return Ready();
            var first = owner.CreateAsync("First", "2027-06-15", "2027-06-22");
            var second = owner.CreateAsync("Second", "2027-06-15", "2027-06-22");
            yield return Wait(first);
            yield return Wait(second);
            var anotherService = new TripService(database, "owner");
            var race = Task.WhenAll(owner.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-1"), anotherService.SaveAsync(first.Result.Id,
                SavedItemKind.Hotel, "london-hotel-1"));
            yield return Wait(race);
            Assert.That(race.Result.Count(added => added), Is.EqualTo(1));
            var separate = owner.SaveAsync(second.Result.Id, SavedItemKind.Hotel, "london-hotel-1");
            yield return Wait(separate);
            Assert.That(separate.Result, Is.True);
            var load = owner.LoadAsync(first.Result.Id);
            yield return Wait(load);
            Assert.That(load.Result.Items.Count, Is.EqualTo(1));
            byte[] beforeDuplicate = Hash();
            var duplicate = owner.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-1");
            yield return Wait(duplicate);
            Assert.That(duplicate.Result, Is.False);
            Assert.That(Hash(), Is.EqualTo(beforeDuplicate));
            var removed = owner.RemoveAsync(first.Result.Id, load.Result.Items.Single().Id);
            yield return Wait(removed);
            Assert.That(removed.Result, Is.True);
            var again = owner.RemoveAsync(first.Result.Id, load.Result.Items.Single().Id);
            yield return Wait(again);
            Assert.That(again.Result, Is.False);
            var empty = owner.LoadAsync(first.Result.Id);
            var untouched = owner.LoadAsync(second.Result.Id);
            yield return Wait(empty);
            yield return Wait(untouched);
            Assert.That(empty.Result.Trip.ItemCount, Is.Zero);
            Assert.That(empty.Result.Items, Is.Empty);
            Assert.That(untouched.Result.Items.Count, Is.EqualTo(1));
        }

#endregion
#region ReopenKeepsSavedReferencesAndReloadUsesChangedCatalogPrice
        [UnityTest]
        public IEnumerator ReopenKeepsSavedReferencesAndReloadUsesChangedCatalogPrice()
        {
            yield return Ready();
            var created = owner.CreateAsync("Persistent", "2027-06-15", "2027-06-22");
            yield return Wait(created);
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Hotel, "london-hotel-1"));
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Restaurant, "london-restaurant-1"));
            var reopened = OpenDatabase();
            yield return Wait(reopened.InitializeAsync(CancellationToken.None));
            var service = new TripService(reopened, "owner");
            var original = service.LoadAsync(created.Result.Id);
            yield return Wait(original);
            Assert.That(original.Result.Items.Single(item => item.Kind == SavedItemKind.Hotel).PriceCents, Is.EqualTo(16000));
            yield return Wait(reopened.ExecuteAsync(connection =>
            {
                connection.Execute("UPDATE hotels SET name=?,price_cents=? WHERE id=?", "Updated hotel", 19999, "london-hotel-1");
                connection.Execute("UPDATE restaurants SET price_cents=NULL WHERE id=?", "london-restaurant-1");
                return 0;
            }, CancellationToken.None));
            var current = service.LoadAsync(created.Result.Id);
            yield return Wait(current);
            Assert.That(current.Result.Items.Single(item => item.Kind == SavedItemKind.Hotel).PriceCents, Is.EqualTo(19999));
            Assert.That(current.Result.Items.Single(item => item.Kind == SavedItemKind.Hotel).Title, Is.EqualTo("Updated hotel"));
            Assert.That(current.Result.Items.Single(item => item.Kind == SavedItemKind.Restaurant).PriceCents, Is.Null);
            Assert.That(current.Result.Items.Select(item => item.Id), Is.EqualTo(original.Result.Items.Select(item => item.Id)));
        }

#endregion
#region NamesDatesAndIdentifiersAreValidatedAndSqlTextIsOnlyData
        [UnityTest]
        public IEnumerator NamesDatesAndIdentifiersAreValidatedAndSqlTextIsOnlyData()
        {
            yield return Ready();
            foreach (string name in new[]
            {
                null,
                "",
                "  ",
                new string ('x', 61),
                "line\nbreak",
                "\tTrip"
            }

            )
                Assert.Throws<ArgumentException>(() => owner.CreateAsync(name, "2027-06-15", "2027-06-22"));
            string[][] invalidDates =
            {
                new[]
                {
                    "2027-06-22",
                    "2027-06-15"
                },
                new[]
                {
                    "2027-05-31",
                    "2027-06-22"
                },
                new[]
                {
                    "2027-06-15",
                    "2027-07-01"
                },
                new[]
                {
                    "06/15/2027",
                    "2027-06-22"
                },
                new[]
                {
                    "2027-06-31",
                    "2027-06-31"
                },
                new[]
                {
                    "2027-06-15' OR 1=1 --",
                    "2027-06-22"
                },
                new string[]
                {
                    null,
                    "2027-06-22"
                }
            };
            foreach (var dates in invalidDates)
                yield return Failure(owner.CreateAsync("Trip", dates[0], dates[1]));
            var smallest = owner.CreateAsync("x", "2027-06-01", "2027-06-01");
            var largest = owner.CreateAsync(new string ('x', 60), "2027-06-01", "2027-06-30");
            yield return Wait(smallest);
            yield return Wait(largest);
            const string sqlName = "Trip'); DROP TABLE trips; --";
            var literal = owner.CreateAsync(sqlName, "2027-06-15", "2027-06-22");
            yield return Wait(literal);
            Assert.That(literal.Result.Name, Is.EqualTo(sqlName));
            Assert.Throws<ArgumentException>(() => owner.SaveAsync(literal.Result.Id, (SavedItemKind)99, "london-hotel-1"));
            yield return Failure(owner.LoadAsync("' OR 1=1 --"));
            yield return Failure(owner.SaveAsync(literal.Result.Id, SavedItemKind.Hotel, "london-restaurant-1"));
            yield return Failure(owner.SaveAsync(literal.Result.Id, SavedItemKind.Hotel, "london-hotel-1' OR 1=1 --"));
            yield return Failure(owner.SaveAsync("' OR 1=1 --", SavedItemKind.Hotel, "london-hotel-1"));
            yield return Failure(owner.RemoveAsync("' OR 1=1 --", "anything"));
            var harmless = owner.RemoveAsync(literal.Result.Id, "' OR 1=1 --");
            yield return Wait(harmless);
            Assert.That(harmless.Result, Is.False);
            var list = owner.ListAsync();
            yield return Wait(list);
            Assert.That(list.Result.Trips.Count, Is.EqualTo(3));
        }

#endregion
#region CancelledQueuedWritesDoNotMutateDatabase
        [UnityTest]
        public IEnumerator CancelledQueuedWritesDoNotMutateDatabase()
        {
            yield return Ready();
            var created = owner.CreateAsync("Keep", "2027-06-15", "2027-06-22");
            yield return Wait(created);
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Hotel, "london-hotel-1"));
            var trip = owner.LoadAsync(created.Result.Id);
            yield return Wait(trip);
            byte[] before = Hash();
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
                Task[] pending;
                try
                {
                    pending = new Task[]
                    {
                        owner.CreateAsync("Cancelled", "2027-06-15", "2027-06-22", cancellation.Token),
                        owner.SaveAsync(created.Result.Id, SavedItemKind.Hotel, "london-hotel-2", cancellation.Token),
                        owner.RemoveAsync(created.Result.Id, trip.Result.Items.Single().Id, cancellation.Token)
                    };
                    cancellation.Cancel();
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                foreach (var operation in pending)
                {
                    yield return Completion(operation);
                    Assert.That(operation.IsCanceled, Is.True);
                }
            }

            Assert.That(Hash(), Is.EqualTo(before));
        }

#endregion
#region ReadOnlyCallsLeaveTripAndCatalogBytesUnchanged
        [UnityTest]
        public IEnumerator ReadOnlyCallsLeaveTripAndCatalogBytesUnchanged()
        {
            yield return Ready();
            var created = owner.CreateAsync("Keep", "2027-06-15", "2027-06-22");
            yield return Wait(created);
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Hotspot, "london-hotspot-1"));
            byte[] before = Hash();
            yield return Wait(owner.ListAsync());
            yield return Wait(owner.LoadAsync(created.Result.Id));
            Assert.That(Hash(), Is.EqualTo(before));
        }

#endregion
#region InvalidCatalogCalendarAndUnknownAccountFailWithoutCreatingTrip
        [UnityTest]
        public IEnumerator InvalidCatalogCalendarAndUnknownAccountFailWithoutCreatingTrip()
        {
            yield return Ready();
            var unknown = new TripService(database, "owner' OR 1=1 --");
            var create = unknown.CreateAsync("No owner", "2027-06-15", "2027-06-22");
            yield return Completion(create);
            Assert.That(create.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            yield return Wait(database.ExecuteAsync(connection => connection.Execute("UPDATE metadata SET value=? WHERE key=?", "2030-06-30",
                "seed_end_date"), CancellationToken.None));
            var list = owner.ListAsync();
            yield return Completion(list);
            Assert.That(list.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            var invalid = owner.CreateAsync("No calendar", "2027-06-15", "2027-06-22");
            yield return Completion(invalid);
            Assert.That(invalid.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            var count = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM trips"), CancellationToken.None);
            yield return Wait(count);
            Assert.That(count.Result, Is.Zero);
        }

#endregion
#region Ready
        private IEnumerator Ready()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                foreach (string id in new[]
                {
                    "owner",
                    "other"
                }

                )
                    connection.Execute(
                        "INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                        id, id + "@example.com", "testfixture", "testfixture", 600000, "2026-09-29T00:00:00Z");
                return 0;
            }, CancellationToken.None));
        }

#endregion
#region OpenDatabase
        private TravelDatabase OpenDatabase() => new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
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
#region Failure
        private static IEnumerator Failure(Task task)
        {
            yield return Completion(task);
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
        }

#endregion
#region Completion
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Trip operation timed out.");
                yield return null;
            }
        }
#endregion
    }
}
