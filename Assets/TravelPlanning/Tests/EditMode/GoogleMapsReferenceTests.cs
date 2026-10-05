using System.IO;
using NUnit.Framework;
using SQLite;
using TravelPlanning.Data;
using TravelPlanning.Destinations;
using TravelPlanning.Reviews;
using UnityEngine;

namespace TravelPlanning.Tests
{
    public sealed class GoogleMapsReferenceTests
    {
#region UnsafeOrMalformedReferenceFailsWithoutThrowing
        [TestCase(null)]
        [TestCase("")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=++")]
        [TestCase("http://www.google.com/maps/search/?api=1&query=London")]
        [TestCase("file:///maps/search/?api=1&query=London")]
        [TestCase("javascript:alert(1)")]
        [TestCase("https://user@www.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://@www.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com.evil.example/maps/search/?api=1&query=London")]
        [TestCase("https://evil.example/maps/search/?api=1&query=London")]
        [TestCase("https://maps.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com:444/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London#details")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London#")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&redirect=https://evil.example")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&query=Paris")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&api=1")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&%71uery=Paris")]
        [TestCase("https://www.google.com/maps/search/?api=2&query=London")]
        [TestCase("https://www.google.com/maps/search/?query=London")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=bad%ZZ")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=bad%")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=%FF")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=%0ALondon")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London%5CParis")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London\n")]
        [TestCase("https://www.google.com\\maps/search/?api=1&query=London")]
        [TestCase(" https://www.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com/other/../maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com/maps/search?api=1&query=London")]
        [TestCase("https://www.google.com/maps/%73earch/?api=1&query=London")]
        public void UnsafeOrMalformedReferenceFailsWithoutThrowing(string raw)
        {
            string canonical = "unchanged";
            Assert.DoesNotThrow(() => Assert.That(GoogleMapsReference.TryGetUrl(raw, out canonical), Is.False));
            Assert.That(canonical, Is.Null);
        }

#endregion
#region SafeSearchReferenceIsCanonicalized
        [TestCase("https://google.com/maps/search/?query=London+Eye&api=1", "https://www.google.com/maps/search/?api=1&query=London%20Eye")]
        [TestCase("https://www.google.com:443/maps/search/?api=1&query=A%26B%20%2B%20Cafe", "https://www.google.com/maps/search/?api=1&query=A%26B%20%2B%20Cafe")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=%E6%9D%B1%E4%BA%AC", "https://www.google.com/maps/search/?api=1&query=%E6%9D%B1%E4%BA%AC")]
        public void SafeSearchReferenceIsCanonicalized(string raw, string expected)
        {
            Assert.That(GoogleMapsReference.TryGetUrl(raw, out var canonical), Is.True);
            Assert.That(canonical, Is.EqualTo(expected));
        }

#endregion
#region All144BundledPlaceReferencesAreAccepted
        [Test]
        public void All144BundledPlaceReferencesAreAccepted()
        {
            SqliteRuntime.Initialize();
            using (var connection = new SQLiteConnection(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), SQLiteOpenFlags.ReadOnly))
            {
                var places = connection.Query<PlaceOption>(
                    "SELECT google_maps_url AS GoogleMapsUrl FROM hotels " +
                    "UNION ALL SELECT google_maps_url FROM restaurants " +
                    "UNION ALL SELECT google_maps_url FROM experiences " +
                    "UNION ALL SELECT google_maps_url FROM hotspots");
                Assert.That(places.Count, Is.EqualTo(144));
                foreach (var place in places)
                {
                    Assert.That(GoogleMapsReference.TryGetUrl(place.GoogleMapsUrl, out var canonical), Is.True, place.GoogleMapsUrl);
                    Assert.That(canonical, Does.StartWith("https://www.google.com/maps/search/?api=1&query="));
                }
            }
        }
#endregion
    }
}
