using System;
using System.Net.Mail;

namespace TravelPlanning.Accounts
{
    /// <summary>The receptionist: checks the form, registers accounts, and verifies login details.</summary>
    public sealed class AccountService
    {
        private readonly AccountDatabase database;

        public string SignedInEmail { get; private set; }

        public AccountService(AccountDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public AccountResult Register(string email, string password, string confirmation)
        {
            string normalized = NormalizeEmail(email);
            if (normalized == null)
                return new AccountResult(false, "Enter a valid email address, such as name@example.com.");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
                return new AccountResult(false, "Use a password with 8 to 128 characters.");
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
                return new AccountResult(false, "The passwords do not match.");

            byte[] salt = PasswordHasher.NewSalt();
            byte[] hash = PasswordHasher.Hash(password, salt, PasswordHasher.Iterations);
            if (!database.Insert(normalized, salt, hash))
                return new AccountResult(false, "An account with this email already exists. Please log in.");

            // Registration does not sign in automatically; the user returns to the login form.
            return new AccountResult(true, "Account created. You can now log in.", normalized);
        }

        public AccountResult Login(string email, string password)
        {
            SignedInEmail = null;
            string normalized = NormalizeEmail(email);
            if (normalized == null || string.IsNullOrEmpty(password) || password.Length > 128)
                return InvalidLogin();
            var account = database.Find(normalized);
            if (account == null)
                return InvalidLogin();

            byte[] actual = PasswordHasher.Hash(password, account["salt"].AsBinary, account["iterations"].AsInt32);
            if (!PasswordHasher.Matches(account["passwordHash"].AsBinary, actual))
                return InvalidLogin();

            SignedInEmail = normalized;
            return new AccountResult(true, "You are logged in.", normalized);
        }

        public void Logout()
        {
            SignedInEmail = null;
        }

        private static AccountResult InvalidLogin()
        {
            return new AccountResult(false, "Email or password is incorrect.");
        }

        private static string NormalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;
            string value = email.Trim().ToLowerInvariant();
            if (value.Length > 254)
                return null;
            try
            {
                var parsed = new MailAddress(value);
                // Accept a plain email only, not a display name such as 'Duy <duy@example.com>'.
                return parsed.Address == value && parsed.Host.Contains(".") ? value : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
