using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Data;
using TravelPlanning.Destinations;
using TravelPlanning.Reviews;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class ReviewServiceTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private ReviewService service;
#region SetUp
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelReviewTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
            service = new ReviewService(database);
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
#region FourPlaceTypesReturnOnlyTheirOwnSampleReviewsAndFreshSummary
        [UnityTest]
        public IEnumerator FourPlaceTypesReturnOnlyTheirOwnSampleReviewsAndFreshSummary()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            foreach (PlaceCategory category in Enum.GetValues(typeof(PlaceCategory)))
            {
                string placeId = "london-" + category.ToString().ToLowerInvariant() + "-1";
                var load = service.LoadAsync(category, placeId);
                yield return Wait(load);
                var details = load.Result;
                Assert.That(details.Place.Id, Is.EqualTo(placeId));
                Assert.That(details.Place.Category, Is.EqualTo(category));
                Assert.That(details.Place.DestinationId, Is.EqualTo("london"));
                Assert.That(details.Place.Name, Is.Not.Empty);
                Assert.That(details.Place.GoogleMapsUrl, Does.StartWith("https://www.google.com/maps/search/"));
                Assert.That(details.Reviews.Select(review => review.Id), Is.EqualTo(new[] { placeId + "-review-1", placeId + "-review-2" }));
                Assert.That(details.Reviews.Select(review => review.Rating), Is.EqualTo(new[] { 4, 5 }));
                Assert.That(details.Reviews.All(review => review.IsDemo && review.TravelerName.StartsWith("Demo traveler") &&
                    review.Body.StartsWith("FICTIONAL DEMO REVIEW:")), Is.True);
                Assert.That(details.Place.ReviewCount, Is.EqualTo(2));
                Assert.That(details.Place.AverageRating, Is.EqualTo(4.5));
                Assert.That(details.WorkerThreadId, Is.Not.EqualTo(mainThread));
            }
        }

#endregion
#region ReloadReflectsChangedPlaceNonDemoReviewAndThenEmptyReviews
        [UnityTest]
        public IEnumerator ReloadReflectsChangedPlaceNonDemoReviewAndThenEmptyReviews()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("UPDATE hotels SET name=?, price_cents=? WHERE id=?", "Changed hotel", 12345, "london-hotel-1");
                connection.Execute("DELETE FROM reviews WHERE hotel_id=?", "london-hotel-1");
                connection.Execute("INSERT INTO reviews (id,hotel_id,traveler_name,rating,body,is_demo) VALUES (?,?,?,?,?,?)", "fixture-review",
                    "london-hotel-1", "Local traveler", 3, "Local fixture review body", 0);
                return 0;
            }, CancellationToken.None));
            var edited = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            yield return Wait(edited);
            Assert.That(edited.Result.Place.Name, Is.EqualTo("Changed hotel"));
            Assert.That(edited.Result.Place.PriceCents, Is.EqualTo(12345));
            Assert.That(edited.Result.Place.ReviewCount, Is.EqualTo(1));
            Assert.That(edited.Result.Place.AverageRating, Is.EqualTo(3));
            Assert.That(edited.Result.Reviews.Single().IsDemo, Is.False);
            Assert.That(edited.Result.Reviews.Single().Body, Is.EqualTo("Local fixture review body"));
            yield return Wait(database.ExecuteAsync(connection => connection.Execute("DELETE FROM reviews WHERE hotel_id=?", "london-hotel-1"), CancellationToken.None));
            var empty = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            yield return Wait(empty);
            Assert.That(empty.Result.Reviews, Is.Empty);
            Assert.That(empty.Result.Place.ReviewCount, Is.Zero);
            Assert.That(empty.Result.Place.AverageRating, Is.Null);
        }

