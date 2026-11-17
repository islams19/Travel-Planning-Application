using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;

public class TravelFlowTests
{
    sealed class MemoryStore : ISaveStore
    {
        public SaveData Load() => new SaveData();
        public void Save(SaveData data) { }
    }
    TravelCatalog catalog;
    Product product, secondProduct;
    AppServices original;
    static readonly PropertyInfo ServicesProperty = typeof(TravelAppRuntime).GetProperty("Services");
    static UIButton Button(string name) => Object.FindObjectsByType<UIButton>(FindObjectsSortMode.None).First(b => b.gameObject.name == name && b.gameObject.activeInHierarchy);
    static TMP_InputField Input(string name) => Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).First(f => f.gameObject.name == name && f.gameObject.activeInHierarchy);
    IEnumerator Screen(string name)
    {
        float deadline = Time.realtimeSinceStartup + 10;
        while (Time.realtimeSinceStartup < deadline && (SceneManager.GetActiveScene().name != name || TravelAppRuntime.Instance.IsNavigating)) yield return null;
        Assert.AreEqual(name, SceneManager.GetActiveScene().name); yield return null;
        Assert.AreEqual(1, Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length, "One authored app Canvas, no runtime overlay.");
        Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
    }
    [UnityTest, Timeout(30000)] public IEnumerator EditableUIRegistrationBookingReviewCancelReloginAndSearch()
    {
        Assert.IsNotNull(TravelAppRuntime.Instance); original = TravelAppRuntime.Instance.Services;
        product = ScriptableObject.CreateInstance<Product>(); product.id = "smoke"; product.productName = "Smoke Hotel"; product.price = 75;
        product.startDate = new TravelDate { year = 2026, month = 1, day = 1 }; product.endDate = new TravelDate { year = 2027, month = 12, day = 31 };
        catalog = ScriptableObject.CreateInstance<TravelCatalog>(); catalog.products.Add(product);
        var services = new AppServices(catalog, new MemoryStore()); ServicesProperty.SetValue(TravelAppRuntime.Instance, services);
        TravelAppRuntime.Instance.Navigate("LoginScreen"); yield return Screen("LoginScreen");
        Button("Create account").onClick.Invoke(); yield return null;
        Input("RegisterUsername").text = "SmokeStudent"; Input("RegisterEmail").text = "student@example.com"; Input("RegisterPassword").text = "demo";
        Button("Register account").onClick.Invoke(); yield return Screen("HomeScreen");
        Assert.IsTrue(services.Accounts.IsLoggedIn);
        Button("Browse all travel products").onClick.Invoke(); yield return null;
        Assert.AreEqual(1, TravelScreenUI.Current.catalog.results.VisibleCount);
        Button("PrimaryAction").onClick.Invoke(); yield return null;
        Button("Add to cart").onClick.Invoke(); Assert.AreEqual(1, services.Bookings.Cart.Count);
        secondProduct=Object.Instantiate(product); secondProduct.id="smoke2"; secondProduct.productName="Second Hotel"; secondProduct.price=25;
        catalog.products.Add(secondProduct);
        TravelScreenUI.Current.Browse(); yield return null;
        Object.FindObjectsByType<TravelItemView>(FindObjectsSortMode.None).First(c=>c.gameObject.activeInHierarchy && c.title.text=="Second Hotel").primaryButton.onClick.Invoke(); yield return null;
        Button("Add to cart").onClick.Invoke(); Assert.AreEqual(2,services.Bookings.Cart.Count);
        Button("Open cart").onClick.Invoke(); yield return null;
        Button("Review checkout").onClick.Invoke(); yield return null;
        Button("Confirm simulated booking").onClick.Invoke(); yield return null;
        Assert.AreEqual(100m, services.Bookings.History[0].total); Assert.AreEqual(0, services.Bookings.Cart.Count);
        Assert.AreEqual(2,TravelScreenUI.Current.account.bookings.VisibleCount);
        Object.FindObjectsByType<TravelItemView>(FindObjectsSortMode.None).First(c=>c.gameObject.activeInHierarchy && c.title.text=="Second Hotel").secondaryButton.onClick.Invoke(); yield return null;
        Input("ReviewRating").text = "5"; Input("ReviewText").text = "A lovely stay.";
        Button("Save review").onClick.Invoke(); Assert.AreEqual(1, services.Bookings.Reviews.Count); Assert.AreEqual("smoke2",services.Bookings.Reviews[0].productId);
        TravelScreenUI.Current.Account(); yield return null;
        Button("PrimaryAction").onClick.Invoke(); yield return null;
        Button("Yes, cancel booking").onClick.Invoke(); yield return null;
        Assert.IsTrue(services.Bookings.History[0].cancelled);
        Button("Log out").onClick.Invoke(); yield return Screen("LoginScreen");
        Assert.IsFalse(services.Accounts.IsLoggedIn);
        Input("UsernameInput").text = "SmokeStudent"; Input("PasswordInput").text = "demo";
        Button("Sign in").onClick.Invoke(); yield return Screen("HomeScreen");
        Button("Explore").onClick.Invoke(); yield return Screen("BookingScreen");
        var view = TravelScreenUI.Current.catalog; view.ResetFilters(); yield return null;
        Assert.AreEqual(2, view.results.VisibleCount);
        view.minPriceInput.text = "200"; view.Search(); yield return null; Assert.AreEqual(0, view.results.VisibleCount);
        view.ResetFilters(); yield return null; Assert.AreEqual(2, view.results.VisibleCount);
        view.typeDropdown.Show(); yield return null;
        Assert.IsTrue(Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None).Any(t => t.gameObject.activeInHierarchy));
        view.typeDropdown.Hide(); yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(1, TravelScreenUI.Current.pages.Count(p => p.activeSelf));
        Button("Log out").onClick.Invoke(); yield return Screen("LoginScreen"); Assert.IsFalse(services.Accounts.IsLoggedIn);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (TravelAppRuntime.Instance != null && original != null) ServicesProperty.SetValue(TravelAppRuntime.Instance, original);
        if (catalog != null) Object.Destroy(catalog); if (product != null) Object.Destroy(product); if(secondProduct!=null) Object.Destroy(secondProduct); yield return null;
    }
}
