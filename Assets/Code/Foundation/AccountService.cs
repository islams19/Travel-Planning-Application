using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace TravelApp
{
    public sealed class AccountService
    {
        readonly AppServices app;
        string currentName;
        public string Username => currentName;
        public bool IsLoggedIn => currentName != null;
        public User CurrentUser => app.Catalog.users.Find(u => u != null && Same(u.username, currentName));
        public bool IsAdmin => CurrentUser != null && CurrentUser.role == UserRole.Admin;
        internal AccountData Current => app.Data.accounts.Find(a => Same(a.username, currentName));
        public AccountService(AppServices app) { this.app = app; }
        internal static bool Same(string a, string b) => a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        public Result Login(string username, string password)
        {
            username = (username ?? "").Trim();
            if (username.Length == 0 || string.IsNullOrEmpty(password)) return Result.Fail("Enter your username and password.");
            var saved = app.Data.accounts.Find(a => Same(a.username, username));
            var seed = app.Catalog.users.Find(u => u != null && Same(u.username, username));
            bool valid = saved != null && !string.IsNullOrEmpty(saved.passwordHash) ? Hash(password, saved.salt) == saved.passwordHash : seed != null && seed.password == password;
            if (!valid) return Result.Fail("Username or password is incorrect.");
            if (saved == null)
            {
                var account = Make(seed.username.Trim(), seed.email, password);
                var result = app.Commit(() => app.Data.accounts.Add(account), "Account initialized.");
                if (!result.success) return result;
                saved = account;
            }
            currentName = saved.username; app.Notify(); return Result.Ok("Welcome, " + currentName + ".");
        }
        public Result Register(string username, string email, string password)
        {
            username = (username ?? "").Trim(); email = (email ?? "").Trim();
            if (username.Length < 3 || username.Length > 40 || username.Any(char.IsControl)) return Result.Fail("Use a username between 3 and 40 characters.");
            if (email.Length > 120 || !email.Contains("@") || email.StartsWith("@") || email.EndsWith("@")) return Result.Fail("Enter an email address.");
            if (string.IsNullOrEmpty(password) || password.Length < 4 || password.Length > 128) return Result.Fail("Use a password between 4 and 128 characters.");
            if (app.Data.accounts.Exists(a => Same(a.username, username)) || app.Catalog.users.Exists(u => u != null && Same(u.username, username))) return Result.Fail("That username is already in use.");
            var account = Make(username, email, password);
            var result = app.Commit(() => app.Data.accounts.Add(account), "Account created.");
            if (result.success) { currentName = username; app.Notify(); }
            return result;
        }
        public void Logout() { currentName = null; app.Notify(); }
        static AccountData Make(string username, string email, string password)
        {
            byte[] salt = new byte[16]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            string encoded = Convert.ToBase64String(salt);
            return new AccountData { username = username, email = email, salt = encoded, passwordHash = Hash(password, encoded) };
        }
        static string Hash(string password, string salt)
        {
            using (var derive = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), 10000, HashAlgorithmName.SHA256))
                return Convert.ToBase64String(derive.GetBytes(32));
        }
    }
}
