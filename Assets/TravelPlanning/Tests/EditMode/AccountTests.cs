using System;
using System.IO;
using System.Threading.Tasks;
using LiteDB;
using NUnit.Framework;
using TravelPlanning.Accounts;

namespace TravelPlanning.Tests
{
    /// <summary>Checks the account rules using a disposable database, never real user accounts.</summary>
    public sealed class AccountTests
    {
        private const string Password = "My travel password!";

        [Test]
        public void AccountsSurviveClosingAndReopeningTheDatabase()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    Assert.That(service.Register(" Duy@Example.com ", Password, Password).Success, Is.True);
                    Assert.That(service.SignedInEmail, Is.Null);
                }
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    Assert.That(service.Login("DUY@example.com", Password).Success, Is.True);
                    Assert.That(service.SignedInEmail, Is.EqualTo("duy@example.com"));
                    service.Logout();
                    Assert.That(service.SignedInEmail, Is.Null);
                    Assert.That(service.Register("duy@example.com", Password, Password).Success, Is.False);
                }
            });
        }

        [Test]
        public void DuplicateEmailDoesNotReplaceOriginalPassword()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    Assert.That(service.Register("duy@example.com", Password, Password).Success, Is.True);
                    Assert.That(service.Register(" DUY@example.com ", "Another password", "Another password").Success, Is.False);
                    Assert.That(service.Login("duy@example.com", "Another password").Success, Is.False);
                    Assert.That(service.Login("duy@example.com", Password).Success, Is.True);
                }
            });
        }

        [Test]
        public void SimultaneousRegistrationsCannotReuseEmail()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                {
                    var first = Task.Run(() => new AccountService(database).Register("duy@example.com", Password, Password));
                    var second = Task.Run(() => new AccountService(database).Register("DUY@example.com", Password, Password));
                    Task.WaitAll(first, second);
                    Assert.That(first.Result.Success ^ second.Result.Success, Is.True);
                }
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-an-email")]
        [TestCase("Duy <duy@example.com>")]
        public void InvalidEmailCannotRegister(string email)
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                    Assert.That(new AccountService(database).Register(email, Password, Password).Success, Is.False);
            });
        }

        [Test]
        public void InvalidPasswordsAndMismatchedConfirmationCannotRegister()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    foreach (string password in new[] { null, "", "short", "        ", new string('x', 129) })
                        Assert.That(service.Register("duy@example.com", password, password).Success, Is.False);
                    Assert.That(service.Register("duy@example.com", Password, "different").Success, Is.False);
                    Assert.That(service.Login("duy@example.com", Password).Success, Is.False);
                }
            });
        }

        [Test]
        public void FailedLoginClearsSessionAndDoesNotRevealWhetherEmailExists()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    service.Register("duy@example.com", Password, Password);
                    Assert.That(service.Login("duy@example.com", Password).Success, Is.True);
                    var wrong = service.Login("duy@example.com", "wrong");
                    Assert.That(wrong.Success, Is.False);
                    Assert.That(service.SignedInEmail, Is.Null);
                    Assert.That(service.Login("missing@example.com", Password).Message, Is.EqualTo(wrong.Message));
                }
            });
        }

        [Test]
        public void PasswordsAreStoredAsText()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    service.Register("first@example.com", Password, Password);
                    service.Register("second@example.com", Password, Password);
                }
                using (var database = new LiteDatabase(path))
                {
                    var records = database.GetCollection("accounts");
                    var first = records.FindOne(Query.EQ("email", "first@example.com"));
                    var second = records.FindOne(Query.EQ("email", "second@example.com"));
                    Assert.That(first["password"].AsString, Is.EqualTo(Password));
                    Assert.That(second["password"].AsString, Is.EqualTo(Password));
                    Assert.That(first.Keys, Is.EquivalentTo(new[] { "_id", "email", "password" }));
                }
            });
        }

        [Test]
        public void DatabaseEnforcesEmailUniquenessEvenWithoutTheService()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path)) { }
                using (var database = new LiteDatabase(path))
                {
                    var records = database.GetCollection("accounts");
                    records.Insert(new BsonDocument { ["email"] = "duy@example.com" });
                    Assert.Throws<LiteException>(() => records.Insert(new BsonDocument { ["email"] = "duy@example.com" }));
                }
            });
        }

        [Test]
        public void PasswordComparisonPreservesCaseAndSpaces()
        {
            WithDatabase(path =>
            {
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    const string password = " My Password ";
                    Assert.That(service.Register("duy@example.com", password, password).Success, Is.True);
                    Assert.That(service.Login("duy@example.com", password.ToLowerInvariant()).Success, Is.False);
                    Assert.That(service.Login("duy@example.com", password.Trim()).Success, Is.False);
                    Assert.That(service.Login("duy@example.com", password).Success, Is.True);
                }
            });
        }

        [Test]
        public void AccountsWithoutPasswordsCannotLoginOrReuseEmail()
        {
            WithDatabase(path =>
            {
                using (var database = new LiteDatabase(path))
                {
                    database.UserVersion = 1;
                    database.GetCollection("accounts").Insert(new BsonDocument
                    {
                        ["email"] = "old@example.com"
                    });
                }
                using (var database = new AccountDatabase(path))
                {
                    var service = new AccountService(database);
                    var result = service.Login("old@example.com", Password);
                    Assert.That(result.Success, Is.False);
                    Assert.That(result.Message, Is.EqualTo("Email or password is incorrect."));
                    Assert.That(service.SignedInEmail, Is.Null);
                    Assert.That(service.Register("old@example.com", Password, Password).Success, Is.False);
                }
            });
        }

        private static void WithDatabase(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "TravelPlanningTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try { test(Path.Combine(folder, "accounts.db")); }
            finally { Directory.Delete(folder, true); }
        }
    }
}