#endregion
#region TypedForeignKeyKeepsSameIdInAnotherCategoryIsolated
        [UnityTest]
        public IEnumerator TypedForeignKeyKeepsSameIdInAnotherCategoryIsolated()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute(
                    "INSERT INTO restaurants (id,destination_id,name,description,address,price_cents,currency,google_maps_url) VALUES (?,?,?,?,?,?,?,?)",
                    "london-hotel-1", "london", "Different category", "Fixture", "Fixture address", 1000, "USD",
                    "https://www.google.com/maps/search/?api=1&query=Fixture");
                connection.Execute("INSERT INTO reviews (id,restaurant_id,traveler_name,rating,body,is_demo) VALUES (?,?,?,?,?,?)",
                    "restaurant-collision", "london-hotel-1", "Fixture traveler", 1, "Restaurant only", 1);
                return 0;
            }, CancellationToken.None));
            var hotel = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            var restaurant = service.LoadAsync(PlaceCategory.Restaurant, "london-hotel-1");
            yield return Wait(hotel);
            yield return Wait(restaurant);
            Assert.That(hotel.Result.Reviews.Count, Is.EqualTo(2));
            Assert.That(hotel.Result.Place.AverageRating, Is.EqualTo(4.5));
            Assert.That(restaurant.Result.Reviews.Single().Id, Is.EqualTo("restaurant-collision"));
            Assert.That(restaurant.Result.Place.AverageRating, Is.EqualTo(1));
        }

#endregion
#region InvalidCategoryWrongCategoryAndInjectionIdAreRejected
        [UnityTest]
        public IEnumerator InvalidCategoryWrongCategoryAndInjectionIdAreRejected()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            Assert.Throws<ArgumentException>(() => service.LoadAsync((PlaceCategory)99, "london-hotel-1"));
            Assert.Throws<ArgumentException>(() => service.LoadAsync(PlaceCategory.Hotel, null));
            Assert.Throws<ArgumentException>(() => service.LoadAsync(PlaceCategory.Hotel, "  "));
            foreach (string id in new[]
            {
                "missing",
                "london-restaurant-1",
                "london-hotel-1' OR 1=1 --",
                "'; DELETE FROM reviews; --"
            }

            )
            {
                var pending = service.LoadAsync(PlaceCategory.Hotel, id);
                yield return Completion(pending);
                Assert.That(pending.IsFaulted, Is.True);
                Assert.That(pending.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
            }

            var valid = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            yield return Wait(valid);
            Assert.That(valid.Result.Reviews.Count, Is.EqualTo(2));
        }

#endregion
#region CancellationWhileQueuedStopsReviewLoad
        [UnityTest]
        public IEnumerator CancellationWhileQueuedStopsReviewLoad()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection =>
                {
                    entered.Set();
                    return release.Wait(TimeSpan.FromSeconds(15));
                }, CancellationToken.None);
                while (!entered.IsSet)
                    yield return null;
                Task<ReviewDetails> pending;
                try
                {
                    pending = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1", cancellation.Token);
                    cancellation.Cancel();
                }
                finally
                {
                    release.Set();
                }

                yield return Wait(blocker);
                yield return Completion(pending);
                Assert.That(pending.IsCanceled, Is.True);
            }
        }

#endregion
#region ReadingReviewsPreservesEveryDatabaseByte
        [UnityTest]
        public IEnumerator ReadingReviewsPreservesEveryDatabaseByte()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            byte[] before = Hash();
            foreach (PlaceCategory category in Enum.GetValues(typeof(PlaceCategory)))
                yield return Wait(service.LoadAsync(category, "london-" + category.ToString().ToLowerInvariant() + "-1"));
            Assert.That(Hash(), Is.EqualTo(before));
        }

#endregion
#region Hash
        private byte[] Hash()
        {
            using (var hash = SHA256.Create())
                return hash.ComputeHash(File.ReadAllBytes(path));
        }

#endregion
#region Wait
        private static IEnumerator Wait(Task task)
        {
            yield return Completion(task);
            task.GetAwaiter().GetResult();
        }

#endregion
#region Completion
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Review operation timed out.");
                yield return null;
            }
        }
#endregion
    }
}
