using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SQLite;
using TravelPlanning.Data;
using TravelPlanning.Tracking;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class PriceTrackingTests
    {
        private const string Flight = "JFK-LHR-20270615-AA";
        private const string Hotel = "london-hotel-1";
        private string folder;
        private string path;
        private TravelDatabase database;
        private PriceTrackingService owner;
        private PriceTrackingService other;
        private DemoPriceService prices;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelTrackingTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = OpenDatabase();
            owner = new PriceTrackingService(database, "owner");
            other = new PriceTrackingService(database, "other");
            prices = new DemoPriceService(database);
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
#region BothKindsHaveFreshPreviewAndConcurrentTrackingCreatesOneWatch
        [UnityTest]
        public IEnumerator BothKindsHaveFreshPreviewAndConcurrentTrackingCreatesOneWatch()
        {
            yield return Ready();
            var preview = owner.GetTargetAsync(PriceTargetKind.Flight, Flight);
            yield return Wait(preview);
            Assert.That(preview.Result.Id, Is.Null);
            Assert.That(preview.Result.IsActive, Is.False);
            Assert.That(preview.Result.CurrentPriceCents, Is.EqualTo(56200));
            Assert.That(preview.Result.LastPriceCents, Is.EqualTo(56200));
            Assert.That(preview.Result.Title, Does.Contain("AA101"));
            Assert.That(preview.Result.Title, Does.Contain("JFK to LHR"));
            Assert.That(preview.Result.Title, Does.Contain("2027-06-15"));
            var rival = new PriceTrackingService(database, "owner");
            var race = Task.WhenAll(owner.TrackAsync(PriceTargetKind.Flight, Flight), rival.TrackAsync(PriceTargetKind.Flight, Flight));
            yield return Wait(race);
            Assert.That(race.Result.Count(created => created), Is.EqualTo(1));
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            int main = Thread.CurrentThread.ManagedThreadId;
            var snapshot = owner.LoadAsync();
            yield return Wait(snapshot);
            Assert.That(snapshot.Result.Watches.Count, Is.EqualTo(2));
            Assert.That(snapshot.Result.Watches.All(watch => watch.IsActive && watch.Currency == "USD"), Is.True);
            Assert.That(snapshot.Result.Watches.Single(watch => watch.Kind == PriceTargetKind.Flight).PriceUnit, Is.EqualTo("/ flight"));
            Assert.That(snapshot.Result.Watches.Single(watch => watch.Kind == PriceTargetKind.Hotel).PriceUnit, Is.EqualTo("/ room / night"));
            Assert.That(snapshot.Result.Watches.Single(watch => watch.Kind == PriceTargetKind.Hotel).Title, Does.Contain("London"));
            Assert.That(snapshot.Result.WorkerThreadId, Is.Not.EqualTo(main));
            Assert.That(snapshot.Result.Notifications, Is.Empty);
            byte[] before = Hash();
            var duplicate = owner.TrackAsync(PriceTargetKind.Hotel, Hotel);
            yield return Wait(duplicate);
            Assert.That(duplicate.Result, Is.False);
            Assert.That(Hash(), Is.EqualTo(before));
        }

#endregion
#region IncreasesDecreasesAndUnwatchedChangesRecordHistoryButEqualPriceDoesNothing
        [UnityTest]
        public IEnumerator IncreasesDecreasesAndUnwatchedChangesRecordHistoryButEqualPriceDoesNothing()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Flight, Flight));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 60000));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 50000));
            byte[] before = Hash();
            var unchanged = prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 50000);
            yield return Wait(unchanged);
            Assert.That(unchanged.Result, Is.False);
            Assert.That(Hash(), Is.EqualTo(before));
            var snapshot = owner.LoadAsync();
            yield return Wait(snapshot);
            Assert.That(snapshot.Result.UnreadCount, Is.EqualTo(2));
            Assert.That(snapshot.Result.Notifications[0].Title, Does.Contain("decreased"));
            Assert.That(snapshot.Result.Notifications[0].Body, Does.Contain("USD 600.00 to USD 500.00 / flight"));
            Assert.That(snapshot.Result.Notifications[0].OldPriceCents, Is.EqualTo(60000));
            Assert.That(snapshot.Result.Notifications[0].NewPriceCents, Is.EqualTo(50000));
            Assert.That(snapshot.Result.Notifications[1].Title, Does.Contain("increased"));
            Assert.That(snapshot.Result.Notifications.All(notice => notice.Body.Contains("2027-06-15")), Is.True);
            Assert.That(snapshot.Result.Watches.Single().LastPriceCents, Is.EqualTo(50000));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 15000));
            var historyCount = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM price_history"), CancellationToken.None);
            yield return Wait(historyCount);
            Assert.That(historyCount.Result, Is.EqualTo(3));
            var unwatched = owner.GetTargetAsync(PriceTargetKind.Hotel, Hotel);
            yield return Wait(unwatched);
            Assert.That(unwatched.Result.Id, Is.Null);
            Assert.That(unwatched.Result.CurrentPriceCents, Is.EqualTo(15000));
            var count = owner.GetUnreadCountAsync();
            yield return Wait(count);
            Assert.That(count.Result, Is.EqualTo(2));
        }

