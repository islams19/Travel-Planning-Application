using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using TravelApp;

public class FoundationTests
{
    Product product;
    User user;
    TravelCatalog catalog;
    string folder;
    [SetUp] public void Setup()
    {
        product = ScriptableObject.CreateInstance<Product>();
        product.id = "hotel"; product.productName = "Test Hotel"; product.location = "Miami";
        product.price = 50; product.available = true;
        product.startDate = new TravelDate { year = 2026, month = 1, day = 1 };
        product.endDate = new TravelDate { year = 2027, month = 12, day = 31 };
        user = ScriptableObject.CreateInstance<User>(); user.username = "Student"; user.password = "demo";
        catalog = ScriptableObject.CreateInstance<TravelCatalog>(); catalog.products.Add(product); catalog.users.Add(user);
        folder = Path.Combine(Path.GetTempPath(), "travel-test-" + Guid.NewGuid().ToString("N"));
    }
    AppServices Open() { return new AppServices(catalog, new JsonFileStore(Path.Combine(folder, "save.json"))); }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(product); UnityEngine.Object.DestroyImmediate(user); UnityEngine.Object.DestroyImmediate(catalog);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
    [Test] public void SearchMatchesCaseAndExcludesUnavailable()
    {
        var query = new ProductQuery { text = "MIAMI" };
        Assert.AreEqual(1, ProductSearch.Find(catalog.products, query).Count);
        product.available = false;
        Assert.AreEqual(0, ProductSearch.Find(catalog.products, query).Count);
    }
    [Test] public void InvalidDatesAreRejectedInsteadOfIgnoringFilter()
    {
        Assert.IsFalse(ProductQuery.TryCreate("", 0, "", "", 0, "2026-02-30", "", 0, out _, out _));
        Assert.IsFalse(ProductQuery.TryCreate("", 0, "80", "20", 0, "", "", 0, out _, out _));
    }
    [Test] public void CheckoutPersistsSnapshotAndDoesNotMutateAssets()
    {
        var app = Open(); Assert.IsTrue(app.Accounts.Login(" student ", "demo").success);
        Assert.IsTrue(app.Bookings.Add(product.id, "2026-11-01", "2026-11-03").success);
        Assert.IsFalse(app.Bookings.Add(product.id, "2026-11-01", "2026-11-03").success);
        Assert.IsTrue(app.Bookings.Checkout().success);
        Assert.IsFalse(app.Bookings.Checkout().success);
        Assert.AreEqual(0, user.cart.Count); Assert.AreEqual(0, user.purchaseHistory.Count);
        product.price = 999;
        var reopened = Open(); reopened.Accounts.Login("Student", "demo");
        Assert.AreEqual(50m, reopened.Bookings.History[0].total);
        Assert.AreEqual(0, reopened.Bookings.Cart.Count);
    }
    [Test] public void AccountsAreIsolatedAndRegistrationPersists()
    {
        var app = Open(); app.Accounts.Login("Student", "demo"); app.Bookings.Add("hotel", "2026-11-01", "2026-11-03");
        app.Accounts.Logout(); Assert.IsFalse(app.Bookings.Checkout().success);
        Assert.IsTrue(app.Accounts.Register("Other", "other@example.com", "password").success);
        Assert.AreEqual(0, app.Bookings.Cart.Count);
        Assert.IsFalse(app.Accounts.Register("student", "a@b.com", "password").success);
        var reopened = Open(); Assert.IsTrue(reopened.Accounts.Login("other", "password").success);
    }
    [Test] public void UnavailableItemCannotBeCheckedOut()
    {
        var app = Open(); app.Accounts.Login("Student", "demo"); app.Bookings.Add("hotel", "2026-11-01", "2026-11-03");
        product.available = false; Assert.IsFalse(app.Bookings.Checkout().success);
        Assert.AreEqual(1, app.Bookings.Cart.Count);
    }
    [Test] public void ReviewsRequirePurchaseAndPersist()
    {
        var app = Open(); app.Accounts.Login("Student", "demo");
        Assert.IsFalse(app.Bookings.Review("hotel", 5, "Nice").success);
        app.Bookings.Add("hotel", "2026-11-01", "2026-11-03"); app.Bookings.Checkout();
        Assert.IsTrue(app.Bookings.Review("hotel", 5, "Nice").success);
        Assert.IsFalse(app.Bookings.Review("hotel", 6, "Nice").success);
        var reopened = Open(); Assert.AreEqual(1, reopened.Bookings.Reviews.Count);
    }
    [Test] public void CorruptPrimaryRecoversBackup()
    {
        var app = Open(); app.Accounts.Register("Other", "other@example.com", "password");
        app.Bookings.Add("hotel", "2026-11-01", "2026-11-03");
        File.WriteAllText(Path.Combine(folder, "save.json"), "broken");
        Assert.IsTrue(Open().Accounts.Login("Other", "password").success);
    }
    sealed class FailingStore : ISaveStore
    {
        public bool fail;
        public SaveData Load() => new SaveData();
        public void Save(SaveData data) { if (fail) throw new IOException("disk full"); }
    }
    [Test] public void FailedSaveRollsBackCartAndCheckout()
    {
        var store = new FailingStore(); var app = new AppServices(catalog, store);
        app.Accounts.Login("Student", "demo");
        store.fail = true;
        Assert.IsFalse(app.Bookings.Add("hotel", "2026-11-01", "2026-11-03").success);
        Assert.AreEqual(0, app.Bookings.Cart.Count);
        store.fail = false; app.Bookings.Add("hotel", "2026-11-01", "2026-11-03"); store.fail = true;
        Assert.IsFalse(app.Bookings.Checkout().success);
        Assert.AreEqual(1, app.Bookings.Cart.Count); Assert.AreEqual(0, app.Bookings.History.Count);
    }
    [Test] public void DateFilterRequiresFullAvailabilityAndSupportsOneBound()
    {
        var q = new ProductQuery { start = new DateTime(2026, 11, 1) };
        Assert.AreEqual(1, ProductSearch.Find(catalog.products, q).Count);
        q.end = new DateTime(2028, 1, 1);
        Assert.AreEqual(0, ProductSearch.Find(catalog.products, q).Count);
        q.start = null; q.end = new DateTime(2026, 11, 2);
        Assert.AreEqual(1, ProductSearch.Find(catalog.products, q).Count);
    }
    [Test] public void CancelledBookingRemainsInHistoryAndCannotBeReviewed()
    {
        var app = Open(); app.Accounts.Login("Student", "demo"); app.Bookings.Add("hotel", "2026-11-01", "2026-11-03"); app.Bookings.Checkout();
        Assert.IsTrue(app.Bookings.Cancel(app.Bookings.History[0].id).success);
        Assert.AreEqual(1, app.Bookings.History.Count); Assert.IsTrue(app.Bookings.History[0].cancelled);
        Assert.IsFalse(app.Bookings.Review("hotel", 5, "Nice").success);
    }
    [Test] public void InvalidAndOutOfRangeBookingDatesAreRejected()
    {
        var app = Open(); app.Accounts.Login("Student", "demo");
        Assert.IsFalse(app.Bookings.Add("hotel", "bad", "bad").success);
        Assert.IsFalse(app.Bookings.Add("hotel", "2026-11-03", "2026-11-01").success);
        Assert.IsFalse(app.Bookings.Add("hotel", "2028-11-01", "2028-11-03").success);
        Assert.AreEqual(0, app.Bookings.Cart.Count);
    }
    [Test] public void MalformedNestedSaveRecoversBackup()
    {
        var app = Open(); app.Accounts.Register("Other", "other@example.com", "password");
        app.Bookings.Add("hotel", "2026-11-01", "2026-11-03");
        File.WriteAllText(Path.Combine(folder, "save.json"), "{\"version\":1,\"accounts\":[{\"username\":\"broken\",\"cart\":[null],\"bookings\":[]}],\"reviews\":[]}");
        Assert.IsTrue(Open().Accounts.Login("Other", "password").success);
    }
}
