using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace TravelApp
{
    public enum TravelPage { Login, Register, Home, Catalog, Details, Cart, Checkout, Account, Confirmation }
    public sealed class TravelScreenUI : MonoBehaviour
    {
        public static TravelScreenUI Current { get; private set; }
        [Header("Editor preview / initial screen")]
        public TravelPage initialPage;
        [Tooltip("One authored panel per TravelPage, in enum order.")]
        public GameObject[] pages;
        [Header("Shared navigation and feedback")]
        public TMP_Text[] pageHeadings, pageSubtitles;
        public TMP_Text heading => pageHeadings[(int)ActivePage];
        public TMP_Text subtitle => pageSubtitles[(int)ActivePage];
        public TMP_Text feedback, identity, cartCount;
        public UnityEngine.UI.Button[] signedInButtons;
        public GameObject signedInNavigation, signedOutNavigation;
        [Header("Screen presenters")]
        public TravelAuthView auth;
        public TravelCatalogView catalog;
        public TravelDetailsView details;
        public TravelCartView cart;
        public TravelAccountView account;
        public TMP_Text confirmationText;
        public UnityEngine.UI.Button confirmButton;
        public AppServices Services { get; private set; }
        [SerializeField, HideInInspector] TravelPage activePage;
        public TravelPage ActivePage => activePage;
        TravelAppRuntime runtime;
        string cancellationId;
        bool attached;
        void Start() { Attach(SceneManager.GetActiveScene().name); }
        public void Attach(string scene)
        {
            if (attached) return;
            runtime = TravelAppRuntime.Instance;
            if (runtime == null || runtime.Services == null) { feedback.text = "Travel catalog could not be loaded."; return; }
            attached = true; Current = this; Services = runtime.Services;
            auth.Bind(this); catalog.Bind(this); details.Bind(this); cart.Bind(this); account.Bind(this);
            runtime.Message += Notify; Services.Changed += RefreshIdentity;
            RefreshIdentity(); Notify(runtime.StorageWarning ?? "Welcome to Waypoint. Your plans, all in one place.");
            ShowPage(initialPage);
        }
        public void PreviewPage(TravelPage page)
        {
            activePage = page;
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == (int)page);
        }
        public void ShowPage(TravelPage page)
        {
            if (!attached) return;
            if (page != TravelPage.Login && page != TravelPage.Register && !Services.Accounts.IsLoggedIn) page = TravelPage.Login;
            PreviewPage(page);
            if (page == TravelPage.Catalog) catalog.Search();
            if (page == TravelPage.Cart || page == TravelPage.Checkout) cart.Refresh();
            if (page == TravelPage.Account) account.Refresh();
        }
        public void LoginPage() => ShowPage(TravelPage.Login);
        public void RegisterPage() => ShowPage(TravelPage.Register);
        public void Home() => ShowPage(TravelPage.Home);
        public void Browse() => ShowPage(TravelPage.Catalog);
        public void Cart() => ShowPage(TravelPage.Cart);
        public void Account() => ShowPage(TravelPage.Account);
        public void Checkout() => ShowPage(TravelPage.Checkout);
        public void GoHome() { runtime.Navigate("HomeScreen"); }
        public void GoSearch() { runtime.Navigate("BookingScreen"); }
        public void GoAccount() { runtime.Navigate("AccountScreen"); }
        public void Logout()
        {
            auth.ClearPasswords(); details.ClearReview();
            Services.Accounts.Logout(); runtime.Navigate("LoginScreen");
        }
        public void ShowProduct(Product product, string start, string end)
        {
            if (!Services.Accounts.IsLoggedIn) { ShowPage(TravelPage.Login); return; }
            details.Show(product, start, end); ShowPage(TravelPage.Details);
        }
        public void ConfirmCancellation(string id)
        {
            cancellationId = id; confirmationText.text = "Cancel this booking? Your receipt will remain in your history.";
            ShowPage(TravelPage.Confirmation);
        }
        public void CancelBooking()
        {
            var result = Services.Bookings.Cancel(cancellationId); Report(result);
            if (result.success) ShowPage(TravelPage.Account);
        }
        public void Report(Result result) { runtime.Show(result); }
        void Notify(string message) { if (feedback != null) feedback.text = message; }
        void RefreshIdentity()
        {
            bool loggedIn = Services.Accounts.IsLoggedIn;
            identity.text = loggedIn ? Services.Accounts.Username : "Your next adventure";
            cartCount.text = loggedIn ? Services.Bookings.Cart.Count + " items in your cart" : "Plan at your own pace";
            signedInNavigation.SetActive(loggedIn); signedOutNavigation.SetActive(!loggedIn);
            foreach (var button in signedInButtons) if (button != null) button.interactable = loggedIn;
        }
        void OnDestroy()
        {
            if (runtime != null) runtime.Message -= Notify;
            if (Services != null) Services.Changed -= RefreshIdentity;
            if (Current == this) Current = null;
        }
    }
}