#endregion
#region OneChangeFansOutToBothOwnersAndReadOperationsRemainPrivate
        [UnityTest]
        public IEnumerator OneChangeFansOutToBothOwnersAndReadOperationsRemainPrivate()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(other.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 17000));
            var mine = owner.LoadAsync();
            var theirs = other.LoadAsync();
            yield return Wait(mine);
            yield return Wait(theirs);
            var mineNotice = mine.Result.Notifications.Single();
            var otherNotice = theirs.Result.Notifications.Single();
            Assert.That(mineNotice.Id, Is.Not.EqualTo(otherNotice.Id));
            Assert.That(mineNotice.HistoryId, Is.EqualTo(otherNotice.HistoryId));
            Assert.That(mine.Result.Watches.Single().LastPriceCents, Is.EqualTo(17000));
            Assert.That(theirs.Result.Watches.Single().LastPriceCents, Is.EqualTo(17000));
            var foreignRead = owner.MarkReadAsync(otherNotice.Id);
            var missingRead = owner.MarkReadAsync("missing");
            var foreignStop = owner.StopAsync(theirs.Result.Watches.Single().Id);
            var missingStop = owner.StopAsync("missing");
            yield return Wait(foreignRead);
            yield return Wait(missingRead);
            yield return Wait(foreignStop);
            yield return Wait(missingStop);
            Assert.That(foreignRead.Result, Is.False);
            Assert.That(missingRead.Result, Is.False);
            Assert.That(foreignStop.Result, Is.False);
            Assert.That(missingStop.Result, Is.False);
            var read = owner.MarkReadAsync(mineNotice.Id);
            yield return Wait(read);
            Assert.That(read.Result, Is.True);
            var again = owner.MarkReadAsync(mineNotice.Id);
            yield return Wait(again);
            Assert.That(again.Result, Is.False);
            var otherUnread = other.GetUnreadCountAsync();
            yield return Wait(otherUnread);
            Assert.That(otherUnread.Result, Is.EqualTo(1));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 18000));
            var markAll = owner.MarkAllReadAsync();
            yield return Wait(markAll);
            Assert.That(markAll.Result, Is.EqualTo(1));
            var mineCount = owner.GetUnreadCountAsync();
            otherUnread = other.GetUnreadCountAsync();
            yield return Wait(mineCount);
            yield return Wait(otherUnread);
            Assert.That(mineCount.Result, Is.Zero);
            Assert.That(otherUnread.Result, Is.EqualTo(2));
            var historyCount = database.ExecuteAsync(connection => connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM price_history WHERE hotel_id=?", Hotel), CancellationToken.None);
            yield return Wait(historyCount);
            Assert.That(historyCount.Result, Is.EqualTo(2));
        }

