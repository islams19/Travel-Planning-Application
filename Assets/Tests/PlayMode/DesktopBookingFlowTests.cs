using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UIButton = UnityEngine.UI.Button;

public class DesktopBookingFlowTests
{
    sealed class MemoryStore : ISaveStore
    {
        public SaveData Load() => new SaveData();
        public void Save(SaveData data) { }
    }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    AppServices original;
    TravelCatalog catalog;
    User user;
    SidebarNavigation nav;
    AppServices services;
    string testUsername;
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static T Page<T>() where T : MonoBehaviour => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
    static UIButton FindButton(Transform root, string name) => root.GetComponentsInChildren<UIButton>(true).Where(b => b.name == name).OrderByDescending(b => b.gameObject.activeInHierarchy).First();
    static void Click(Transform root, string name)
    {
        var button = FindButton(root, name);
        Assert.IsTrue(button.interactable, name + " should be enabled");
        Assert.IsTrue(button.gameObject.activeInHierarchy, name + " should be visible");
        button.onClick.Invoke();
    }
    void Check(string active)
    {
        Assert.IsTrue(services.Accounts.IsLoggedIn);
        Assert.AreEqual(testUsername, services.Accounts.Username);
        Assert.AreEqual(1, Object.FindObjectsByType<TravelAppRuntime>(FindObjectsSortMode.None).Length);
        var profile = Page<SharedProfileMenu>();
        var avatar = GameObject.Find("header_avatar").GetComponent<UIButton>();
        Assert.IsNotNull(avatar, "Shared avatar must be clickable on every booking page");
        avatar.onClick.Invoke();
        Assert.IsTrue(profile.IsOpen);
        Assert.AreEqual(testUsername, profile.GetComponentInParent<Canvas>().GetComponentsInChildren<TMP_Text>(true)
            .First(t => t.name == "Signed-in name").text);
        Assert.AreEqual("flow@example.com", services.Accounts.Email);
        profile.Close();
        var pages = new[] { "searchflights_page", "Flight Results", "Seat Selection", "Return Flight Results", "Return Seat Selection", "Trip Review", "Payment", "Booking Confirmation" };
        Assert.IsNotNull(GameObject.Find(active), "Expected page " + active);
        foreach (var page in pages.Where(p => p != active))
            Assert.IsNull(GameObject.Find(page), "Inactive page must not intercept input: " + page);
    }
    IEnumerator StartLogin(bool roundTrip)
    {
        testUsername = roundTrip ? "FlowRoundTrip" : "FlowOneWay";
        yield return null;
        var runtime = TravelAppRuntime.Instance;
        original = runtime.Services;
        catalog = ScriptableObject.CreateInstance<TravelCatalog>();
        user = ScriptableObject.CreateInstance<User>();
        user.username = testUsername; user.password = "test-pass"; user.email = "flow@example.com";
        catalog.users.Add(user);
        services = new AppServices(catalog, new MemoryStore());
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime, services);
        yield return SceneManager.LoadSceneAsync("Login");
        yield return null;
        var login = Page<DesktopLoginUI>();
        Field<TMP_InputField>(login, "username").text = testUsername;
        Field<TMP_InputField>(login, "password").text = "test-pass";
        Field<UIButton>(login, "submit").onClick.Invoke();
        float deadline = Time.realtimeSinceStartup + 10;
        while ((SceneManager.GetActiveScene().name != "DesktopLayout" || runtime.IsNavigating) && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null; yield return null;
        Assert.AreEqual("DesktopLayout", SceneManager.GetActiveScene().name);
        nav = Page<SidebarNavigation>();
        Check("searchflights_page");
    }
    void SidebarRoundTrip(string resume)
    {
        var outbound = nav.CurrentSeatBooking;
        var inbound = nav.CurrentReturnSeatBooking;
        var buttons = Field<UIButton[]>(nav, "buttons");
        foreach (int i in new[] { 1, 2, 3, 0 })
        {
            buttons[i].onClick.Invoke();
            Assert.AreSame(outbound, nav.CurrentSeatBooking);
            Assert.AreSame(inbound, nav.CurrentReturnSeatBooking);
            Assert.IsTrue(services.Accounts.IsLoggedIn);
        }
        Click(GameObject.Find("searchflights_page").transform, "Search Flights");
        Check("Flight Results");
        if (resume == "search") return;
        var results = Field<FlightResultsPage>(nav, "flightResults");
        Click(results.transform.Find("Flight results scroll/Viewport/Flight cards/JetBlue"), "Select");
        Check("Seat Selection");
        Assert.AreSame(outbound, nav.CurrentSeatBooking, "Unchanged search must retain departure seats");
        Click(Field<SeatSelectionPage>(nav,"seatPage").transform, "Confirm Seat");
        if (inbound != null)
        {
            var returns = Field<FlightResultsPage>(nav,"returnResults");
            Assert.IsNotNull(returns.SelectedDeparture, "Return selection survives unchanged departure confirmation");
            Click(returns.transform.Find("Flight results scroll/Viewport/Flight cards/Delta Air Lines"), "Select");
            Assert.AreSame(inbound, nav.CurrentReturnSeatBooking);
            Click(Field<SeatSelectionPage>(nav,"returnSeats").transform, "Confirm Seat");
        }
        Check("Trip Review");
    }
    IEnumerator Run(bool roundTrip)
    {
        yield return StartLogin(roundTrip);
        var search = GameObject.Find("searchflights_page").transform;
        Click(search, "card-Tokyo");
        Assert.AreEqual("New York, New York (JFK)", Field<DestinationAutocomplete>(nav,"originSearch").SelectedDestination);
        Click(search, "Departure date");
        var dates = Page<DateSelectionPage>();
        if (!roundTrip) Click(dates.transform,"Trip type");
        var days = Field<UIButton[]>(dates,"dayButtons").Where(b=>b.gameObject.activeSelf).ToArray();
        days[9].onClick.Invoke();
        if (roundTrip) days[14].onClick.Invoke();
        var departure = Field<DateTime?>(dates,"departureDate").Value;
        var returning = Field<DateTime?>(dates,"returnDate");
        Click(dates.transform,"Confirm Dates");
        Click(GameObject.Find("searchflights_page").transform,"Search Flights");
        Check("Flight Results");
        var flights = Field<FlightResultsPage>(nav,"flightResults");
        Click(flights.transform,"Modify Search");
        Click(flights.transform,"Passengers"); // Two travelers, one shared cabin.
        Click(flights.transform,"Cabin"); Click(flights.transform,"Cabin");
        Click(flights.transform,"Done");
        Assert.AreEqual(2,flights.PassengerCount); Assert.AreEqual("Business",flights.CabinClass);
        Click(flights.transform.Find("Flight results scroll/Viewport/Flight cards/JetBlue"),"Select");
        Check("Seat Selection");
        var outbound = nav.CurrentSeatBooking;
        Assert.AreEqual("Tokyo, Japan",outbound.Destination);
        Assert.AreEqual(departure,outbound.DepartureDate);
        Assert.AreEqual(returning,outbound.ReturnDate);
        var seats = Field<SeatSelectionPage>(nav,"seatPage");
        Click(seats.transform,"Seat 1A"); Click(seats.transform,"Seat 1B");
        Assert.IsFalse(FindButton(seats.transform,"Seat 1C").interactable,"Unavailable seat stays unavailable");
        CollectionAssert.AreEqual(new[]{"1A","1B"},outbound.Seats);
        Click(seats.transform,"Confirm Seat");
        if (roundTrip)
        {
            Check("Return Flight Results");
            var returns = Field<FlightResultsPage>(nav,"returnResults");
            Assert.AreEqual(2,returns.PassengerCount);Assert.AreEqual("Business",returns.CabinClass);
            Click(returns.transform.Find("Flight results scroll/Viewport/Flight cards/Delta Air Lines"),"Select");
            Check("Return Seat Selection");
            var inbound = nav.CurrentReturnSeatBooking;
            Assert.AreEqual(outbound.Destination,inbound.Origin);Assert.AreEqual(outbound.Origin,inbound.Destination);
            Assert.AreEqual(returning,inbound.DepartureDate);
            var returnSeats = Field<SeatSelectionPage>(nav,"returnSeats");
            Click(returnSeats.transform,"Seat 2A");
            Click(returnSeats.transform,"Save for Later");nav.OpenSavedTrips();Click(Page<SavedTripsPage>().transform,"Resume draft");
            Check("Return Seat Selection");
            returnSeats=Field<SeatSelectionPage>(nav,"returnSeats");
            outbound=nav.CurrentSeatBooking;
            Assert.AreEqual("2A",nav.CurrentReturnSeatBooking.Seats[0]);Assert.IsNull(nav.CurrentReturnSeatBooking.Seats[1]);
            Click(returnSeats.transform,"Seat 2C");
            Click(returnSeats.transform,"Confirm Seat");
        }
        Check("Trip Review");
        SidebarRoundTrip("review");
        // Route editing returns to the same compact form without resetting booking options/dates.
        Click(Field<TripReviewPage>(nav,"tripReview").transform,"Edit route");
        Check("searchflights_page");
        Assert.AreEqual("JFK",Field<DestinationAutocomplete>(nav,"originSearch").SelectedAirportCode);
        Assert.AreEqual("HND",Field<DestinationAutocomplete>(nav,"destinationSearch").SelectedAirportCode);
        Assert.AreEqual(2,flights.PassengerCount); Assert.AreEqual("Business",flights.CabinClass);
        Assert.AreEqual(departure,Field<DateTime?>(dates,"departureDate"));
        SidebarRoundTrip("review");
        // Date editing uses the same calendar as an overlay on the compact form.
        Click(Field<TripReviewPage>(nav,"tripReview").transform,"Edit dates");
        Assert.IsNull(GameObject.Find("Trip Review"));
        Click(dates.transform,"Confirm Dates");
        Click(GameObject.Find("searchflights_page").transform,"Search Flights");
        Assert.AreSame(outbound,nav.CurrentSeatBooking);
        Click(flights.transform.Find("Flight results scroll/Viewport/Flight cards/JetBlue"),"Select");
        Click(seats.transform,"Confirm Seat");
        if(roundTrip)
        {
            Click(Field<FlightResultsPage>(nav,"returnResults").transform.Find("Flight results scroll/Viewport/Flight cards/Delta Air Lines"),"Select");
            Click(Field<SeatSelectionPage>(nav,"returnSeats").transform,"Confirm Seat");
        }
        Click(Field<TripReviewPage>(nav,"tripReview").transform,"Save for Later");
        Click(Field<TripReviewPage>(nav,"tripReview").transform,"Save for Later");
        Assert.AreEqual(1,services.DraftTrips.Count,"Repeated saving updates the same draft");
        var draft=services.DraftTrips[0];
        Assert.AreEqual(TripDraft.Step.Review,draft.step);
        Assert.IsTrue(draft.NeedsPayment);
        nav.OpenSavedTrips();
        var tripList=Page<SavedTripsPage>();
        Click(tripList.transform,"Filter Needs Payment");
        Assert.IsTrue(tripList.GetComponentsInChildren<TMP_Text>().Any(label=>label.name=="Trip status" && label.text=="Needs Payment"));
        Click(tripList.transform,"Filter Incomplete");
        Assert.IsFalse(tripList.GetComponentsInChildren<TMP_Text>().Any(label=>label.name=="Trip status"));
        Click(tripList.transform,"Filter All trips");
        Assert.AreEqual(outbound.Origin,draft.origin);Assert.AreEqual(outbound.Destination,draft.destination);
        CollectionAssert.AreEqual(outbound.Seats,draft.outbound.seats);
        Assert.AreEqual(0,TravelAppRuntime.Instance.CompletedFlights.Count,"Saving does not book or pay");
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        nav=Page<SidebarNavigation>();dates=Page<DateSelectionPage>();
        flights=Field<FlightResultsPage>(nav,"flightResults");seats=Field<SeatSelectionPage>(nav,"seatPage");
        nav.OpenSavedTrips();
        Click(Page<SavedTripsPage>().transform,"Resume draft");
        Check("Trip Review");outbound=nav.CurrentSeatBooking;
        Assert.AreEqual(2,outbound.PassengerCount);Assert.AreEqual("Business",outbound.Cabin);
        Assert.AreEqual(departure,outbound.DepartureDate);Assert.AreEqual(returning,outbound.ReturnDate);
        CollectionAssert.AreEqual(new[]{"1A","1B"},outbound.Seats);
        if(roundTrip)CollectionAssert.AreEqual(new[]{"2A","2C"},nav.CurrentReturnSeatBooking.Seats);
        Click(Field<TripReviewPage>(nav,"tripReview").transform,"Continue to Payment");
        Check("Payment");
        var payment=Page<PaymentPlaceholderPage>(); var snapshot=payment.Booking;
        Assert.AreEqual(testUsername,snapshot.Username);
        Assert.AreEqual(outbound.FarePerTraveler*2+(roundTrip?nav.CurrentReturnSeatBooking.FarePerTraveler*2:0),snapshot.EstimatedTotal);
        CollectionAssert.AreEqual(outbound.Seats,snapshot.Outbound.Seats);
        Assert.AreEqual(roundTrip,snapshot.Return!=null);
        Click(payment.transform,"Back to Trip Review");
        Click(Field<TripReviewPage>(nav,"tripReview").transform,"Continue to Payment");
        Assert.AreSame(snapshot,payment.Booking,"Unchanged payment must reuse the handoff");
        Click(payment.transform,"Submit mock payment");
        Assert.IsTrue(payment.gameObject.activeInHierarchy,"Invalid empty payment must not navigate");
        Assert.AreEqual(0,TravelAppRuntime.Instance.CompletedFlights.Count);
        var inputs=payment.GetComponentsInChildren<TMP_InputField>();
        inputs.First(f=>f.name=="Cardholder name").text="Flow Validator";
        inputs.First(f=>f.name=="Card number").text="4242424242424242";
        inputs.First(f=>f.name=="Expiration date").text="12/39";
        inputs.First(f=>f.name=="Security code").text="123";
        inputs.First(f=>f.name=="ZIP / postal code").text="10001";
        Click(payment.transform,"Submit mock payment");
        Check("Booking Confirmation");
        var runtime=TravelAppRuntime.Instance;
        Assert.AreEqual(1,runtime.CompletedFlights.Count);
        var receipt=runtime.CompletedFlights[0];
        Assert.AreEqual(0,services.DraftTrips.Count,"Completed draft leaves Saved Trips without changing history");
        Assert.IsTrue(receipt.PaymentCompleted); Assert.IsNotEmpty(receipt.Reference);
        Assert.AreSame(snapshot,receipt.Booking);
        Assert.AreSame(receipt,runtime.CompleteFlightPayment(snapshot),"Repeated completion must not duplicate a booking");
        Click(Field<DemoBookingPages>(nav,"confirmationPage").transform,"View Booked Trips");
        var history=Field<DemoBookingPages>(nav,"bookedTrips");
        Assert.AreEqual(1,history.Bookings.Count);
        var incompletePlan=new TripDraft{origin=snapshot.Origin,destination=snapshot.Destination};
        Assert.IsTrue(services.SaveDraft(incompletePlan).success);
        var unpaidPlan=new TripDraft{origin=snapshot.Origin,destination=snapshot.Destination,
            departureTicks=snapshot.DepartureDate.Ticks,returnTicks=snapshot.ReturnDate?.Ticks??0,oneWay=!roundTrip,
            passengers=2,cabin="Business",step=TripDraft.Step.Review};
        unpaidPlan.outbound=new TripDraft.Leg{airlineCode=snapshot.Outbound.AirlineCode,farePerTraveler=snapshot.Outbound.FarePerTraveler,seats=snapshot.Outbound.Seats.ToArray(),confirmed=true};
        if(roundTrip)unpaidPlan.inbound=new TripDraft.Leg{airlineCode=snapshot.Return.AirlineCode,farePerTraveler=snapshot.Return.FarePerTraveler,seats=snapshot.Return.Seats.ToArray(),confirmed=true};
        Assert.IsTrue(services.SaveDraft(unpaidPlan).success);
        Page<SavedTripsPage>().Refresh();
        CollectionAssert.AreEquivalent(new[]{"Incomplete","Needs Payment","Paid"},history.GetComponentsInChildren<TMP_Text>()
            .Where(label=>label.name=="Trip status").Select(label=>label.text).ToArray());
        Click(history.transform,"Filter Paid");
        Assert.IsTrue(history.GetComponentsInChildren<TMP_Text>().Any(label=>label.name=="Trip status" && label.text=="Paid"));
        Click(history.transform,"Filter All trips");
        Click(history.transform,"View details");
        Check("Booking Confirmation");
        string details=history.Bookings[0].Booking==snapshot?CompletedDemoBooking.Itinerary(snapshot):"";
        StringAssert.Contains("Seats: 1A, 1B",details);
        if(roundTrip)StringAssert.Contains("Seats: 2A, 2C",details);
        SidebarRoundTrip("review");
        Click(Field<TripReviewPage>(nav,"tripReview").transform,"Continue to Payment");
        Assert.IsFalse(FindButton(payment.transform,"Submit mock payment").interactable,"Paid booking cannot be charged again");
        Assert.AreEqual(1,runtime.CompletedFlights.Count);
        // Receipts survive recreation of the desktop Canvas, and remain scoped to their signed-in owner.
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null;yield return null;
        Assert.AreEqual(1,runtime.CompletedFlights.Count);
        nav=Page<SidebarNavigation>();
        Field<UIButton[]>(nav,"buttons")[2].onClick.Invoke();
        Assert.AreEqual(1,Field<DemoBookingPages>(nav,"bookedTrips").Bookings.Count);
        services.Accounts.Logout(); Assert.AreEqual(0,runtime.CompletedFlights.Count);
        Assert.IsTrue(services.Accounts.Login(testUsername,"test-pass").success);
        Assert.AreEqual(1,runtime.CompletedFlights.Count);
    }
    [UnityTest,Timeout(60000)] public IEnumerator RoundTripBusinessBookingPersistsThroughPaymentHistoryAndSidebar() { yield return Run(true); }
    [UnityTest,Timeout(60000)] public IEnumerator OneWaySkipsReturnAndKeepsCompleteReceipt() { yield return Run(false); }
    [UnityTest,Timeout(30000)] public IEnumerator EarlyDraftPreservesRawSearchDatesAndBookingOptions()
    {
        yield return StartLogin(false);
        var destination=Field<DestinationAutocomplete>(nav,"destinationSearch");
        Field<TMP_InputField>(destination,"input").text="Unfinished city";
        Click(GameObject.Find("searchflights_page").transform,"Save for Later");
        Assert.IsTrue(string.IsNullOrEmpty(services.DraftTrips[0].destination));
        nav.OpenSavedTrips();Click(Page<SavedTripsPage>().transform,"Resume draft");Check("searchflights_page");
        Assert.AreEqual("Unfinished city",Field<TMP_InputField>(destination,"input").text);
        Assert.IsFalse(FindButton(GameObject.Find("searchflights_page").transform,"Search Flights").interactable);
        Click(GameObject.Find("searchflights_page").transform,"card-Tokyo");Click(GameObject.Find("searchflights_page").transform,"Departure date");
        var dates=Page<DateSelectionPage>();var days=Field<UIButton[]>(dates,"dayButtons").Where(b=>b.gameObject.activeSelf).ToArray();
        days[9].onClick.Invoke();var departing=Field<DateTime?>(dates,"departureDate");
        Click(dates.transform,"Save for Later");Assert.AreEqual(1,services.DraftTrips.Count);
        nav.OpenSavedTrips();Click(Page<SavedTripsPage>().transform,"Resume draft");
        Assert.IsTrue(dates.gameObject.activeInHierarchy);Assert.AreEqual(departing,Field<DateTime?>(dates,"departureDate"));
        Assert.IsFalse(FindButton(dates.transform,"Confirm Dates").interactable);
        days=Field<UIButton[]>(dates,"dayButtons").Where(b=>b.gameObject.activeSelf).ToArray();days[14].onClick.Invoke();Click(dates.transform,"Confirm Dates");
        Click(GameObject.Find("searchflights_page").transform,"Search Flights");
        var flights=Field<FlightResultsPage>(nav,"flightResults");Click(flights.transform,"Modify Search");Click(flights.transform,"Passengers");
        Click(flights.transform,"Cabin");Click(flights.transform,"Done");Click(flights.transform,"Save for Later");
        nav.OpenSavedTrips();Click(Page<SavedTripsPage>().transform,"Resume draft");Check("Flight Results");
        Assert.AreEqual(2,flights.PassengerCount);Assert.AreEqual("Premium Economy",flights.CabinClass);Assert.IsNull(flights.SelectedDeparture);
        Click(flights.transform,"Modify Search");Click(flights.transform,"Cabin");
        Assert.AreEqual("Business",flights.CabinClass,"Restored control must advance from its displayed cabin");
        Assert.AreEqual(1,services.DraftTrips.Count);Assert.AreEqual(0,TravelAppRuntime.Instance.CompletedFlights.Count);
        string firstDraftId=services.DraftTrips[0].id;
        nav.OpenSavedTrips();Click(Page<SavedTripsPage>().transform,"Plan another trip");
        Assert.AreEqual(1,services.DraftTrips.Count,"Starting a new plan keeps previously saved drafts");
        Click(GameObject.Find("searchflights_page").transform,"Save for Later");
        Assert.AreEqual(2,services.DraftTrips.Count,"A deliberately new plan receives its own draft ID");
        nav.ResumeDraft(services.DraftTrips.First(d=>d.id==firstDraftId));
        Assert.IsTrue(nav.SaveActiveDraft().success);Assert.AreEqual(2,services.DraftTrips.Count);
    }

