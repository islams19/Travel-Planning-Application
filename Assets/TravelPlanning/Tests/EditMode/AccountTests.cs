using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Accounts;
using TravelPlanning.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    /// <summary>Exercises real SQLite and PBKDF2 against temporary databases, never user accounts.</summary>
    public sealed class AccountTests
    {
        private const string Password = " My travel password! ";
        private string folder;
        private string path;
        private TravelDatabase database;
        private AccountService service;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelAccountTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = NewDatabase();
            service = new AccountService(new AccountDatabase(database));
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
#region AccountsSurviveReopenAndLogoutClearsIdentity
        [UnityTest]
        public IEnumerator AccountsSurviveReopenAndLogoutClearsIdentity()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var registration = service.RegisterAsync(" Duy@Example.com ", Password, Password);
            yield return Wait(registration);
            Assert.That(registration.Result.Success, Is.True);
            Assert.That(registration.Result.UserId, Is.Not.Empty);
            Assert.That(service.SignedInEmail, Is.Null);
            yield return Wait(database.ExecuteAsync(connection => connection.Execute(
                "INSERT INTO trips (id, user_id, name, start_date, end_date, created_utc, updated_utc) VALUES (?, ?, ?, ?, ?, ?, ?)",
                "preserved-trip", registration.Result.UserId, "My saved trip", "2027-06-15", "2027-06-22", "2026-09-29T00:00:00Z",
                "2026-09-29T00:00:00Z"), CancellationToken.None));
            var reopened = NewDatabase();
            yield return Wait(reopened.InitializeAsync(CancellationToken.None));
            var second = new AccountService(new AccountDatabase(reopened));
            var login = second.LoginAsync("DUY@example.com", Password);
            yield return Wait(login);
            Assert.That(login.Result.Success, Is.True);
            Assert.That(second.SignedInEmail, Is.EqualTo("duy@example.com"));
            Assert.That(second.SignedInUserId, Is.EqualTo(registration.Result.UserId));
            var tripName = reopened.ExecuteAsync(connection => connection.ExecuteScalar<string>("SELECT name FROM trips WHERE id = ? AND user_id = ?",
                "preserved-trip", second.SignedInUserId), CancellationToken.None);
            yield return Wait(tripName);
            Assert.That(tripName.Result, Is.EqualTo("My saved trip"));
            second.Logout();
            Assert.That(second.SignedInEmail, Is.Null);
            Assert.That(second.SignedInUserId, Is.Null);
        }

#endregion
#region EqualPasswordsHaveDifferentSaltsAndHashesWithoutPlaintextStorage
        [UnityTest]
        public IEnumerator EqualPasswordsHaveDifferentSaltsAndHashesWithoutPlaintextStorage()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var registrations = Task.WhenAll(service.RegisterAsync("first@example.com", Password, Password), service.RegisterAsync("second@example.com", Password, Password));
            yield return Wait(registrations);
            Assert.That(registrations.Result.All(result => result.Success), Is.True);
            var rows = database.ExecuteAsync(connection => connection.Query<AccountRecord>("SELECT * FROM users ORDER BY email_normalized"), CancellationToken.None);
            yield return Wait(rows);
            Assert.That(rows.Result.Count, Is.EqualTo(2));
            Assert.That(rows.Result[0].Salt, Is.Not.EqualTo(rows.Result[1].Salt));
            Assert.That(rows.Result[0].Hash, Is.Not.EqualTo(rows.Result[1].Hash));
            foreach (var row in rows.Result)
            {
                Assert.That(row.Hash, Is.Not.EqualTo(Password));
                Assert.That(Convert.FromBase64String(row.Hash).Length, Is.EqualTo(32));
                Assert.That(Convert.FromBase64String(row.Salt).Length, Is.EqualTo(16));
                Assert.That(row.Iterations, Is.EqualTo(600000));
                Assert.That(row.Algorithm, Is.EqualTo("PBKDF2-SHA256"));
            }

            Assert.That(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)), Does.Not.Contain(Password));
        }

#endregion
#region ConcurrentDuplicateCannotReplaceWinningPassword
        [UnityTest]
        public IEnumerator ConcurrentDuplicateCannotReplaceWinningPassword()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            const string otherPassword = "A different password!";
            var first = service.RegisterAsync("same@example.com", Password, Password);
            var second = service.RegisterAsync(" SAME@example.com ", otherPassword, otherPassword);
            yield return Wait(Task.WhenAll(first, second));
            Assert.That(first.Result.Success ^ second.Result.Success, Is.True);
            var winner = service.LoginAsync("same@example.com", first.Result.Success ? Password : otherPassword);
            yield return Wait(winner);
            Assert.That(winner.Result.Success, Is.True);
            var loser = service.LoginAsync("same@example.com", first.Result.Success ? otherPassword : Password);
            yield return Wait(loser);
            Assert.That(loser.Result.Success, Is.False);
        }

