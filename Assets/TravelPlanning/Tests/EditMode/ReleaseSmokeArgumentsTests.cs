using System;
using NUnit.Framework;
using TravelPlanning.UI.Polish;

namespace TravelPlanning.Tests
{
    public sealed class ReleaseSmokeArgumentsTests
    {
        private const string Isolated = "auth-smoke-0123456789abcdef0123456789abcdef.db";
#region ExplicitReleaseQaUsesOnlyItsGuidNamedDatabase
        [Test]
        public void ExplicitReleaseQaUsesOnlyItsGuidNamedDatabase()
        {
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "app.exe", "-releaseSmoke", "-authFile", Isolated }, false), Is.EqualTo(Isolated));
            Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-releaseSmoke", "-authFile", Isolated }, true));
        }

#endregion
#region MalformedFileCannotResolveToAnyDatabase
        [TestCase("travel.db")]
        [TestCase("auth-smoke-example.db")]
        [TestCase("../auth-smoke-0123456789abcdef0123456789abcdef.db")]
        [TestCase("C:\\auth-smoke-0123456789abcdef0123456789abcdef.db")]
        [TestCase("auth-smoke-0123456789abcdef0123456789abcdeg.db")]
        [TestCase("auth-smoke-0123456789abcdef0123456789abcdef.db.extra")]
        public void MalformedFileCannotResolveToAnyDatabase(string file)
        {
            Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-releaseSmoke", "-authFile", file }, false));
        }

#endregion
#region MissingDuplicateAndCombinedFlagsAreRejected
        [Test]
        public void MissingDuplicateAndCombinedFlagsAreRejected()
        {
            foreach (var args in new[]
            {
                new[]
                {
                    "-releaseSmoke"
                },
                new[]
                {
                    "-releaseSmoke",
                    "-authFile"
                },
                new[]
                {
                    "-releaseSmoke",
                    "-releaseSmoke",
                    "-authFile",
                    Isolated
                },
                new[]
                {
                    "-releaseSmoke",
                    "-authFile",
                    Isolated,
                    "-authFile",
                    Isolated
                },
                new[]
                {
                    "-releaseSmoke",
                    "-authFile",
                    Isolated,
                    "-authSmoke"
                }
            }

            )
                Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(args, false));
        }

#endregion
#region OrdinaryReleaseIgnoresOverrideAndLegacySmokeCannotRunInRelease
        [Test]
        public void OrdinaryReleaseIgnoresOverrideAndLegacySmokeCannotRunInRelease()
        {
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-authFile", "arbitrary.db" }, false, "editor.db"), Is.EqualTo("travel.db"));
            foreach (string flag in new[]
            {
                "-authSmoke",
                "-flightSmoke",
                "-destinationSmoke",
                "-reviewSmoke",
                "-tripSmoke",
                "-trackingSmoke"
            }

            )
                Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(new[] { flag, "-authFile", Isolated }, false));
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-authSmoke", "-authFile", Isolated }, true), Is.EqualTo(Isolated));
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new string[0], true, "editor-play-test.db"), Is.EqualTo("editor-play-test.db"));
        }
#endregion
    }
}