#endregion
#region StoppingKeepsNoticesAndRetrackingResetsBaselineWithoutHistoricalAlert
        [UnityTest]
        public IEnumerator StoppingKeepsNoticesAndRetrackingResetsBaselineWithoutHistoricalAlert()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 17000));
            var first = owner.LoadAsync();
            yield return Wait(first);
            string watchId = first.Result.Watches.Single().Id;
            var stop = owner.StopAsync(watchId);
            yield return Wait(stop);
            Assert.That(stop.Result, Is.True);
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 18000));
            var stopped = owner.LoadAsync();
            yield return Wait(stopped);
            Assert.That(stopped.Result.Watches.Single().IsActive, Is.False);
            Assert.That(stopped.Result.Watches.Single().LastPriceCents, Is.EqualTo(17000));
            Assert.That(stopped.Result.Watches.Single().CurrentPriceCents, Is.EqualTo(18000));
            Assert.That(stopped.Result.Notifications.Single().Id, Is.EqualTo(first.Result.Notifications.Single().Id));
            var resume = owner.TrackAsync(PriceTargetKind.Hotel, Hotel);
            yield return Wait(resume);
            Assert.That(resume.Result, Is.True);
            var active = owner.GetTargetAsync(PriceTargetKind.Hotel, Hotel);
            yield return Wait(active);
            Assert.That(active.Result.Id, Is.EqualTo(watchId));
            Assert.That(active.Result.LastPriceCents, Is.EqualTo(18000));
            var unread = owner.GetUnreadCountAsync();
            yield return Wait(unread);
            Assert.That(unread.Result, Is.EqualTo(1));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 19000));
            var resumed = owner.LoadAsync();
            yield return Wait(resumed);
            Assert.That(resumed.Result.Notifications.Count, Is.EqualTo(2));
            Assert.That(resumed.Result.Notifications[0].OldPriceCents, Is.EqualTo(18000));
        }

#endregion
#region FailureDuringNotificationInsertRollsBackPriceHistoryNoticesAndBaselines
        [UnityTest]
        public IEnumerator FailureDuringNotificationInsertRollsBackPriceHistoryNoticesAndBaselines()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(other.TrackAsync(PriceTargetKind.Hotel, Hotel));
            // Test-only trigger makes notification creation fail after the price/history writes.
            yield return Wait(database.ExecuteAsync(connection => connection.Execute(
                "CREATE TRIGGER test_fail_notice BEFORE INSERT ON notifications " +
                "WHEN NEW.user_id='other' BEGIN SELECT RAISE(ABORT,'Injected test failure'); END"), CancellationToken.None));
            byte[] before = Hash();
            var failed = prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 22000);
            yield return Completion(failed);
            Assert.That(failed.IsFaulted, Is.True);
            Assert.That(failed.Exception.GetBaseException(), Is.InstanceOf<SQLiteException>());
            Assert.That(Hash(), Is.EqualTo(before));
            var snapshot = owner.LoadAsync();
            yield return Wait(snapshot);
            Assert.That(snapshot.Result.Watches.Single().CurrentPriceCents, Is.EqualTo(16000));
            Assert.That(snapshot.Result.Watches.Single().LastPriceCents, Is.EqualTo(16000));
            Assert.That(snapshot.Result.Notifications, Is.Empty);
            var history = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM price_history"), CancellationToken.None);
            yield return Wait(history);
            Assert.That(history.Result, Is.Zero);
        }

#endregion
#region QueuedCancellationLeavesEveryDatabaseByteUnchanged
        [UnityTest]
        public IEnumerator QueuedCancellationLeavesEveryDatabaseByteUnchanged()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 17000));
            var current = owner.LoadAsync();
            yield return Wait(current);
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
                        owner.TrackAsync(PriceTargetKind.Flight, Flight, cancellation.Token),
                        owner.StopAsync(current.Result.Watches.Single().Id, cancellation.Token),
                        owner.MarkReadAsync(current.Result.Notifications.Single().Id, cancellation.Token),
                        owner.MarkAllReadAsync(cancellation.Token),
                        prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 18000, cancellation.Token)
                    };
                    cancellation.Cancel();
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                foreach (var task in pending)
                {
                    yield return Completion(task);
                    Assert.That(task.IsCanceled, Is.True);
                }
            }

            Assert.That(Hash(), Is.EqualTo(before));
        }