    [UnityTest,Timeout(30000)] public IEnumerator PartialSeatDraftResumesWithoutPaymentOrMissingSeats()
    {
        yield return StartLogin(true);
        Click(GameObject.Find("searchflights_page").transform,"card-Tokyo");
        Click(GameObject.Find("searchflights_page").transform,"Search Flights");
        var dates=Page<DateSelectionPage>();
        var days=Field<UIButton[]>(dates,"dayButtons").Where(b=>b.gameObject.activeSelf).ToArray();
        days[9].onClick.Invoke();days[14].onClick.Invoke();Click(dates.transform,"Confirm Dates");
        Click(GameObject.Find("searchflights_page").transform,"Search Flights");
        var flights=Field<FlightResultsPage>(nav,"flightResults");
        Click(flights.transform,"Modify Search");Click(flights.transform,"Passengers");Click(flights.transform,"Done");
        Click(flights.transform.Find("Flight results scroll/Viewport/Flight cards/JetBlue"),"Select");
        var seats=Field<SeatSelectionPage>(nav,"seatPage");
        Click(seats.transform,"Seat 1A");Click(seats.transform,"Save for Later");
        Assert.AreEqual(1,services.DraftTrips.Count);
        Assert.IsFalse(services.DraftTrips[0].outbound.confirmed);
        Assert.IsFalse(services.DraftTrips[0].NeedsPayment);
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        nav=Page<SidebarNavigation>();nav.OpenSavedTrips();Click(Page<SavedTripsPage>().transform,"Resume draft");
        Check("Seat Selection");
        Assert.AreEqual("1A",nav.CurrentSeatBooking.Seats[0]);Assert.IsNull(nav.CurrentSeatBooking.Seats[1]);
        Assert.IsFalse(FindButton(Field<SeatSelectionPage>(nav,"seatPage").transform,"Confirm Seat").interactable);
        Assert.AreEqual(0,TravelAppRuntime.Instance.CompletedFlights.Count);
        Assert.IsTrue(nav.SaveActiveDraft().success);Assert.AreEqual(1,services.DraftTrips.Count);
    }

