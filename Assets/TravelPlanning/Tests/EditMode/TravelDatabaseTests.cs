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
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TravelDatabaseTests
    {
        private string folder;
        private string seed;
        private string writable;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            SqliteRuntime.Initialize();
            folder = Path.Combine(Path.GetTempPath(), "TravelDatabaseTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            seed = Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db");
            writable = Path.Combine(folder, "travel.db");
            Assert.That(File.Exists(seed), Is.True, "Build the seed database before running these tests.");
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
#region FirstCopyAndReopenPreserveEditedUserTrip
        [UnityTest]
        public IEnumerator FirstCopyAndReopenPreserveEditedUserTrip()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var service = new TravelDatabase(seed, writable);
            var firstTask = service.InitializeAsync(CancellationToken.None);
            yield return Complete(firstTask);
            var first = firstTask.GetAwaiter().GetResult();
            Assert.That(first.CopiedSeed, Is.True);
            Assert.That(first.Destinations, Is.EqualTo(12));
            Assert.That(first.Users, Is.Zero);
            Assert.That(first.WorkerThreadId, Is.Not.EqualTo(mainThread));
            var write = service.ExecuteAsync(connection =>
            {
                AddUserAndTrip(connection);
                return true;
            }, CancellationToken.None);
            yield return Complete(write);
            write.GetAwaiter().GetResult();
            byte[] before = Hash(writable);
            // Existing installations do not depend on the shipped seed still being present.
            var reopen = new TravelDatabase(Path.Combine(folder, "missing-seed.db"), writable).InitializeAsync(CancellationToken.None);
            yield return Complete(reopen);
            var second = reopen.GetAwaiter().GetResult();
            Assert.That(second.CopiedSeed, Is.False);
            Assert.That(second.Users, Is.EqualTo(1));
            Assert.That(second.Trips, Is.EqualTo(1));
            Assert.That(Hash(writable), Is.EqualTo(before), "Startup must not rewrite an existing database.");
        }

#endregion
#region ConcurrentFirstLaunchCopiesOnlyOnce
        [UnityTest]
        public IEnumerator ConcurrentFirstLaunchCopiesOnlyOnce()
        {
            var pending = Task.WhenAll(new TravelDatabase(seed, writable).InitializeAsync(CancellationToken.None), new TravelDatabase(seed,
                writable).InitializeAsync(CancellationToken.None));
            yield return Complete(pending);
            Assert.That(pending.GetAwaiter().GetResult().Count(result => result.CopiedSeed), Is.EqualTo(1));
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }

#endregion
#region MissingSeedFailsWithoutCreatingTarget
        [UnityTest]
        public IEnumerator MissingSeedFailsWithoutCreatingTarget()
        {
            var pending = new TravelDatabase(Path.Combine(folder, "absent.db"), writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.Exception?.GetBaseException(), Is.InstanceOf<FileNotFoundException>());
            Assert.That(File.Exists(writable), Is.False);
        }

#endregion
#region CorruptSeedNeverBecomesWritableDatabase
        [UnityTest]
        public IEnumerator CorruptSeedNeverBecomesWritableDatabase()
        {
            string corrupt = Path.Combine(folder, "bad-seed.db");
            File.WriteAllBytes(corrupt, Enumerable.Repeat((byte)171, 1024).ToArray());
            var pending = new TravelDatabase(corrupt, writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.IsFaulted, Is.True);
            Assert.That(File.Exists(writable), Is.False);
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }

#endregion
#region CorruptExistingDatabaseIsPreserved
        [UnityTest]
        public IEnumerator CorruptExistingDatabaseIsPreserved()
        {
            File.WriteAllBytes(writable, Enumerable.Repeat((byte)171, 1024).ToArray());
            byte[] original = Hash(writable);
            var pending = new TravelDatabase(seed, writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.IsFaulted, Is.True);
            Assert.That(Hash(writable), Is.EqualTo(original));
        }

#endregion
#region NewerSchemaIsRejectedWithoutChanges
        [UnityTest]
        public IEnumerator NewerSchemaIsRejectedWithoutChanges()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable))
                connection.Execute("PRAGMA user_version = 999");
            byte[] original = Hash(writable);
            var pending = new TravelDatabase(seed, writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.IsFaulted, Is.True);
            Assert.That(Hash(writable), Is.EqualTo(original));
        }

#endregion
#region SeedHasAllDestinationsAndValidRelationships
        [Test]
        public void SeedHasAllDestinationsAndValidRelationships()
        {
            using (var connection = new SQLiteConnection(seed, SQLiteOpenFlags.ReadOnly))
            {
                connection.Execute("PRAGMA foreign_keys = ON");
                TravelDatabaseSchema.Validate(connection);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM destinations"), Is.EqualTo(12));
                foreach (string table in new[]
                {
                    "hotels",
                    "restaurants",
                    "experiences",
                    "hotspots"
                }

                )
                {
                    // Table names here are trusted constants, not user input.
                    Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(DISTINCT destination_id) FROM " + table), Is.EqualTo(12));
                    Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM " + table), Is.GreaterThanOrEqualTo(36));
                }

                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM reviews WHERE is_demo <> 1"), Is.Zero);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM flights WHERE departure_local_date NOT BETWEEN ? AND ?", "2027-06-01", "2027-06-30"), Is.Zero);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM flights WHERE departure_local_date = ?", "2027-06-15"), Is.GreaterThan(0));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM flights WHERE departure_local_date = ?", "2027-06-22"), Is.GreaterThan(0));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users"), Is.Zero);
            }
        }

