using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TravelApp;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;

public static class EditableTravelUIBuilder
{
    const string Folder = "Assets/UI/Prefabs";
    static readonly Color Ink = Hex("172E3B"), Muted = Hex("607379"), Cream = Hex("F5F4EE"), Teal = Hex("176B62"), Mint = Hex("D9E9E1"), Coral = Hex("E9B49A"), Line = Hex("D8DFD9");
    static TMP_FontAsset font;
    static TravelItemView productPrefab, cartPrefab, bookingPrefab;
    [MenuItem("Travel App/Create Editable UI (replaces four scenes)")]
    static void ConfirmBuild()
    {
        if (!EditorUtility.DisplayDialog("Replace travel UI?", "This rebuilds AppShell and the four travel scenes. Back up custom UI changes first. Normal editing never requires this command.", "Rebuild", "Cancel")) return;
        Build();
    }
    public static void Build()
    {
        TravelCatalogBuilder.Synchronize();
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        productPrefab = ItemPrefab("ProductCard", 210);
        cartPrefab = ItemPrefab("CartItem", 180);
        bookingPrefab = ItemPrefab("BookingCard", 210);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = Rect("WaypointApp", null);
        var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 960); scaler.matchWidthOrHeight = .5f;
        root.gameObject.AddComponent<GraphicRaycaster>(); Image(root, Cream);
        var screen = root.gameObject.AddComponent<TravelScreenUI>();
        screen.auth = root.gameObject.AddComponent<TravelAuthView>();
        screen.catalog = root.gameObject.AddComponent<TravelCatalogView>();
        screen.details = root.gameObject.AddComponent<TravelDetailsView>();
        screen.cart = root.gameObject.AddComponent<TravelCartView>();
        screen.account = root.gameObject.AddComponent<TravelAccountView>();
        // Presenters live on their corresponding panels below, except shared auth/cart presenters.
        BuildSidebar(root, screen);
        var header = Rect("PageHeader", root); Anchor(header, new Vector2(0,1), Vector2.one, new Vector2(270,-52), new Vector2(-36,-30));
        Vertical(header, 0); Text(header, "YOUR NEXT CHAPTER", 13, Teal, 20, FontStyles.Bold);
        var pageRoot = Rect("Screens - select a panel to edit", root); Stretch(pageRoot); pageRoot.offsetMin = new Vector2(270,90); pageRoot.offsetMax = new Vector2(-36,-150);
        screen.pages = new GameObject[9]; screen.pageHeadings = new TMP_Text[9]; screen.pageSubtitles = new TMP_Text[9];
        string[] titles = { "Welcome back", "A little adventure awaits", "Where will you go next?", "Find your next experience", "A closer look", "Your travel plans", "Ready when you are", "Your journey so far", "One last check" };
        string[] descriptions = { "Sign in and pick up where you left off.", "Create an account to start planning.", "Good trips start with a little curiosity.", "Flights, stays, dining and moments worth remembering.", "All the details, before you decide.", "A few great choices, ready for your next trip.", "Review your dates and confirm your plans.", "Keep track of every booking and memory.", "Review this change before continuing." };
        for (int i = 0; i < screen.pages.Length; i++)
        {
            var page = Rect(((TravelPage)i) + "Screen", pageRoot); Stretch(page); screen.pages[i] = page.gameObject;
            var title = Rect("EditablePageHeading", page); Anchor(title, new Vector2(0,1), Vector2.one, new Vector2(0,8), new Vector2(0,96));
            title.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; Vertical(title, 5);
            screen.pageHeadings[i] = Text(title, titles[i], 34, Ink, 46, FontStyles.Bold);
            screen.pageSubtitles[i] = Text(title, descriptions[i], 17, Muted, 27);
        }
        BuildLogin((RectTransform)screen.pages[0].transform, screen);
        BuildRegister((RectTransform)screen.pages[1].transform, screen);
        BuildHome((RectTransform)screen.pages[2].transform, screen);
        // Move component ownership to panels so debounce lifetimes follow visible screens.
        UnityEngine.Object.DestroyImmediate(screen.catalog);
        screen.catalog = screen.pages[3].AddComponent<TravelCatalogView>();
        BuildCatalog((RectTransform)screen.pages[3].transform, screen.catalog);
        UnityEngine.Object.DestroyImmediate(screen.details); screen.details = screen.pages[4].AddComponent<TravelDetailsView>();
        BuildDetails((RectTransform)screen.pages[4].transform, screen);
        BuildCart((RectTransform)screen.pages[5].transform, screen, false);
        BuildCart((RectTransform)screen.pages[6].transform, screen, true);
        UnityEngine.Object.DestroyImmediate(screen.account); screen.account = screen.pages[7].AddComponent<TravelAccountView>();
        BuildAccount((RectTransform)screen.pages[7].transform, screen);
        BuildConfirmation((RectTransform)screen.pages[8].transform, screen);
        // Home category callbacks are added after the catalog presenter exists.
        WireCategory(root, "StaysAction", screen.catalog.Hotels);
        WireCategory(root, "FlightsAction", screen.catalog.Flights);
        WireCategory(root, "DiningAction", screen.catalog.Dining);
        var status = Rect("FeedbackBar", root); Anchor(status, Vector2.zero, new Vector2(1,0), new Vector2(270,20), new Vector2(-36,69));
        Image(status, Color.white); Padding(status, 16, 10);
        screen.feedback = Text(status, "Welcome to Waypoint. Your plans, all in one place.", 15, Muted, 28);
        screen.initialPage = TravelPage.Login; screen.PreviewPage(TravelPage.Login);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "/AppShell.prefab");
        UnityEngine.Object.DestroyImmediate(root.gameObject);
        string[] names = { "LoginScreen", "HomeScreen", "AccountScreen", "BookingScreen" };
        TravelPage[] initial = { TravelPage.Login, TravelPage.Home, TravelPage.Account, TravelPage.Catalog };
        for (int i = 0; i < names.Length; i++)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var view = instance.GetComponent<TravelScreenUI>(); view.initialPage = initial[i]; view.PreviewPage(initial[i]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            foreach (var go in view.pages) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.heading); PrefabUtility.RecordPrefabInstancePropertyModifications(view.subtitle);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = events.GetComponent<InputSystemUIInputModule>(); module.AssignDefaultActions();
            var camera = new GameObject("Main Camera", typeof(Camera)); camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor; camera.GetComponent<Camera>().backgroundColor = Cream;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + names[i] + ".unity");
        }
        EditorSceneManager.OpenScene("Assets/Scenes/LoginScreen.unity");
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Editable UI created: AppShell, three item prefabs, four scenes. No UI is constructed at runtime.");
    }
    static void WireCategory(RectTransform root, string name, UnityAction action)
    {
        var button = root.GetComponentsInChildren<UIButton>(true).First(b => b.name == name); Wire(button, action);
    }
    static void BuildSidebar(RectTransform root, TravelScreenUI screen)
    {
        var side = Rect("Sidebar", root); Anchor(side, Vector2.zero, new Vector2(0,1), Vector2.zero, new Vector2(232,0)); Image(side, Ink);
        var brand = Rect("Brand", side); Anchor(brand, new Vector2(0,1), Vector2.one, new Vector2(24,-130), new Vector2(-24,-36)); Vertical(brand, 8);
        Text(brand, "WAYPOINT", 26, Color.white, 38, FontStyles.Bold); Text(brand, "A little further from ordinary.", 13, Hex("B6CDC8"), 42);
        var nav = Rect("Navigation", side); Anchor(nav, new Vector2(0,1), Vector2.one, new Vector2(16,-510), new Vector2(-16,-180)); Vertical(nav, 14);
        Text(nav, "YOUR TRAVEL SPACE", 12, Hex("A6BDB8"), 24, FontStyles.Bold);
        var home = Button(nav, "Home", "Home", screen.GoHome, true);
        var search = Button(nav, "Explore", "Explore", screen.GoSearch, true);
        var cart = Button(nav, "Cart", "Your cart", screen.Cart, true);
        var account = Button(nav, "Account", "Your bookings", screen.GoAccount, true);
        screen.signedInButtons = new[] { home, search, cart, account };
        var profile = Rect("Profile", side); Anchor(profile, Vector2.zero, new Vector2(1,0), new Vector2(22,40), new Vector2(-22,218)); Vertical(profile, 8);
        screen.identity = Text(profile, "Your next adventure", 18, Color.white, 30, FontStyles.Bold);
        screen.cartCount = Text(profile, "Plan at your own pace", 13, Hex("BCD2CD"), 28);
        var signedIn = Rect("SignedInActions", profile); Vertical(signedIn, 4); screen.signedInNavigation = signedIn.gameObject;
        Button(signedIn, "Log out", "Log out", screen.Logout, true);
        var signedOut = Rect("SignedOutActions", profile); Vertical(signedOut, 4); screen.signedOutNavigation = signedOut.gameObject;
        Button(signedOut, "SignInNavigation", "Sign in", screen.LoginPage, true);
        signedIn.gameObject.SetActive(false);
    }
    static void BuildLogin(RectTransform page, TravelScreenUI screen)
    {
        var content = Scroll(page, "LoginScroll");
        var row = Row(content, 720, 26); var hero = Box(row, "WelcomeArtwork", Mint, 1); Vertical(hero, 18, 34);
        Text(hero, "THE WORLD CAN WAIT.\nYOUR NEXT TRIP DOESN'T HAVE TO.", 32, Ink, 150, FontStyles.Bold);
        Text(hero, "Find a place to stay, a new favorite meal, or an experience that makes the whole trip.", 20, Muted, 110);
        var artwork = Rect("ReplaceableArtwork", hero); Image(artwork, Hex("A5CBC0")); Height(artwork, 150);
        var artLayout = artwork.gameObject.AddComponent<HorizontalLayoutGroup>(); artLayout.padding = new RectOffset(18,18,20,20); artLayout.spacing = 14; artLayout.childControlWidth = true;
        Text(artwork, "01\nEXPLORE", 21, Teal, 95, FontStyles.Bold); Text(artwork, "02\nPLAN", 21, Teal, 95, FontStyles.Bold); Text(artwork, "03\nGO", 21, Teal, 95, FontStyles.Bold);
        var form = Box(row, "SignInForm", Color.white, 1); Vertical(form, 16, 30);
        Text(form, "Make yourself at home.", 28, Ink, 76, FontStyles.Bold);
        Text(form, "Your travel plans are right where you left them.", 17, Muted, 65);
        screen.auth.loginUsername = Input(form, "UsernameInput", "Username");
        screen.auth.loginPassword = Input(form, "PasswordInput", "Password", true);
        Button(form, "Sign in", "Sign in", screen.auth.Login);
        Text(form, "New around here?", 15, Muted, 26);
        Button(form, "Create account", "Create an account", screen.RegisterPage, false);
        Text(form, "Offline classroom demo. No real payments or reservations.", 13, Muted, 52);
    }
    static void BuildRegister(RectTransform page, TravelScreenUI screen)
    {
        var content = Scroll(page, "RegistrationScroll"); var form = Box(content, "RegistrationForm", Color.white); Vertical(form, 16, 30);
        Text(form, "Start your next chapter.", 28, Ink, 48, FontStyles.Bold);
        Text(form, "One local account. All your plans in one place.", 17, Muted, 36);
        screen.auth.registerUsername = Input(form, "RegisterUsername", "Username (3-40 characters)");
        screen.auth.registerEmail = Input(form, "RegisterEmail", "Email address");
        screen.auth.registerPassword = Input(form, "RegisterPassword", "Password (4+ characters)", true);
        Button(form, "Register account", "Create my account", screen.auth.Register);
        Button(form, "Back to sign in", "Already have an account? Sign in", screen.LoginPage, false);
    }
    static void BuildHome(RectTransform page, TravelScreenUI screen)
    {
        var content = Scroll(page, "HomeScroll");
        var hero = Box(content, "HomeHero", Mint); Vertical(hero, 14, 28);
        Text(hero, "A CHANGE OF SCENERY", 13, Teal, 25, FontStyles.Bold);
        Text(hero, "Less planning.\nMore possibilities.", 40, Ink, 112, FontStyles.Bold);
        Text(hero, "Find the small moments that make a great trip.", 18, Muted, 35);
        Button(hero, "Browse all travel products", "Explore the collection", screen.Browse);
        Text(content, "A trip, your way.", 26, Ink, 44, FontStyles.Bold);
        var row = Row(content, 300, 18);
        Category(row, "StaysAction", "01 / STAY", "Somewhere to slow down.", "Find your home away from home.", Mint);
        Category(row, "FlightsAction", "02 / GO", "A new point of view.", "Start with a flight somewhere new.", Hex("E1E7EC"));
        Category(row, "DiningAction", "03 / TASTE", "The best kind of detour.", "Make room for a memorable meal.", Hex("F2E1D4"));
        var note = Box(content, "PlanningNote", Color.white); Vertical(note, 10, 20);
        Text(note, "A simple plan goes a long way.", 21, Ink, 34, FontStyles.Bold);
        Text(note, "Choose your dates, add your favorites to the cart, then confirm when you're ready. Everything is saved locally.", 17, Muted, 70);
    }
    static void Category(RectTransform parent, string name, string eyebrow, string title, string description, Color color)
    {
        var box = Box(parent, name + "Card", color, 1); Vertical(box, 12, 20);
        Text(box, eyebrow, 13, Teal, 24, FontStyles.Bold); Text(box, title, 23, Ink, 70, FontStyles.Bold);
        Text(box, description, 15, Muted, 52); Button(box, name, "Explore", null, false);
    }
    static void BuildCatalog(RectTransform page, TravelCatalogView view)
    {
        Vertical(page, 12);
        var filters = Box(page, "EditableFilters", Color.white); Vertical(filters, 12, 18);
        var search = Row(filters, 92, 16);
        view.searchInput = Input(search, "SearchInput", "Search destinations or experiences");
        view.typeDropdown = Dropdown(search, "TypeDropdown", "Category", new[] { "All experiences", "Hotels", "Flights", "Restaurants", "Experiences" });
        view.sortDropdown = Dropdown(search, "SortDropdown", "Sort by", new[] { "Price: low to high", "Price: high to low", "Rating: low to high", "Rating: high to low", "Date: latest", "Date: earliest" });
        var price = Row(filters, 92, 12);
        view.minPriceInput = Input(price, "MinPriceInput", "Minimum price"); view.maxPriceInput = Input(price, "MaxPriceInput", "Maximum price");
        view.ratingDropdown = Dropdown(price, "RatingDropdown", "Minimum rating", new[] { "Any rating", "1+ stars", "2+ stars", "3+ stars", "4+ stars", "5 stars" });
        view.startDateInput = Input(price, "StartDateInput", "From (YYYY-MM-DD)"); view.endDateInput = Input(price, "EndDateInput", "To (YYYY-MM-DD)");
        var controls = Row(filters, 46, 14); Button(controls, "Search", "Find my next experience", view.Search); Button(controls, "Reset filters", "Reset filters", view.ResetFilters, false);
        view.resultCount = Text(page, "Experiences to explore", 18, Ink, 34, FontStyles.Bold);
        var list = Rect("CatalogResults", page); list.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        view.results = ItemList(list, productPrefab, "No experiences match these filters. Try broadening your search.");
    }
    static void BuildDetails(RectTransform page, TravelScreenUI screen)
    {
        var view = screen.details; var content = Scroll(page, "ProductDetailsScroll");
        Button(content, "Back to collection", "Back to the collection", screen.Browse, false);
        var box = Box(content, "ProductSummary", Color.white); Vertical(box, 12, 22);
        view.category = Text(box, "HOTEL / DESTINATION", 14, Teal, 26, FontStyles.Bold);
        view.productName = Text(box, "Your next favorite place", 32, Ink, 54, FontStyles.Bold);
        var image = Rect("ProductArtwork", box); Height(image, 165); Image(image, Mint);
        var photo = Rect("ProductImage", image); Stretch(photo); view.productImage = Image(photo, Color.white); view.productImage.preserveAspect = true; view.productImage.enabled = false;
        view.description = Text(box, "A detailed description of this travel experience appears here.", 18, Muted, 100);
        view.price = Text(box, "$125.00 per booking", 23, Teal, 40, FontStyles.Bold);
        view.availability = Text(box, "Availability dates", 15, Muted, 34);
        var dates = Row(box, 92, 16); view.startDateInput = Input(dates, "BookingStartDate", "Start date (YYYY-MM-DD)"); view.endDateInput = Input(dates, "BookingEndDate", "End date (YYYY-MM-DD)");
        Text(box, "Flat price per product. Dates select availability; no nightly or per-person charges.", 14, Muted, 50);
        var actions = Row(box, 48, 14); Button(actions, "Add to cart", "Add to my plans", view.AddToCart); Button(actions, "Open cart", "View my cart", screen.Cart, false);
        var reviews = Box(content, "Reviews", Color.white); Vertical(reviews, 14, 22);
        Text(reviews, "A few words from fellow travelers.", 23, Ink, 44, FontStyles.Bold);
        view.reviews = Text(reviews, "Reviews will appear here.", 17, Muted, 66); view.reviews.gameObject.GetComponent<LayoutElement>().preferredHeight = -1;
        var reviewForm = Rect("ReviewForm", reviews); Vertical(reviewForm, 12); view.reviewForm = reviewForm.gameObject;
        view.ratingInput = Input(reviewForm, "ReviewRating", "Your rating (1-5)");
        view.reviewInput = Input(reviewForm, "ReviewText", "Tell us about your experience"); view.reviewInput.characterLimit = 1000; view.reviewInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        view.reviewInput.GetComponent<LayoutElement>().preferredHeight = 120;
        Height((RectTransform)view.reviewInput.transform.parent,160);
        view.reviewInput.textComponent.textWrappingMode = TextWrappingModes.Normal;
        Button(reviewForm, "Save review", "Save my review", view.SubmitReview);
    }
    static void BuildCart(RectTransform page, TravelScreenUI screen, bool checkout)
    {
        Vertical(page, 14);
        var note = Box(page, "Summary", checkout ? Mint : Color.white); Vertical(note, 10, 20);
        Text(note, checkout ? "Everything look right?" : "Good choices, all together.", 25, Ink, 44, FontStyles.Bold);
        Text(note, checkout ? "Double-check the products and dates below. This is a simulated booking; no payment will be taken." : "Your selected experiences are saved here until you're ready.", 17, Muted, 58);
        var list = Rect(checkout ? "CheckoutItems" : "CartItems", page); list.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        var items = ItemList(list, cartPrefab, "Your cart is waiting for a little adventure. Explore the collection to begin.");
        var totals = Box(page, "BookingTotal", Color.white); var row = Row(totals, 80, 22); row.gameObject.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(20,20,12,12);
        Text(row, "TOTAL / FLAT BOOKING PRICES", 13, Muted, 40, FontStyles.Bold);
        var total = Text(row, "$0.00", 32, Teal, 48, FontStyles.Bold);
        if (checkout)
        {
            screen.cart.checkoutItems = items; screen.cart.checkoutTotal = total;
            screen.cart.confirmButton = Button(row, "Confirm simulated booking", "Confirm my booking", screen.cart.ConfirmCheckout);
            Button(page, "Back to cart", "Back to my cart", screen.Cart, false);
        }
        else
        {
            screen.cart.cartItems = items; screen.cart.cartTotal = total;
            screen.cart.reviewButton = Button(row, "Review checkout", "Review & book", screen.Checkout);
            Button(page, "Continue exploring", "Keep exploring", screen.Browse, false);
        }
    }
    static void BuildAccount(RectTransform page, TravelScreenUI screen)
    {
        Vertical(page, 16); var profile = Box(page, "AccountSummary", Mint); Vertical(profile, 8, 22);
        Text(profile, "YOUR TRAVEL JOURNAL", 13, Teal, 26, FontStyles.Bold);
        screen.account.accountName = Text(profile, "Your account", 28, Ink, 44, FontStyles.Bold);
        screen.account.bookingCount = Text(profile, "All your bookings, in one place.", 17, Muted, 34);
        var list = Rect("BookingHistory", page); list.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        screen.account.bookings = ItemList(list, bookingPrefab, "Your travel story starts here. Once you book, your plans will appear in this space.");
        Button(page, "Explore more", "Find your next experience", screen.Browse);
    }
    static void BuildConfirmation(RectTransform page, TravelScreenUI screen)
    {
        var content = Scroll(page, "ConfirmationScroll"); var box = Box(content, "ConfirmationCard", Color.white); Vertical(box, 20, 32);
        Text(box, "A CHANGE OF PLANS?", 14, Teal, 28, FontStyles.Bold);
        screen.confirmationText = Text(box, "Cancel this booking? Your receipt will remain in your history.", 24, Ink, 110, FontStyles.Bold);
        screen.confirmButton = Button(box, "Yes, cancel booking", "Yes, cancel this booking", screen.CancelBooking);
        Button(box, "Keep booking", "Keep my booking", screen.Account, false);
    }
    static TravelItemView ItemPrefab(string name, int height)
    {
        var root = Rect(name, null); Image(root, Color.white); Height(root, height); root.sizeDelta = new Vector2(1020,height);
        var row = root.gameObject.AddComponent<HorizontalLayoutGroup>(); row.padding = new RectOffset(18,18,18,18); row.spacing = 20; row.childForceExpandWidth = false; row.childControlWidth = true; row.childControlHeight = true;
        var image = Rect("Artwork - drop your sprite here", root); Image(image, Mint); Width(image, 145);
        var middle = Rect("Information", root); middle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1; Vertical(middle, 6);
        var view = root.gameObject.AddComponent<TravelItemView>();
        var photo = Rect("ProductImage", image); Stretch(photo); view.image = Image(photo, Color.white); view.image.preserveAspect = true; view.image.enabled = false;
        view.eyebrow = Text(middle, "STAY / SOMEWHERE SPECIAL", 12, Teal, 24, FontStyles.Bold);
        view.title = Text(middle, "A new favorite place", 24, Ink, 42, FontStyles.Bold);
        view.description = Text(middle, "Room for a little adventure.\nYour travel details will appear here.", 16, Muted, 74); view.description.overflowMode = TextOverflowModes.Ellipsis;
        var actions = Rect("Actions", root); Width(actions, 190); Vertical(actions, 8);
        view.price = Text(actions, "$125.00 / booking", 20, Ink, 45, FontStyles.Bold);
        view.primaryButton = Button(actions, "PrimaryAction", "Explore & book", null); view.primaryLabel = view.primaryButton.GetComponentInChildren<TMP_Text>();
        view.secondaryButton = Button(actions, "SecondaryAction", "View & review", null, false); view.secondaryLabel = view.secondaryButton.GetComponentInChildren<TMP_Text>();
        view.secondaryButton.gameObject.SetActive(false);
        if (name == "BookingCard")
        {
            root.GetComponent<LayoutElement>().preferredHeight = -1;
            view.description.GetComponent<LayoutElement>().preferredHeight = -1;
        }
        var asset = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root.gameObject); return asset.GetComponent<TravelItemView>();
    }
    static TravelItemList ItemList(RectTransform parent, TravelItemView template, string empty)
    {
        var content = Scroll(parent, "ItemsScroll");
        var list = parent.gameObject.AddComponent<TravelItemList>(); list.content = content; list.itemPrefab = template;
        var sample = (GameObject)PrefabUtility.InstantiatePrefab(template.gameObject); sample.name = "EditorPreview - sample card"; sample.transform.SetParent(content, false); list.editorPreview = sample;
        var emptyBox = Box(content, "EmptyState", Mint); Vertical(emptyBox, 14, 28); Text(emptyBox, "Room for something wonderful.", 24, Ink, 54, FontStyles.Bold); Text(emptyBox, empty, 18, Muted, 110);
        list.emptyState = emptyBox.gameObject; emptyBox.gameObject.SetActive(false); return list;
    }
    static RectTransform Scroll(RectTransform parent, string name)
    {
        var root = Rect(name, parent); Stretch(root);
        if (parent.GetComponent<LayoutGroup>() != null) root.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
        var viewport = Rect("Viewport", root); Stretch(viewport); Image(viewport, Color.white); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = Rect("Content", viewport); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
        Vertical(content, 18); content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; return content;
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); if (parent != null) go.transform.SetParent(parent,false); return (RectTransform)go.transform;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#"+hex, out var color); return color; }
    static UIImage Image(RectTransform rect, Color color) { var image = rect.gameObject.AddComponent<UIImage>(); image.color = color; return image; }
    static void Height(RectTransform rect, float value) { var e = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.minHeight = value; e.preferredHeight = value; e.flexibleHeight = 0; }
    static void Width(RectTransform rect, float value) { var e = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); e.minWidth = value; e.preferredWidth = value; e.flexibleWidth = 0; }
    static void Vertical(RectTransform rect, int spacing, int padding = 0)
    {
        var l = rect.gameObject.AddComponent<VerticalLayoutGroup>(); l.spacing = spacing; l.padding = new RectOffset(padding,padding,padding,padding); l.childControlHeight = true; l.childControlWidth = true; l.childForceExpandWidth = true; l.childForceExpandHeight = false;
    }
    static void Padding(RectTransform rect, int x, int y) { Vertical(rect,0); rect.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(x,x,y,y); }
    static RectTransform Row(Transform parent, float height, float spacing)
    {
        var row = Rect("Row", parent); Height(row,height); var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = spacing; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = true; return row;
    }
    static RectTransform Box(Transform parent, string name, Color color, float flex = 0)
    {
        var box = Rect(name,parent); Image(box,color); if (flex>0) box.gameObject.AddComponent<LayoutElement>().flexibleWidth = flex; return box;
    }
    static TMP_Text Text(Transform parent, string text, int size, Color color, float height, FontStyles style = FontStyles.Normal)
    {
        var rect = Rect("Text - "+text.Split('\n')[0],parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
        label.text = text; label.fontSize = size; label.fontStyle = style; label.color = color; label.richText = false; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis; label.margin = Vector4.zero;
        Height(rect,height); return label;
    }
    static UIButton Button(Transform parent, string name, string label, UnityAction action, bool primary = true)
    {
        var rect = Rect(name,parent); var image = Image(rect, primary ? Teal : Hex("E7EEE8")); Height(rect,46);
        var button = rect.gameObject.AddComponent<UIButton>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = Hex("D7EAE4"); colors.pressedColor = Hex("A9CDC0"); colors.disabledColor = Hex("B5C1BB"); colors.fadeDuration = .12f; button.colors = colors;
        var text = Text(rect,label,16,primary ? Color.white : Teal,36,FontStyles.Bold); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(12,5); text.rectTransform.offsetMax = new Vector2(-12,-5); text.alignment = TextAlignmentOptions.Center;
        if (action != null) Wire(button,action); return button;
    }
    static void Wire(UIButton button, UnityAction action) { UnityEventTools.AddPersistentListener(button.onClick,action); }
    static TMP_InputField Input(Transform parent, string name, string label, bool password = false)
    {
        var group = Rect(name+"Field",parent); Vertical(group,6); Height(group,90); group.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1;
        Text(group,label,14,Muted,25,FontStyles.Bold);
        var rect = Rect(name,group); var image = Image(rect,Hex("F0F3EE")); Height(rect,50);
        var input = rect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = image; input.characterLimit = 128;
        var viewport = Rect("TextArea",rect); Stretch(viewport); viewport.offsetMin = new Vector2(12,8); viewport.offsetMax = new Vector2(-12,-8); viewport.gameObject.AddComponent<RectMask2D>();
        var text = Text(viewport,"",18,Ink,35); Stretch(text.rectTransform); text.textWrappingMode = TextWrappingModes.NoWrap;
        var placeholder = Text(viewport,password ? "Enter password" : "Enter "+label.ToLowerInvariant(),16,Hex("7E8E8C"),35); Stretch(placeholder.rectTransform);
        input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
        if (password) input.contentType = TMP_InputField.ContentType.Password;
        return input;
    }
    static TMP_Dropdown Dropdown(Transform parent, string name, string label, string[] options)
    {
        var group = Rect(name+"Field",parent); Vertical(group,6); Height(group,90); group.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1;
        Text(group,label,14,Muted,25,FontStyles.Bold);
        var rect = Rect(name,group); var image = Image(rect,Hex("F0F3EE")); Height(rect,50);
        var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>(); dropdown.targetGraphic = image;
        var caption = Text(rect,options[0],15,Ink,34); Stretch(caption.rectTransform); caption.rectTransform.offsetMin = new Vector2(12,8); caption.rectTransform.offsetMax = new Vector2(-20,-8); dropdown.captionText = caption;
        var arrow = Text(rect,"v",12,Muted,30); Anchor(arrow.rectTransform, new Vector2(1,0), Vector2.one,new Vector2(-20,10),new Vector2(-6,-10));
        var template = Rect("Template",rect); template.anchorMin = new Vector2(0,0); template.anchorMax = new Vector2(1,0); template.pivot = new Vector2(.5f,1); template.sizeDelta = new Vector2(0,220); template.anchoredPosition = new Vector2(0,-4); Image(template,Color.white);
        var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Rect("Viewport",template); Stretch(viewport); Image(viewport,Color.white); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = Rect("Content",viewport); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1); content.sizeDelta = new Vector2(0,40);
        var item = Rect("Item",content); item.anchorMin = new Vector2(0,0); item.anchorMax = new Vector2(1,0); item.sizeDelta = new Vector2(0,40); item.pivot = Vector2.zero;
        var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = Image(item,Hex("E5EEE6"));
        var check = Rect("Checkmark",item); Anchor(check,Vector2.zero,new Vector2(0,1),new Vector2(4,12),new Vector2(10,-12)); toggle.graphic = Image(check,Teal);
        var itemText = Text(item,"Option",15,Ink,36); Stretch(itemText.rectTransform); itemText.rectTransform.offsetMin = new Vector2(16,4); itemText.rectTransform.offsetMax = new Vector2(-8,-4);
        dropdown.template = template; dropdown.itemText = itemText; scroll.viewport = viewport; scroll.content = content;
        dropdown.ClearOptions(); dropdown.AddOptions(options.ToList()); template.gameObject.SetActive(false); return dropdown;
    }
}
