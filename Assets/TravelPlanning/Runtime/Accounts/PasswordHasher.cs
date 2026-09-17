using System.Security.Cryptography;

namespace TravelPlanning.Accounts
{
    /// <summary>Makes a one-way password fingerprint, so the actual password is never saved.</summary>
    internal static class PasswordHasher
    {
        internal const int Iterations = 600000;

        internal static byte[] NewSalt()
        {
            // A different random salt makes identical passwords produce different fingerprints.
            var salt = new byte[16];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(salt);
            return salt;
        }

        internal static byte[] Hash(string password, byte[] salt, int iterations)
        {
            using (var algorithm = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return algorithm.GetBytes(32);
        }

        internal static bool Matches(byte[] expected, byte[] actual)
        {
            if (expected.Length != actual.Length)
                return false;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++)
                difference |= expected[i] ^ actual[i];
            return difference == 0;
        }
    }
}