    [UnityTest,Timeout(30000)] public IEnumerator SharedProfileNavigatesAndSignsOutUsingExistingAccount()
    {
        yield return StartLogin(true);
        var profile = Page<SharedProfileMenu>();
        var canvas = profile.GetComponentInParent<Canvas>().transform;
        GameObject.Find("header_avatar").GetComponent<UIButton>().onClick.Invoke();
        var overlay = canvas.Find("Shared profile overlay");
        Click(overlay,"Profile");
        Assert.IsTrue(overlay.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Profile details").gameObject.activeInHierarchy);
        Click(overlay,"Back to profile menu");
        Click(overlay,"Settings");
        Assert.IsFalse(profile.IsOpen);
        Assert.IsTrue(Field<GameObject[]>(nav,"pages")[3].activeSelf);
        profile.Toggle();Click(overlay,"Saved Trips");
        Assert.IsTrue(Field<GameObject[]>(nav,"pages")[2].activeInHierarchy);
        profile.Toggle();Click(overlay,"Dismiss profile menu");Assert.IsFalse(profile.IsOpen);
        profile.Toggle();Click(overlay,"Sign Out");
        yield return null;yield return null;yield return null;
        Assert.IsFalse(services.Accounts.IsLoggedIn);
        Assert.AreEqual("Login",SceneManager.GetActiveScene().name);
        Assert.IsNull(GameObject.Find("Shared profile overlay"));
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(TravelAppRuntime.Instance!=null && original!=null)typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);
        if(user!=null)Object.Destroy(user);if(catalog!=null)Object.Destroy(catalog);
        yield return null;
    }
}