#endregion
#region InvalidFormsDoNotWriteAccounts
        [UnityTest]
        public IEnumerator InvalidFormsDoNotWriteAccounts()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (string email in new[]
            {
                null,
                "",
                "not-email",
                "Name <name@example.com>",
                "a@example.com\n",
                new string ('a', 250) + "@example.com"
            }

            )
            {
                var result = service.RegisterAsync(email, Password, Password);
                yield return Wait(result);
                Assert.That(result.Result.Success, Is.False);
            }

            foreach (string password in new[]
            {
                null,
                "",
                new string ('x', 9),
                new string (' ', 10),
                new string ('x', 129)
            }

            )
            {
                var result = service.RegisterAsync("test@example.com", password, password);
                yield return Wait(result);
                Assert.That(result.Result.Success, Is.False);
            }

            var mismatch = service.RegisterAsync("test@example.com", Password, Password.Trim());
            yield return Wait(mismatch);
            Assert.That(mismatch.Result.Success, Is.False);
            var count = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users"), CancellationToken.None);
            yield return Wait(count);
            Assert.That(count.Result, Is.Zero);
        }

#endregion
#region PasswordLengthsTenAnd128CanRegisterAndLogin
        [UnityTest]
        public IEnumerator PasswordLengthsTenAnd128CanRegisterAndLogin()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (int length in new[]
            {
                10,
                128
            }

            )
            {
                string password = new string ('x', length);
                string email = "boundary" + length + "@example.com";
                var registration = service.RegisterAsync(email, password, password);
                yield return Wait(registration);
                Assert.That(registration.Result.Success, Is.True, "Registration length " + length);
                var login = service.LoginAsync(email, password);
                yield return Wait(login);
                Assert.That(login.Result.Success, Is.True, "Login length " + length);
                service.Logout();
            }
        }

#endregion
#region FailedLoginClearsSessionAndUsesGenericMessage
        [UnityTest]
        public IEnumerator FailedLoginClearsSessionAndUsesGenericMessage()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            var good = service.LoginAsync("test@example.com", Password);
            yield return Wait(good);
            Assert.That(good.Result.Success, Is.True);
            var wrong = service.LoginAsync("test@example.com", "A wrong password!");
            yield return Wait(wrong);
            Assert.That(wrong.Result.Success, Is.False);
            Assert.That(service.SignedInUserId, Is.Null);
            var missing = service.LoginAsync("missing@example.com", Password);
            yield return Wait(missing);
            Assert.That(missing.Result.Success, Is.False);
            Assert.That(missing.Result.Message, Is.EqualTo(wrong.Result.Message));
            Assert.That(missing.Result.Email, Is.Null);
        }

#endregion
#region PasswordComparisonPreservesCaseAndSpaces
        [UnityTest]
        public IEnumerator PasswordComparisonPreservesCaseAndSpaces()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            foreach (string altered in new[]
            {
                Password.Trim(),
                Password.ToLowerInvariant()
            }

            )
            {
                var bad = service.LoginAsync("test@example.com", altered);
                yield return Wait(bad);
                Assert.That(bad.Result.Success, Is.False);
            }

            var good = service.LoginAsync("test@example.com", Password);
            yield return Wait(good);
            Assert.That(good.Result.Success, Is.True);
        }

#endregion
#region SqlTextInEmailIsStoredAsData
        [UnityTest]
        public IEnumerator SqlTextInEmailIsStoredAsData()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            const string email = "o'brien@example.com";
            var registration = service.RegisterAsync(email, Password, Password);
            yield return Wait(registration);
            Assert.That(registration.Result.Success, Is.True);
            var attack = service.LoginAsync("' OR 1=1 --@example.com", Password);
            yield return Wait(attack);
            Assert.That(attack.Result.Success, Is.False);
            var login = service.LoginAsync(email, Password);
            yield return Wait(login);
            Assert.That(login.Result.Success, Is.True);
        }

