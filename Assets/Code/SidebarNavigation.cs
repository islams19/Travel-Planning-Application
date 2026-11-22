using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class SidebarNavigation : MonoBehaviour
{
    [SerializeField] private GameObject[] pages;
    [SerializeField] private UnityEngine.UI.Button[] buttons;
    [SerializeField] private TMP_Text headerTitle;
    [SerializeField] private TMP_Text headerSubtitle;
    [SerializeField] private DateSelectionPage datePage;
    private DestinationAutocomplete originSearch;
    private string bookingOrigin;
    private string bookingDestination;
    private static string DefaultOrigin => TravelApp.FlightMap.TravelLocationCatalog.Find("JFK").BookingDestination+" (JFK)";
    private CompactFlightSearch compactSearch;
    private DestinationAutocomplete destinationSearch;
    private FlightResultsPage flightResults;
    private SeatSelectionPage seatPage;
    private FlightResultsPage returnResults;
    private SeatSelectionPage returnSeats;
    private TripReviewPage tripReview;
    private PaymentPlaceholderPage paymentPage;
    private DemoBookingPages bookedTrips;
    private DemoBookingPages confirmationPage;
    private DateTime departureDate;
    private DateTime? returnDate;
    private string travelDates;
    public SeatSelectionPage.Booking CurrentSeatBooking => seatPage?.CurrentBooking;
    public SeatSelectionPage.Booking CurrentReturnSeatBooking => returnSeats?.CurrentBooking;
    private PaymentBooking paymentBooking;
    private SharedProfileMenu profileMenu;
    private SavedTripsPage savedTrips;
    private string activeDraftId;
    private TripDraft.Step bookingStep;
    private float subtitleRightOffset;
    private TextWrappingModes subtitleWrapping;

    private static readonly string[] Titles =
    {
        "Search flight destinations", "Explore World", "My Trips", "Settings"
    };

    private static readonly string[] Subtitles =
    {
        "LET'S FIND A TRIP", "DISCOVER DESTINATIONS", "YOUR TRIPS", "YOUR PREFERENCES"
    };

    private void Awake()
    {
        subtitleRightOffset = headerSubtitle.rectTransform.offsetMax.x;
        subtitleWrapping = headerSubtitle.textWrappingMode;
        for (int i = 0; i < buttons.Length; i++)
        {
            int pageIndex = i;
            buttons[i].onClick.AddListener(() => ShowPage(pageIndex));
        }

        ShowPage(0);
    }

    public TravelApp.Result SearchExploreDestination(string airportCode)
    {
        if (destinationSearch == null || !destinationSearch.TrySelectAirportDestination(airportCode))
            return TravelApp.Result.Fail("This destination isn’t available in Search Flights yet. Choose another airport.");
        // The same autocomplete selection path as suggestions/cards owns the destination.
        // No origin, dates, fares, flights, seats, or Explore simulation data are transferred.
        ShowPage(0);
        return TravelApp.Result.Ok("Destination selected.");
    }

    public void OpenSettings() => ShowPage(3);
    public void OpenSavedTrips() => ShowPage(2);

    private void HideAllPages()
    {
        profileMenu?.Close();
        if (savedTrips != null) savedTrips.gameObject.SetActive(false);
        foreach (var page in pages)
            page.SetActive(false);
        if (datePage != null)
            datePage.gameObject.SetActive(false);
        if (flightResults != null)
            flightResults.gameObject.SetActive(false);
        if (seatPage != null)
            seatPage.gameObject.SetActive(false);
        if (paymentPage != null)
            paymentPage.gameObject.SetActive(false);
        if (confirmationPage != null)
            confirmationPage.gameObject.SetActive(false);
        if (returnResults != null)
        {
            returnResults.gameObject.SetActive(false);
            returnSeats.gameObject.SetActive(false);
            tripReview.gameObject.SetActive(false);
        }

    }

    private void ShowPage(int index)
    {
        HideAllPages();
        pages[index].SetActive(true);
        if (index == 2 && savedTrips != null) savedTrips.Refresh();
        if (index == 0) bookingStep = TripDraft.Step.Search;

        if(index==0) compactSearch?.Refresh();
        headerTitle.text = Titles[index];
        SetSubtitleRightOffset(subtitleRightOffset);
        headerSubtitle.textWrappingMode = subtitleWrapping;
        headerSubtitle.text = Subtitles[index];
    }

    private void Start()
    {
        var searchPage = pages[0];
        destinationSearch = searchPage.GetComponentInChildren<DestinationAutocomplete>(true);
        if (destinationSearch == null || datePage == null)
            return;

        datePage.Build();
        profileMenu = gameObject.AddComponent<SharedProfileMenu>();
        profileMenu.Build(datePage, this, headerTitle.GetComponentInParent<Canvas>());
        var exploreSelector = pages[1].GetComponentInChildren<TravelApp.FlightMap.FlightLocationSelector>(true);
        if (exploreSelector != null)
            pages[1].AddComponent<ExploreBookingBridge>().Build(datePage, exploreSelector, SearchExploreDestination);
        BuildOriginField(searchPage.transform);
        flightResults = CreateBookingPanel("Flight Results").AddComponent<FlightResultsPage>();
        flightResults.Build(datePage, () => ShowPage(0), ShowDateSelection);
        seatPage = CreateBookingPanel("Seat Selection").AddComponent<SeatSelectionPage>();
        seatPage.Build(datePage, ReturnToFlightResults);
        flightResults.DepartureSelected += ShowSeatSelection;
        seatPage.SeatsConfirmed += ContinueAfterDepartureSeats;

        returnResults = CreateBookingPanel("Return Flight Results").AddComponent<FlightResultsPage>();
        returnResults.Build(datePage, null, null, ShowSeatSelection);
        returnSeats = CreateBookingPanel("Return Seat Selection").AddComponent<SeatSelectionPage>();
        returnSeats.Build(datePage, ShowReturnResults);
        returnResults.DepartureSelected += ShowReturnSeats;
        returnSeats.SeatsConfirmed += ShowTripReview;
        tripReview = CreateBookingPanel("Trip Review").AddComponent<TripReviewPage>();
        tripReview.Build(datePage, () => ShowPage(0), ShowDateSelection,
            ReturnToFlightResults, ShowSeatSelection, ShowReturnResults, ShowReturnSeats, ContinueToPayment);
        paymentPage = CreateBookingPanel("Payment").AddComponent<PaymentPlaceholderPage>();
        paymentPage.Build(datePage, ShowTripReview, CompleteDemoPayment);
        bookedTrips = pages[2].AddComponent<DemoBookingPages>();
        bookedTrips.BuildBookedTrips(datePage, ShowConfirmation, ResumeDraft, StartNewTrip);
        savedTrips = pages[2].GetComponent<SavedTripsPage>();
        buttons[2].GetComponentInChildren<TMP_Text>().text = "My Trips";
        confirmationPage = CreateBookingPanel("Booking Confirmation").AddComponent<DemoBookingPages>();
        confirmationPage.BuildConfirmation(datePage, () => ShowPage(2));

        AddSaveAction(datePage.transform, 84, 802, TripDraft.Step.Dates);
        AddSaveAction(flightResults.transform, 600, 199, TripDraft.Step.DepartureFlights);
        AddSaveAction(seatPage.transform.Find("Seat viewport/Scrollable seat content"), 940, 880, TripDraft.Step.DepartureSeats);
        AddSaveAction(returnResults.transform, 600, 199, TripDraft.Step.ReturnFlights);
        AddSaveAction(returnSeats.transform.Find("Seat viewport/Scrollable seat content"), 940, 880, TripDraft.Step.ReturnSeats);
        AddSaveAction(tripReview.transform, 780, 115, TripDraft.Step.Review);
        AddSaveAction(paymentPage.transform, 330, 26, TripDraft.Step.Payment);
        datePage.DatesConfirmed += (departure,returning) =>
        {
            if(compactSearch!=null && compactSearch.IsCalendarOpen) compactSearch.CloseCalendar();
            else ShowFlightResults(departure,returning);
        };
        compactSearch=searchPage.AddComponent<CompactFlightSearch>();
        compactSearch.Build(datePage,flightResults,originSearch,destinationSearch,()=>HasValidRoute,
            ()=>ShowFlightResults(datePage.Departure.Value,datePage.Return));
        AddSaveAction(compactSearch.SearchSurface,1214,20,TripDraft.Step.Search);
    }

    private void BuildOriginField(Transform searchPage)
    {
        var origin=datePage.Shape("Origin airport",searchPage,0,0,324,64,Color.white,2);
        origin.gameObject.SetActive(false);
        originSearch=origin.gameObject.AddComponent<DestinationAutocomplete>();
        originSearch.ConfigureOrigin(new[]{DefaultOrigin});
        origin.gameObject.SetActive(true);
    }

    private bool HasValidRoute => originSearch!=null && destinationSearch!=null && originSearch.CanContinue && destinationSearch.CanContinue &&
        !string.Equals(originSearch.SelectedAirportCode,destinationSearch.SelectedAirportCode,StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(originSearch.SelectedCityKey,destinationSearch.SelectedCityKey,StringComparison.OrdinalIgnoreCase);

    private GameObject CreateBookingPanel(string panelName)
    {
        var template = (RectTransform)pages[0].transform;
        var panel = new GameObject(panelName, typeof(RectTransform));
        panel.layer = datePage.gameObject.layer;
        panel.SetActive(false);
        var rect = (RectTransform)panel.transform;
        rect.SetParent(pages[0].transform.parent, false);
        rect.anchorMin = template.anchorMin;
        rect.anchorMax = template.anchorMax;
        rect.offsetMin = template.offsetMin;
        rect.offsetMax = template.offsetMax;
        return panel;
    }

    private void ShowDateSelection()
    {
        ShowPage(0);
        compactSearch.OpenCalendar(datePage.Departure.HasValue && !datePage.Return.HasValue && !datePage.IsOneWay);
    }

    private void ShowFlightResults(DateTime departure, DateTime? returnDate)
    {
        if (!HasValidRoute)
            return;

        bookingOrigin = originSearch.SelectedDestination;
        bookingDestination = destinationSearch.SelectedDestination;
        departureDate = departure;
        this.returnDate = returnDate;
        travelDates = FormatDate(departure) + (returnDate.HasValue ? " – " + FormatDate(returnDate.Value) : " · One way");
        flightResults.ShowTrip(OutboundRoute, travelDates, departureDate);
        ReturnToFlightResults();
    }

    private void ReturnToFlightResults()
    {
        HideAllPages();
        bookingStep = TripDraft.Step.DepartureFlights;
        headerTitle.text = "Select Departure Flight";
        SetTripSubtitle(OutboundRoute.ToUpperInvariant() + "  ·  " + travelDates);
        tripReview.gameObject.SetActive(false);
        datePage.gameObject.SetActive(false);
        seatPage.gameObject.SetActive(false);
        flightResults.gameObject.SetActive(true);
    }

    private void ShowSeatSelection()
    {
        HideAllPages();
        bookingStep = TripDraft.Step.DepartureSeats;
        returnResults.gameObject.SetActive(false);
        flightResults.gameObject.SetActive(false);
        tripReview.gameObject.SetActive(false);
        headerTitle.text = "Choose your seat";
        SetTripSubtitle(OutboundRoute.ToUpperInvariant() + "  ·  " + flightResults.CabinClass);
        seatPage.gameObject.SetActive(true);
        seatPage.ShowBooking(bookingOrigin, bookingDestination, departureDate, returnDate, flightResults);
    }

    private void ContinueAfterDepartureSeats()
    {
        if (returnDate.HasValue)
        {
            returnResults.CopyBookingOptions(flightResults);
            returnResults.ShowTrip(ReturnRoute, FormatDate(returnDate.Value), returnDate.Value);
            ShowReturnResults();
        }
        else
        {
            ShowTripReview();
        }
    }

    private string OutboundRoute => bookingOrigin + " → " + bookingDestination;
    private string ReturnRoute => bookingDestination + " → " + bookingOrigin;

    private void ShowReturnResults()
    {
        HideAllPages();
        bookingStep = TripDraft.Step.ReturnFlights;
        seatPage.gameObject.SetActive(false);
        returnSeats.gameObject.SetActive(false);
        tripReview.gameObject.SetActive(false);
        headerTitle.text = "Select Return Flight";
        SetTripSubtitle(ReturnRoute.ToUpperInvariant() + "  ·  " + FormatDate(returnDate.Value));
        returnResults.gameObject.SetActive(true);
    }

    private void ShowReturnSeats()
    {
        HideAllPages();
        bookingStep = TripDraft.Step.ReturnSeats;
        returnResults.gameObject.SetActive(false);
        tripReview.gameObject.SetActive(false);
        headerTitle.text = "Choose your return seat";
        SetTripSubtitle(ReturnRoute.ToUpperInvariant() + "  ·  " + returnResults.CabinClass);
        returnSeats.gameObject.SetActive(true);
        returnSeats.ShowBooking(bookingDestination, bookingOrigin, returnDate.Value, null, returnResults);
    }

    private void ShowTripReview()
    {
        HideAllPages();
        bookingStep = TripDraft.Step.Review;
        confirmationPage.gameObject.SetActive(false);
        paymentPage.gameObject.SetActive(false);
        seatPage.gameObject.SetActive(false);
        returnSeats.gameObject.SetActive(false);
        headerTitle.text = "Review your trip";
        SetTripSubtitle(OutboundRoute.ToUpperInvariant() + "  ·  " + travelDates);
        tripReview.gameObject.SetActive(true);
        tripReview.ShowBooking(CurrentSeatBooking, returnDate.HasValue ? CurrentReturnSeatBooking : null);
    }

    private bool ContinueToPayment()
    {
        var outbound = CurrentSeatBooking;
        var inbound = returnDate.HasValue ? CurrentReturnSeatBooking : null;
        if (!HasValidRoute || bookingOrigin != originSearch.SelectedDestination ||
            bookingDestination != destinationSearch.SelectedDestination ||
            !IsCompleteLeg(outbound, bookingOrigin, bookingDestination, departureDate, flightResults) ||
            outbound.ReturnDate != returnDate)
            return false;

        if (returnDate.HasValue && (returnDate.Value <= departureDate ||
            !IsCompleteLeg(inbound, bookingDestination, bookingOrigin, returnDate.Value, returnResults) ||
            inbound.PassengerCount != outbound.PassengerCount || inbound.Cabin != outbound.Cabin))
            return false;

        // Reuse an unchanged handoff, including after Back/sidebar navigation, so payment is idempotent.
        if (paymentBooking == null || !paymentBooking.Matches(outbound, inbound))
            paymentBooking = new PaymentBooking(outbound, inbound);
        HideAllPages();
        paymentPage.gameObject.SetActive(true);
        paymentPage.ShowBooking(paymentBooking);
        bookingStep = TripDraft.Step.Payment;
        tripReview.gameObject.SetActive(false);
        headerTitle.text = "Payment";
        SetTripSubtitle(OutboundRoute.ToUpperInvariant() + "  ·  " + travelDates);
        return true;
    }

    private void CompleteDemoPayment(PaymentBooking booking)
    {
        ShowConfirmation(bookedTrips.Complete(booking));
        if (activeDraftId != null)
        {
            TravelApp.TravelAppRuntime.Instance.Services.RemoveDraft(activeDraftId);
            activeDraftId = null;
        }
    }

    private void ShowConfirmation(CompletedDemoBooking booking)
    {
        ShowPage(2);
        pages[2].SetActive(false);
        headerTitle.text = "Booking confirmation";
        SetTripSubtitle("BOOKING · " + booking.Reference);
        confirmationPage.gameObject.SetActive(true);
        confirmationPage.ShowConfirmation(booking);
    }

    private void AddSaveAction(Transform parent, float x, float y, TripDraft.Step step)
    {
        var button = datePage.Control("Save for Later", parent, x, y, 210, 46, "Save for Later",
            new Color32(235, 240, 255, 255), new Color32(47, 80, 247, 255), 19, 2);
        button.onClick.AddListener(() =>
        {
            var result = SaveActiveDraft(step);
            button.GetComponentInChildren<TMP_Text>().text = result.success ? "Saved for later" : "Could not save";
            StartCoroutine(ResetSaveLabel(button));
        });
    }

    private System.Collections.IEnumerator ResetSaveLabel(UnityEngine.UI.Button button)
    {
        yield return new WaitForSecondsRealtime(2);
        if (button != null) button.GetComponentInChildren<TMP_Text>().text = "Save for Later";
    }

    public TravelApp.Result SaveActiveDraft() => SaveActiveDraft(bookingStep);
    private TravelApp.Result SaveActiveDraft(TripDraft.Step step)
    {
        if (CurrentSeatBooking != null && paymentBooking != null &&
            paymentBooking.Matches(CurrentSeatBooking, returnDate.HasValue ? CurrentReturnSeatBooking : null) &&
            paymentBooking.Origin == originSearch.SelectedDestination && paymentBooking.Destination == destinationSearch.SelectedDestination &&
            paymentBooking.DepartureDate == datePage.Departure && paymentBooking.ReturnDate == datePage.Return &&
            TravelApp.TravelAppRuntime.Instance.CompletedFlights.Any(record => ReferenceEquals(record.Booking, paymentBooking)))
            return TravelApp.Result.Fail("This trip is already booked.");
        var draft = new TripDraft
        {
            id = activeDraftId ?? Guid.NewGuid().ToString("N"),
            origin = originSearch.SelectedDestination, destination = destinationSearch.SelectedDestination,
            originText = originSearch.RawText, destinationText = destinationSearch.RawText,
            originAirportCode=originSearch.SelectedAirportCode, destinationAirportCode=destinationSearch.SelectedAirportCode,
            departureTicks = datePage.Departure?.Ticks ?? 0, returnTicks = datePage.Return?.Ticks ?? 0,
            oneWay = datePage.IsOneWay, passengers = flightResults.PassengerCount, cabin = flightResults.CabinClass, step = step
        };
        bool sameRoute = draft.origin == bookingOrigin && draft.destination == bookingDestination;
        if (sameRoute && datePage.Departure == departureDate)
            draft.outbound = CaptureLeg(flightResults, CurrentSeatBooking, draft.origin, draft.destination, datePage.Departure);
        if (sameRoute && datePage.Return.HasValue && datePage.Return == returnDate)
            draft.inbound = CaptureLeg(returnResults, CurrentReturnSeatBooking, draft.destination, draft.origin, datePage.Return);
        var result = TravelApp.TravelAppRuntime.Instance.Services.SaveDraft(draft);
        if (result.success) activeDraftId = draft.id;
        return result;
    }

    private static TripDraft.Leg CaptureLeg(FlightResultsPage results, SeatSelectionPage.Booking booking,
        string origin, string destination, DateTime? date)
    {
        var leg = new TripDraft.Leg { airlineCode = results.SelectedDeparture?.Code, itineraryId = results.SelectedDeparture?.Id, farePerTraveler = results.SelectedFarePerTraveler };
        if (booking != null && booking.Flight == results.SelectedDeparture && booking.Origin == origin &&
            booking.Destination == destination && booking.DepartureDate == date && booking.PassengerCount == results.PassengerCount && booking.Cabin == results.CabinClass)
        {
            leg.seats = booking.Seats.ToArray();
            leg.segments = booking.Flight.Segments.Select((segment,index)=>new TripDraft.Segment
            {
                originCode=segment.OriginCode, destinationCode=segment.DestinationCode, flightNumber=segment.FlightNumber,
                departureUtcTicks=segment.DepartureUtc.Ticks, arrivalUtcTicks=segment.ArrivalUtc.Ticks,
                seats=booking.SeatsForSegment(index).ToArray()
            }).ToArray();
            leg.confirmed = booking.Confirmed;
        }
        return leg;
    }

    public void StartNewTrip()
    {
        activeDraftId = null;
        paymentBooking = null;
        originSearch.RestoreSelection(DefaultOrigin, DefaultOrigin,"JFK");
        destinationSearch.RestoreSelection(null, "");
        datePage.RestoreDates(null, null, false);
        bookingOrigin = bookingDestination = null;
        departureDate = default; returnDate = null; travelDates = "";
        flightResults.ShowTrip("", ""); flightResults.RestoreOptions(1, "Economy", null);
        returnResults.ShowTrip("", ""); returnResults.RestoreOptions(1, "Economy", null);
        seatPage.ClearBooking(); returnSeats.ClearBooking();
        ShowPage(0);
    }

    public void ResumeDraft(TripDraft draft)
    {
        // Look up the saved snapshot under the current account; never trust a UI-supplied owner.
        draft = TravelApp.TravelAppRuntime.Instance.Services.DraftTrips.FirstOrDefault(saved => saved.id == draft?.id);
        if (draft == null) return;
        HideAllPages();
        activeDraftId = draft.id;
        paymentBooking = null;
        originSearch.RestoreSelection(draft.origin, draft.originText,draft.originAirportCode);
        destinationSearch.RestoreSelection(draft.destination, draft.destinationText,draft.destinationAirportCode);
        datePage.RestoreDates(draft.Departure, draft.Return, draft.oneWay);
        bookingOrigin = originSearch.SelectedDestination;
        bookingDestination = destinationSearch.SelectedDestination;
        departureDate = draft.Departure ?? default;
        returnDate = datePage.Return;
        travelDates = draft.Departure.HasValue ? FormatDate(departureDate) + (returnDate.HasValue ? " – " + FormatDate(returnDate.Value) : " · One way") : "";
        flightResults.ShowTrip(OutboundRoute, travelDates, departureDate);
        flightResults.RestoreOptions(draft.passengers, draft.cabin, draft.outbound?.airlineCode, draft.outbound?.itineraryId);
        returnResults.ShowTrip(ReturnRoute, returnDate.HasValue ? FormatDate(returnDate.Value) : "", returnDate);
        returnResults.RestoreOptions(draft.passengers, draft.cabin, draft.inbound?.airlineCode, draft.inbound?.itineraryId);
        seatPage.ClearBooking(); returnSeats.ClearBooking();
        if (HasValidRoute && draft.Departure.HasValue && flightResults.SelectedDeparture != null)
        {
            seatPage.ShowBooking(bookingOrigin, bookingDestination, departureDate, returnDate, flightResults);
            seatPage.RestoreAssignments(draft.outbound);
        }
        if (HasValidRoute && returnDate.HasValue && returnResults.SelectedDeparture != null)
        {
            returnSeats.ShowBooking(bookingDestination, bookingOrigin, returnDate.Value, null, returnResults);
            returnSeats.RestoreAssignments(draft.inbound);
        }
        var available = !HasValidRoute ? TripDraft.Step.Search :
            !draft.Departure.HasValue || !draft.oneWay && (!returnDate.HasValue || returnDate <= departureDate) ? TripDraft.Step.Dates :
            flightResults.SelectedDeparture == null ? TripDraft.Step.DepartureFlights :
            CurrentSeatBooking == null || !CurrentSeatBooking.Confirmed ? TripDraft.Step.DepartureSeats :
            returnDate.HasValue && returnResults.SelectedDeparture == null ? TripDraft.Step.ReturnFlights :
            returnDate.HasValue && (CurrentReturnSeatBooking == null || !CurrentReturnSeatBooking.Confirmed) ? TripDraft.Step.ReturnSeats : TripDraft.Step.Payment;
        var step = (TripDraft.Step)Math.Min((int)draft.step, (int)available);
        if (!returnDate.HasValue && (step == TripDraft.Step.ReturnFlights || step == TripDraft.Step.ReturnSeats)) step = TripDraft.Step.Review;
        switch (step)
        {
            case TripDraft.Step.Search: ShowPage(0); break;
            case TripDraft.Step.Dates: ShowDateSelection(); break;
            case TripDraft.Step.DepartureFlights: ReturnToFlightResults(); break;
            case TripDraft.Step.DepartureSeats: ShowSeatSelection(); break;
            case TripDraft.Step.ReturnFlights: ShowReturnResults(); break;
            case TripDraft.Step.ReturnSeats: ShowReturnSeats(); break;
            case TripDraft.Step.Review: ShowTripReview(); break;
            default: ShowTripReview(); ContinueToPayment(); break;
        }
    }

    private static bool IsCompleteLeg(SeatSelectionPage.Booking leg, string origin, string destination,
        DateTime date, FlightResultsPage results)
    {
        return leg != null && leg.Confirmed && date != default && leg.DepartureDate == date &&
            !string.IsNullOrWhiteSpace(origin) && !string.IsNullOrWhiteSpace(destination) &&
            leg.Origin == origin && leg.Destination == destination &&
            leg.Flight != null && leg.Flight == results.SelectedDeparture &&
            leg.PassengerCount >= 1 && leg.PassengerCount <= 6 && leg.PassengerCount == results.PassengerCount &&
            !string.IsNullOrWhiteSpace(leg.Cabin) && leg.Cabin == results.CabinClass &&
            leg.FarePerTraveler > 0 && leg.FarePerTraveler == results.SelectedFarePerTraveler &&
            leg.HasAllSeats;
    }

    private static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private void SetTripSubtitle(string subtitle)
    {
        SetSubtitleRightOffset(subtitleRightOffset);
        headerSubtitle.textWrappingMode = TextWrappingModes.NoWrap;
        headerSubtitle.text = subtitle;
        float extraWidth = Mathf.Max(0, Mathf.Ceil(headerSubtitle.preferredWidth) - headerSubtitle.rectTransform.rect.width);
        SetSubtitleRightOffset(subtitleRightOffset + extraWidth);
    }

    private void SetSubtitleRightOffset(float rightOffset)
    {
        var rect = headerSubtitle.rectTransform;
        rect.offsetMax = new Vector2(rightOffset, rect.offsetMax.y);
    }
}
