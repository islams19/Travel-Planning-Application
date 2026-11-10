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
    private static readonly string[] OriginAirports =
    {
        "New York, New York (JFK)", "Miami, Florida (MIA)", "Boston, Massachusetts (BOS)",
        "Chicago, Illinois (ORD)", "Los Angeles, California (LAX)", "San Francisco, California (SFO)",
        "Toronto, Canada (YYZ)", "London, United Kingdom (LHR)", "Paris, France (CDG)",
        "Tokyo, Japan (HND)", "Sydney, Australia (SYD)", "Dubai, United Arab Emirates (DXB)"
    };
    private DestinationAutocomplete destinationSearch;
    private FlightResultsPage flightResults;
    private SeatSelectionPage seatPage;
    private FlightResultsPage returnResults;
    private SeatSelectionPage returnSeats;
    private TripReviewPage tripReview;
    private PaymentPlaceholderPage paymentPage;
    private DateTime departureDate;
    private DateTime? returnDate;
    private string travelDates;
    public SeatSelectionPage.Booking CurrentSeatBooking => seatPage?.CurrentBooking;
    public SeatSelectionPage.Booking CurrentReturnSeatBooking => returnSeats?.CurrentBooking;
    private float subtitleRightOffset;
    private TextWrappingModes subtitleWrapping;

    private static readonly string[] Titles =
    {
        "Search flight destinations", "Explore World", "Booked Trips", "Settings"
    };

    private static readonly string[] Subtitles =
    {
        "LET'S FIND A TRIP", "DISCOVER DESTINATIONS", "YOUR TRAVEL PLANS", "YOUR PREFERENCES"
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

    private void ShowPage(int index)
    {
        if (datePage != null)
            datePage.gameObject.SetActive(false);
        if (flightResults != null)
            flightResults.gameObject.SetActive(false);
        if (seatPage != null)
            seatPage.gameObject.SetActive(false);
        if (paymentPage != null)
            paymentPage.gameObject.SetActive(false);
        if (returnResults != null)
        {
            returnResults.gameObject.SetActive(false);
            returnSeats.gameObject.SetActive(false);
            tripReview.gameObject.SetActive(false);
        }

        for (int i = 0; i < pages.Length; i++)
            pages[i].SetActive(i == index);

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
        paymentPage = CreateBookingPanel("PaymentPlaceholder").AddComponent<PaymentPlaceholderPage>();
        paymentPage.Build(datePage, ShowTripReview);
        datePage.DatesConfirmed += ShowFlightResults;

        var continueButton = datePage.CreateContinueButton(searchPage.transform);
        var message = DateSelectionPage.Text("Route validation", searchPage.transform, 50, 167, 1100, 28,
            "Choose different origin and destination cities.", 18, new Color32(168, 49, 54, 255),
            false, TextAlignmentOptions.Left);
        void RefreshRoute(bool _)
        {
            continueButton.interactable = HasValidRoute;
            message.gameObject.SetActive(originSearch.CanContinue && destinationSearch.CanContinue && !HasValidRoute);
        }
        originSearch.ContinuationChanged += RefreshRoute;
        destinationSearch.ContinuationChanged += RefreshRoute;
        RefreshRoute(false);
        continueButton.onClick.AddListener(ShowDateSelection);
    }

    private void BuildOriginField(Transform searchPage)
    {
        var destination = (RectTransform)destinationSearch.transform;
        const float left = 46;
        const float destinationLeft = 770;
        const float top = 57;
        const float width = 700;
        const float height = 76;
        var origin = datePage.Shape("Origin airport", searchPage, left, top, width, height, Color.white, 1.15f);
        origin.gameObject.SetActive(false);
        destination.anchorMin = destination.anchorMax = destination.pivot = Vector2.up;
        destination.sizeDelta = new Vector2(width, height);
        destination.anchoredPosition = new Vector2(destinationLeft, -top);
        var fieldBackground = origin.GetComponent<UnityEngine.UI.Image>();
        var destinationBackground = destination.GetComponent<UnityEngine.UI.Image>();
        destinationBackground.sprite = fieldBackground.sprite;
        destinationBackground.type = fieldBackground.type;
        destinationBackground.pixelsPerUnitMultiplier = fieldBackground.pixelsPerUnitMultiplier;
        destinationBackground.color = fieldBackground.color;

        var labelColor = new Color32(49, 65, 91, 255);
        DateSelectionPage.Text("Origin label", searchPage, left, 24, width, 25,
            "Origin airport", 18, labelColor, true, TextAlignmentOptions.Left);
        DateSelectionPage.Text("Destination label", searchPage, destinationLeft, 24, width, 25,
            "Destination", 18, labelColor, true, TextAlignmentOptions.Left);
        originSearch = origin.gameObject.AddComponent<DestinationAutocomplete>();
        originSearch.ConfigureOrigin(OriginAirports);
        origin.gameObject.SetActive(true);
    }

    private bool HasValidRoute => originSearch.CanContinue && destinationSearch.CanContinue &&
        !string.Equals(originSearch.SelectedDestination.Split(',')[0], destinationSearch.SelectedDestination.Split(',')[0],
            StringComparison.OrdinalIgnoreCase);

    private GameObject CreateBookingPanel(string panelName)
    {
        var template = (RectTransform)datePage.transform;
        var panel = new GameObject(panelName, typeof(RectTransform));
        panel.layer = datePage.gameObject.layer;
        panel.SetActive(false);
        var rect = (RectTransform)panel.transform;
        rect.SetParent(template.parent, false);
        rect.anchorMin = template.anchorMin;
        rect.anchorMax = template.anchorMax;
        rect.offsetMin = template.offsetMin;
        rect.offsetMax = template.offsetMax;
        return panel;
    }

    private void ShowDateSelection()
    {
        if (!HasValidRoute)
            return;

        string destination = destinationSearch.SelectedDestination;
        foreach (var page in pages)
            page.SetActive(false);

        headerTitle.text = "Select dates for your trip";
        SetTripSubtitle(destination.ToUpperInvariant());
        flightResults.gameObject.SetActive(false);
        tripReview.gameObject.SetActive(false);
        seatPage.gameObject.SetActive(false);
        datePage.SetDestination(destination);
        datePage.gameObject.SetActive(true);
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
        flightResults.ShowTrip(OutboundRoute, travelDates);
        ReturnToFlightResults();
    }

    private void ReturnToFlightResults()
    {
        headerTitle.text = "Select Departure Flight";
        SetTripSubtitle(OutboundRoute.ToUpperInvariant() + "  ·  " + travelDates);
        tripReview.gameObject.SetActive(false);
        datePage.gameObject.SetActive(false);
        seatPage.gameObject.SetActive(false);
        flightResults.gameObject.SetActive(true);
    }

    private void ShowSeatSelection()
    {
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
            returnResults.ShowTrip(ReturnRoute, FormatDate(returnDate.Value));
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
        seatPage.gameObject.SetActive(false);
        returnSeats.gameObject.SetActive(false);
        tripReview.gameObject.SetActive(false);
        headerTitle.text = "Select Return Flight";
        SetTripSubtitle(ReturnRoute.ToUpperInvariant() + "  ·  " + FormatDate(returnDate.Value));
        returnResults.gameObject.SetActive(true);
    }

    private void ShowReturnSeats()
    {
        returnResults.gameObject.SetActive(false);
        tripReview.gameObject.SetActive(false);
        headerTitle.text = "Choose your return seat";
        SetTripSubtitle(ReturnRoute.ToUpperInvariant() + "  ·  " + returnResults.CabinClass);
        returnSeats.gameObject.SetActive(true);
        returnSeats.ShowBooking(bookingDestination, bookingOrigin, returnDate.Value, null, returnResults);
    }

    private void ShowTripReview()
    {
        paymentPage.gameObject.SetActive(false);
        seatPage.gameObject.SetActive(false);
        returnSeats.gameObject.SetActive(false);
        headerTitle.text = "Review your trip";
        SetTripSubtitle(OutboundRoute.ToUpperInvariant() + "  ·  " + travelDates);
        tripReview.ShowBooking(CurrentSeatBooking, returnDate.HasValue ? CurrentReturnSeatBooking : null);
        tripReview.gameObject.SetActive(true);
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

        paymentPage.ShowBooking(new PaymentBooking(outbound, inbound));
        tripReview.gameObject.SetActive(false);
        headerTitle.text = "Payment";
        SetTripSubtitle(OutboundRoute.ToUpperInvariant() + "  ·  " + travelDates);
        paymentPage.gameObject.SetActive(true);
        return true;
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
            leg.Seats.All(seat => !string.IsNullOrWhiteSpace(seat)) &&
            leg.Seats.Distinct(StringComparer.Ordinal).Count() == leg.PassengerCount;
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