#endregion
#region DatabaseConstraintsRejectBadPricesRatingsAndBrokenReferences
        [Test]
        public void DatabaseConstraintsRejectBadPricesRatingsAndBrokenReferences()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable))
            {
                connection.Execute("PRAGMA foreign_keys = ON");
                string hotel = connection.ExecuteScalar<string>("SELECT id FROM hotels LIMIT 1");
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE hotels SET price_cents = ? WHERE id = ?", -1, hotel));
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE hotels SET destination_id = ? WHERE id = ?", "missing-city", hotel));
                string review = connection.ExecuteScalar<string>("SELECT id FROM reviews LIMIT 1");
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE reviews SET rating = ? WHERE id = ?", 6, review));
                AddUserAndTrip(connection);
                Assert.Throws<SQLiteException>(() => connection.Execute("INSERT INTO saved_items (id,user_id,trip_id,created_utc) VALUES (?,?,?,?)",
                    "invalid", "test-user", "test-trip", "2027-01-01T00:00:00Z"));
                connection.Execute("INSERT INTO saved_items (id,user_id,trip_id,hotel_id,created_utc) VALUES (?,?,?,?,?)", "saved-one", "test-user",
                    "test-trip", hotel, "2027-01-01T00:00:00Z");
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO saved_items (id,user_id,trip_id,hotel_id,created_utc) VALUES (?,?,?,?,?)", "saved-duplicate", "test-user",
                    "test-trip", hotel, "2027-01-01T00:00:00Z"));
            }
        }

#endregion
#region OwnershipAndNotificationTargetsAreEnforced
        [Test]
        public void OwnershipAndNotificationTargetsAreEnforced()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable))
            {
                connection.Execute("PRAGMA foreign_keys = ON");
                AddUserAndTrip(connection);
                string hotel = connection.ExecuteScalar<string>("SELECT id FROM hotels LIMIT 1");
                string otherHotel = connection.ExecuteScalar<string>("SELECT id FROM hotels WHERE id<>? LIMIT 1", hotel);
                const string now = "2027-01-01T00:00:00Z";
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO saved_items (id,user_id,trip_id,hotel_id,created_utc) VALUES (?,?,?,?,?)", "wrong-owner", "other-user", "test-trip", hotel, now));
                connection.Execute("INSERT INTO price_watches (id,user_id,hotel_id,last_price_cents,created_utc) VALUES (?,?,?,?,?)", "watch", "test-user", hotel, 10000, now);
                connection.Execute("INSERT INTO price_history (id,hotel_id,old_price_cents,new_price_cents,changed_utc) VALUES (?,?,?,?,?)",
                    "wrong-history", otherHotel, 10000, 9000, now);
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO notifications (id,user_id,watch_id,price_history_id,title,body,created_utc) VALUES (?,?,?,?,?,?,?)", "wrong-notice",
                    "test-user", "watch", "wrong-history", "Price changed", "Demo", now));
                connection.Execute("INSERT INTO price_history (id,hotel_id,old_price_cents,new_price_cents,changed_utc) VALUES (?,?,?,?,?)", "history", hotel, 10000, 9000, now);
                connection.Execute("INSERT INTO notifications (id,user_id,watch_id,price_history_id,title,body,created_utc) VALUES (?,?,?,?,?,?,?)",
                    "notice", "test-user", "watch", "history", "Price changed", "Demo", now);
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE price_watches SET hotel_id=? WHERE id=?", otherHotel, "watch"));
                connection.Execute("DELETE FROM users WHERE id=?", "test-user");
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM notifications"), Is.Zero);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM trips"), Is.Zero);
            }
        }

#endregion
#region MissingSearchIndexIsRejected
        [Test]
        public void MissingSearchIndexIsRejected()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable))
            {
                connection.Execute("DROP INDEX flights_search");
                Assert.Throws<InvalidOperationException>(() => TravelDatabaseSchema.Validate(connection));
            }
        }

#endregion
#region CancelledStartupDoesNotCopySeed
        [Test]
        public void CancelledStartupDoesNotCopySeed()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var pending = new TravelDatabase(seed, writable).InitializeAsync(cancellation.Token);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(File.Exists(writable), Is.False);
            }
        }

#endregion
#region AddUserAndTrip
        private static void AddUserAndTrip(SQLiteConnection connection)
        {
            connection.RunInTransaction(() =>
            {
                connection.Execute(
                    "INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                    "test-user", "test@example.invalid", Convert.ToBase64String(new byte[32]), Convert.ToBase64String(new byte[16]), 600000, "2027-01-01T00:00:00Z");
                connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)",
                    "test-trip", "test-user", "Trip", "2027-06-15", "2027-06-22", "2027-01-01T00:00:00Z", "2027-01-01T00:00:00Z");
                connection.Execute("UPDATE trips SET name = ? WHERE id = ?", "Edited trip", "test-trip");
            });
        }

#endregion
#region Hash
        private static byte[] Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
                return algorithm.ComputeHash(stream);
        }

#endregion
#region Complete
        private static IEnumerator Complete(Task task)
        {
            float end = Time.realtimeSinceStartup + 20;
            while (!task.IsCompleted && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, "Database worker exceeded 20 seconds.");
        }
#endregion
    }
}