#endregion
#region InvalidIdsKindsAndNegativePricesFailWhileZeroAndIntMaximumWork
        [UnityTest]
        public IEnumerator InvalidIdsKindsAndNegativePricesFailWhileZeroAndIntMaximumWork()
        {
            yield return Ready();
            Assert.Throws<ArgumentException>(() => owner.TrackAsync((PriceTargetKind)99, Hotel));
            Assert.Throws<ArgumentException>(() => owner.GetTargetAsync((PriceTargetKind)99, Hotel));
            Assert.Throws<ArgumentException>(() => prices.ChangePriceAsync((PriceTargetKind)99, Hotel, 100));
            Assert.Throws<ArgumentException>(() => prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, -1));
            foreach (string id in new[]
            {
                "missing",
                "london-restaurant-1",
                "london-hotel-1' OR 1=1 --",
                "'; DELETE FROM users; --"
            }

            )
            {
                yield return ArgumentFailure(owner.TrackAsync(PriceTargetKind.Hotel, id));
                yield return ArgumentFailure(owner.GetTargetAsync(PriceTargetKind.Hotel, id));
                yield return ArgumentFailure(prices.ChangePriceAsync(PriceTargetKind.Hotel, id, 100));
            }

            var unknownAccount = new PriceTrackingService(database, "owner' OR 1=1 --");
            var invalidOwner = unknownAccount.TrackAsync(PriceTargetKind.Hotel, Hotel);
            yield return Completion(invalidOwner);
            Assert.That(invalidOwner.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 0));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, int.MaxValue));
            var snapshot = owner.LoadAsync();
            yield return Wait(snapshot);
            Assert.That(snapshot.Result.Watches.Single().CurrentPriceCents, Is.EqualTo(int.MaxValue));
            Assert.That(snapshot.Result.Notifications[0].Body, Does.Contain("USD 0.00 to USD 21474836.47"));
            var injectedRead = owner.MarkReadAsync("' OR 1=1 --");
            var injectedStop = owner.StopAsync("' OR 1=1 --");
            yield return Wait(injectedRead);
            yield return Wait(injectedStop);
            Assert.That(injectedRead.Result, Is.False);
            Assert.That(injectedStop.Result, Is.False);
        }

#endregion
#region ReopenPreservesReadStateAndReadOnlyQueriesPreserveDatabaseBytes
        [UnityTest]
        public IEnumerator ReopenPreservesReadStateAndReadOnlyQueriesPreserveDatabaseBytes()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Flight, Flight));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 56000));
            var initial = owner.LoadAsync();
            yield return Wait(initial);
            yield return Wait(owner.MarkReadAsync(initial.Result.Notifications.Single().Id));
            var reopened = OpenDatabase();
            yield return Wait(reopened.InitializeAsync(CancellationToken.None));
            var service = new PriceTrackingService(reopened, "owner");
            byte[] before = Hash();
            var snapshot = service.LoadAsync();
            var count = service.GetUnreadCountAsync();
            var preview = service.GetTargetAsync(PriceTargetKind.Flight, Flight);
            yield return Wait(snapshot);
            yield return Wait(count);
            yield return Wait(preview);
            Assert.That(snapshot.Result.Notifications.Single().IsRead, Is.True);
            Assert.That(snapshot.Result.Watches.Single().Id, Is.EqualTo(initial.Result.Watches.Single().Id));
            Assert.That(count.Result, Is.Zero);
            Assert.That(preview.Result.CurrentPriceCents, Is.EqualTo(56000));
            Assert.That(Hash(), Is.EqualTo(before));
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
#region ArgumentFailure
        private static IEnumerator ArgumentFailure(Task task)
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
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Tracking operation timed out.");
                yield return null;
            }
        }
#endregion
    }
}