#endregion
#region MalformedCredentialMetadataFailsClosed
        [UnityTest]
        public IEnumerator MalformedCredentialMetadataFailsClosed()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            var originals = database.ExecuteAsync(connection => connection.FindWithQuery<AccountRecord>("SELECT * FROM users LIMIT 1"), CancellationToken.None);
            yield return Wait(originals);
            var original = originals.Result;
            // Each malformed row must be rejected before expensive work with an attacker-selected cost.
            object[][] badValues =
            {
                new object[]
                {
                    "UNKNOWN",
                    600000,
                    original.Salt,
                    original.Hash
                },
                new object[]
                {
                    "PBKDF2-SHA256",
                    1,
                    original.Salt,
                    original.Hash
                },
                new object[]
                {
                    "PBKDF2-SHA256",
                    int.MaxValue,
                    original.Salt,
                    original.Hash
                },
                new object[]
                {
                    "PBKDF2-SHA256",
                    600000,
                    new string ('!', 24),
                    original.Hash
                },
                new object[]
                {
                    "PBKDF2-SHA256",
                    600000,
                    original.Salt,
                    new string ('!', 44)
                },
                new object[]
                {
                    "PBKDF2-SHA256",
                    600000,
                    "AA==",
                    original.Hash
                }
            };
            foreach (var values in badValues)
            {
                yield return Wait(database.ExecuteAsync(connection => connection.Execute(
                    "UPDATE users SET password_algorithm = ?, password_iterations = ?, password_salt = ?, password_hash = ?", values), CancellationToken.None));
                var login = service.LoginAsync("test@example.com", Password);
                yield return Wait(login);
                Assert.That(login.Result.Success, Is.False);
                Assert.That(login.Result.Message, Is.EqualTo("Email or password is incorrect."));
            }
        }

#endregion
#region LogoutWhileLoginIsPendingCannotRestoreSession
        [UnityTest]
        public IEnumerator LogoutWhileLoginIsPendingCannotRestoreSession()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            {
                var blocker = database.ExecuteAsync(connection =>
                {
                    entered.Set();
                    return release.Wait(TimeSpan.FromSeconds(30));
                }, CancellationToken.None);
                while (!entered.IsSet)
                    yield return null;
                Task<AccountResult> login;
                try
                {
                    login = service.LoginAsync("test@example.com", Password);
                    service.Logout();
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                yield return Wait(login);
                Assert.That(login.Result.Success, Is.False);
                Assert.That(service.SignedInUserId, Is.Null);
            }
        }

#endregion
#region CancellationPreventsRegistrationAndLoginSession
        [UnityTest]
        public IEnumerator CancellationPreventsRegistrationAndLoginSession()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection =>
                {
                    entered.Set();
                    return release.Wait(TimeSpan.FromSeconds(30));
                }, CancellationToken.None);
                while (!entered.IsSet)
                    yield return null;
                Task<AccountResult> pending;
                try
                {
                    pending = service.RegisterAsync("cancelled@example.com", Password, Password, cancellation.Token);
                    cancellation.Cancel();
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                yield return WaitCompletion(pending);
                Assert.That(pending.IsCanceled, Is.True);
            }

            var count = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users"), CancellationToken.None);
            yield return Wait(count);
            Assert.That(count.Result, Is.Zero);
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection =>
                {
                    entered.Set();
                    return release.Wait(TimeSpan.FromSeconds(30));
                }, CancellationToken.None);
                while (!entered.IsSet)
                    yield return null;
                Task<AccountResult> pending;
                try
                {
                    pending = service.LoginAsync("test@example.com", Password, cancellation.Token);
                    cancellation.Cancel();
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                yield return WaitCompletion(pending);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(service.SignedInUserId, Is.Null);
            }
        }

#endregion
#region LegacyAccountFileIsNeverReadOrModified
        [UnityTest]
        public IEnumerator LegacyAccountFileIsNeverReadOrModified()
        {
            string legacy = Path.Combine(folder, "accounts.db");
            byte[] original =
            {
                7,
                9,
                12,
                34,
                99
            };
            File.WriteAllBytes(legacy, original);
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            Assert.That(File.ReadAllBytes(legacy), Is.EqualTo(original));
        }

#endregion
#region NewDatabase
        private TravelDatabase NewDatabase() => new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
#endregion
#region Wait
        private static IEnumerator Wait(Task task)
        {
            yield return WaitCompletion(task);
            task.GetAwaiter().GetResult(); // Safe only after the task finished; never block Unity's main thread.
        }

#endregion
#region WaitCompletion
        private static IEnumerator WaitCompletion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 120;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Account operation timed out.");
                yield return null;
            }
        }
#endregion
    }
}
