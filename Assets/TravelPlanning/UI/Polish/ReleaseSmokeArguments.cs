using System;
using System.IO;
using System.Linq;

namespace TravelPlanning.UI.Polish
{
    /// <summary>Validates isolation before LoginPage creates or opens any database.</summary>
    public static class ReleaseSmokeArguments
    {
        private static readonly string[] LegacyFlags =
        {
            "-authSmoke",
            "-flightSmoke",
            "-destinationSmoke",
            "-reviewSmoke",
            "-tripSmoke",
            "-trackingSmoke"
        };
        public static bool IsSmokeRequested(string[] args) => args.Contains("-releaseSmoke") || args.Any(value => LegacyFlags.Contains(value));
        public static string ResolveDatabaseFile(string[] args, bool developmentBuild, string developmentOverride = null)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            bool release = args.Contains("-releaseSmoke");
            bool legacy = args.Any(value => LegacyFlags.Contains(value));
            if (release)
            {
                if (developmentBuild || legacy || args.Count(value => value == "-releaseSmoke") != 1 || args.Count(value => value == "-authFile") != 1)
                {
                    throw new ArgumentException("Release QA requires one isolated release flag and a nondevelopment player.");
                }

                string file = Value(args, "-authFile");
                if (!IsIsolatedFile(file))
                {
                    throw new ArgumentException("Release QA requires auth-smoke-<32 hexadecimal GUID characters>.db.");
                }

                return file;
            }

            string smokeFile = Value(args, "-authFile");
            if (legacy && (!developmentBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
            {
                throw new ArgumentException("Development smoke checks require an explicit isolated database.");
            }

            if (!developmentBuild)
            {
                return "travel.db"; // Ordinary releases ignore arbitrary database overrides.
            }

            string selected = smokeFile ?? developmentOverride;
            if (string.IsNullOrEmpty(selected))
            {
                return "travel.db";
            }

            if (Path.GetFileName(selected) != selected || !selected.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The test database must be a .db file name.");
            }

            return selected;
        }

        public static bool IsIsolatedFile(string file)
        {
            const string prefix = "auth-smoke-";
            return file != null && file.Length == prefix.Length + 32 + 3 && file.StartsWith(prefix, StringComparison.Ordinal) &&
                file.EndsWith(".db", StringComparison.Ordinal) && Guid.TryParseExact(file.Substring(prefix.Length, 32), "N",
                out _);
        }

        public static string Value(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
