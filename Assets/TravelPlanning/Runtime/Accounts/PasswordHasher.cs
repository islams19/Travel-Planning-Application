using System;
using System.Security.Cryptography;

namespace TravelPlanning.Accounts
{
    /// <summary>PBKDF2 repeats a password calculation to make guessing expensive; each account has a random salt.</summary>
    internal static class PasswordHasher
    {
        private const string Algorithm = "PBKDF2-SHA256";
        private const int Iterations = 600000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private static readonly byte[] DummySalt = new byte[SaltBytes];
#region Create
        internal static AccountRecord Create(string email, string password)
        {
            var salt = new byte[SaltBytes];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(salt);
            byte[] hash = Derive(password, salt, Iterations);
            try
            {
                return new AccountRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Email = email,
                    Hash = Convert.ToBase64String(hash),
                    Salt = Convert.ToBase64String(salt),
                    Iterations = Iterations,
                    Algorithm = Algorithm
                };
            }
            finally
            {
                Array.Clear(hash, 0, hash.Length);
            }
        }
#endregion

#region Verify
        internal static bool Verify(AccountRecord account, string password)
        {
            if (account == null)
            {
                // Unknown emails still do the normal amount of expensive work.
                byte[] dummy = Derive(password, DummySalt, Iterations);
                Array.Clear(dummy, 0, dummy.Length);
                return false;
            }

            // Reject damaged or unexpected metadata before allocating/starting an expensive calculation.
            if (account.Algorithm != Algorithm ||
                account.Iterations < Iterations ||
                account.Iterations > 2000000 ||
                account.Salt == null ||
                account.Salt.Length != 24 ||
                account.Hash == null ||
                account.Hash.Length != 44)
                return false;
            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(account.Salt);
                expected = Convert.FromBase64String(account.Hash);
            }
            catch (FormatException)
            {
                return false;
            }

            if (salt.Length != SaltBytes || expected.Length != HashBytes)
                return false;
            byte[] actual = Derive(password, salt, account.Iterations);
            try
            {
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            finally
            {
                Array.Clear(actual, 0, actual.Length);
                Array.Clear(expected, 0, expected.Length);
            }
        }
#endregion

#region Derive
        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var calculation = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return calculation.GetBytes(HashBytes);
        }
#endregion
    }
}
