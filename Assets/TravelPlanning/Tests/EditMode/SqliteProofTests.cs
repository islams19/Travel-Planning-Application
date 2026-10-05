using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class SqliteProofTests
    {
        private string directory;
        private string path;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "TravelPlannerSqliteTests", Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "proof.db");
        }

#endregion
#region TearDown
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

#endregion
#region SavedRowSurvivesNewServiceAndRunsOnWorker
        [UnityTest]
        public IEnumerator SavedRowSurvivesNewServiceAndRunsOnWorker()
        {
            int callerThread = Thread.CurrentThread.ManagedThreadId;
            var firstTask = new SqliteProofDatabase().RunAsync(path, CancellationToken.None);
            yield return Complete(firstTask);
            var first = firstTask.GetAwaiter().GetResult();
            var secondTask = new SqliteProofDatabase().RunAsync(path, CancellationToken.None);
            yield return Complete(secondTask);
            var second = secondTask.GetAwaiter().GetResult();
            Assert.That(first.WasAlreadySaved, Is.False);
            Assert.That(second.WasAlreadySaved, Is.True);
            Assert.That(second.Visits, Is.EqualTo(2));
            Assert.That(second.CreatedUtc, Is.EqualTo(first.CreatedUtc));
            Assert.That(second.Message, Is.EqualTo(SqliteProofDatabase.SampleMessage));
            Assert.That(first.WorkerThreadId, Is.Not.EqualTo(callerThread));
        }

#endregion
#region ConcurrentRequestsKeepOneRowAndAllVisits
        [UnityTest]
        public IEnumerator ConcurrentRequestsKeepOneRowAndAllVisits()
        {
            var service = new SqliteProofDatabase();
            var pending = Task.WhenAll(Enumerable.Range(0, 4).Select(_ => service.RunAsync(path, CancellationToken.None)));
            yield return Complete(pending);
            var results = pending.GetAwaiter().GetResult();
            Assert.That(results.Select(row => row.Visits).OrderBy(value => value), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(results.Count(row => !row.WasAlreadySaved), Is.EqualTo(1));
        }

#endregion
#region CorruptDatabaseIsReportedWithoutReplacingIt
        [UnityTest]
        public IEnumerator CorruptDatabaseIsReportedWithoutReplacingIt()
        {
            Directory.CreateDirectory(directory);
            byte[] original = Enumerable.Repeat((byte)0xAB, 1024).ToArray();
            File.WriteAllBytes(path, original);
            var pending = new SqliteProofDatabase().RunAsync(path, CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.Exception?.GetBaseException(), Is.InstanceOf<SQLite.SQLiteException>());
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

#endregion
#region FileWhereDirectoryShouldBeIsReported
        [UnityTest]
        public IEnumerator FileWhereDirectoryShouldBeIsReported()
        {
            Directory.CreateDirectory(directory);
            string blocker = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blocker, "Keep this file.");
            var pending = new SqliteProofDatabase().RunAsync(Path.Combine(blocker, "proof.db"), CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.Exception?.GetBaseException(), Is.InstanceOf<IOException>());
            Assert.That(File.ReadAllText(blocker), Is.EqualTo("Keep this file."));
        }

#endregion
#region CancellationDoesNotCreateDatabase
        [Test]
        public void CancellationDoesNotCreateDatabase()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var pending = new SqliteProofDatabase().RunAsync(path, cancellation.Token);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(File.Exists(path), Is.False);
            }
        }

#endregion
#region Complete
        private static IEnumerator Complete(Task pending)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!pending.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(pending.IsCompleted, Is.True, "Database worker did not finish within 15 seconds.");
        }
#endregion
    }
}
