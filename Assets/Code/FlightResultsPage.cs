using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

public sealed class FlightResultsPage : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29, 40, 59, 255);
    private static readonly Color Muted = new Color32(96, 113, 139, 255);
    private static readonly Color Blue = new Color32(47, 80, 247, 255);
    private static readonly Color PaleBlue = new Color32(235, 240, 255, 255);
    private static readonly Color Card = new Color32(18, 27, 46, 255);
    private static readonly Color CardAccent = new Color32(47, 59, 83, 255);

    private TMP_Text route;
    private TMP_Text dates;
    private TMP_Text selectedButtonLabel;
    private Texture2D cardShading;
    private TMP_Text resultCount;
    private TMP_Text emptyResults;
    private GameObject openMenu;
    private string travelDates;
    private int passengers = 1;
    private int cabin;
    private bool nonstopOnly;
    private int airlineFilter;
    private int timeFilter;
    private const int NoPriceLimit = 2000;
    private int maximumPrice = NoPriceLimit;
    private int? lowestVisibleFare;
    private int durationFilter;
    private int sort;
    private static readonly string[] Cabins = { "Economy", "Premium Economy", "Business", "First" };
    private static readonly float[] CabinFareMultipliers = { 1f, 1.35f, 2.2f, 3.25f };
    private static readonly string[] SortOptions = { "Cheapest", "Shortest", "Earliest departure" };
    private readonly List<Flight> flights = new List<Flight>();

    public Flight SelectedDeparture { get; private set; }
    public int PassengerCount => passengers;
    public string CabinClass => Cabins[cabin];
    public int SelectedFarePerTraveler => SelectedDeparture == null ? 0 : FareFor(SelectedDeparture);
    public event Action DepartureSelected;

    public sealed class Flight
    {
        public string Code { get; }
        public string Airline { get; }
        public string Departure { get; }
        public string Arrival { get; }
        public int DurationMinutes { get; }
        public int Price { get; }
        public int Stops { get; }
        internal TimeSpan DepartureTime { get; }
        internal RectTransform CardView { get; set; }
        internal TMP_Text PriceLabel { get; set; }

        internal Flight(string code, string airline, string departure, string arrival, int durationMinutes, int price, int stops = 0)
        {
            Code = code;
            Airline = airline;
            Departure = departure;
            Arrival = arrival;
            DurationMinutes = durationMinutes;
            Price = price;
            Stops = stops;
            DepartureTime = DateTime.ParseExact(departure, "h:mm tt", CultureInfo.InvariantCulture).TimeOfDay;
        }
    }

    public void Build(DateSelectionPage style, Action editDestination, Action editDates, Action back = null)
    {
        cardShading = new Texture2D(1, 5, TextureFormat.RGBA32, false);
        cardShading.wrapMode = TextureWrapMode.Clamp;
        // Texture rows run bottom to top. Leave the wing visible through the center.
        cardShading.SetPixels32(new[]
        {
            new Color32(9, 16, 31, 235),
            new Color32(9, 16, 31, 185),
            new Color32(9, 16, 31, 55),
            new Color32(9, 16, 31, 125),
            new Color32(9, 16, 31, 205)
        });
        cardShading.Apply(false, true);
        var background = gameObject.AddComponent<Image>();
        background.color = new Color32(245, 247, 250, 255);
        background.raycastTarget = false;

        var summary = style.Panel("Trip summary", transform, 60, 62, 1416, 112);
        style.Shape("Trip badge", summary, 34, 25, 60, 60, PaleBlue);
        Text("Trip badge label", summary, 34, 25, 60, 60, "TO", 18, Blue, true);
        route = Text("Destination", summary, 118, 21, 1010, 36, "", 26, Ink, true, TextAlignmentOptions.Left);
        dates = Text("Travel dates", summary, 118, 59, 1010, 32, "", 20, Muted, false, TextAlignmentOptions.Left);
        var modify = style.Control("Modify Search", summary, 1018, 27, 208, 56,
            back == null ? "Modify Search" : "Departure seats", PaleBlue, Blue, 21, 1.6f);
        var filters = style.Control("Filters", summary, 1242, 27, 136, 56,
            "Filters", PaleBlue, Blue, 21, 1.6f);

        resultCount = Text("Result count", transform, 60, 201, 400, 42, "", 25, Ink, true, TextAlignmentOptions.Left);
        emptyResults = Text("No matches", transform, 60, 276, 1416, 95,
            "No flights match these filters.", 24, Muted, false);
        Text("Mock data label", transform, 830, 201, 372, 42, "Sample fares · USD", 20, Muted, false, TextAlignmentOptions.Right);
        var sortButton = style.Control("Sort", transform, 1220, 197, 256, 50,
            "Sort: Cheapest", PaleBlue, Blue, 19, 1.6f);

        AddFlight(style, new Flight("B6", "JetBlue", "8:30 PM", "1:15 AM +1", 285, 189));
        AddFlight(style, new Flight("DL", "Delta Air Lines", "6:00 AM", "10:30 AM", 270, 249));
        AddFlight(style, new Flight("AA", "American Airlines", "2:15 PM", "6:45 PM", 270, 312));
        AddFlight(style, new Flight("UA", "United Airlines", "11:00 AM", "3:45 PM", 285, 425));
        BuildMenus(style, modify, filters, sortButton, editDestination, editDates, back);
        RefreshResults();
    }

    public void ShowTrip(string routeLabel, string travelDates)
    {
        route.text = routeLabel;
        this.travelDates = travelDates;
        SelectedDeparture = null;
        if (selectedButtonLabel != null)
            selectedButtonLabel.text = "Select";
        selectedButtonLabel = null;
        RefreshTripDetails();
    }

    public void CopyBookingOptions(FlightResultsPage departureResults)
    {
        passengers = departureResults.passengers;
        cabin = departureResults.cabin;
        RefreshResults();
    }

    private void AddFlight(DateSelectionPage style, Flight flight)
    {
        var card = style.Shape(flight.Airline, transform, 0, 0, 693, 242, Card, 1.3f);
        flight.CardView = card;
        flights.Add(flight);
        var texture = Resources.Load<Texture2D>("FlightWings/" + flight.Code);
        if (texture != null)
        {
            card.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            AddBackdropLayer(card, "Wing photo", texture, new Color(0.83f, 0.87f, 0.95f));
            AddBackdropLayer(card, "Navy shading", cardShading, Color.white);
        }
        style.Shape("Airline badge", card, 30, 30, 44, 36, CardAccent, 3);
        Text("Airline code", card, 30, 30, 44, 36, flight.Code, 16, Color.white, true);
        Text("Airline", card, 90, 27, 460, 42, flight.Airline, 26, Color.white, true, TextAlignmentOptions.Left);
        style.Shape("Duration badge", card, 565, 30, 98, 36, CardAccent, 2);
        Text("Duration", card, 565, 30, 98, 36, $"{flight.DurationMinutes / 60}h {flight.DurationMinutes % 60}m", 18, Color.white, false);
        Text("Flight times", card, 30, 145, 405, 44, flight.Departure + " – " + flight.Arrival,
            26, Color.white, true, TextAlignmentOptions.Left);
        Text("Stops", card, 30, 189, 405, 28, flight.Stops == 0 ? "Nonstop" : $"{flight.Stops} stops", 18, PaleBlue, false, TextAlignmentOptions.Left);
        flight.PriceLabel = Text("Price", card, 420, 143, 102, 48, "", 34, Color.white, true, TextAlignmentOptions.Right);
        flight.PriceLabel.enableAutoSizing = true;
        flight.PriceLabel.fontSizeMin = 25;
        flight.PriceLabel.fontSizeMax = 34;
        Text("Fare type", card, 390, 190, 132, 25, "per traveler", 16, PaleBlue, false, TextAlignmentOptions.Right);
        var select = style.Control("Select", card, 545, 153, 118, 52, "Select", Blue, Color.white, 20, 1.8f);
        var label = select.GetComponentInChildren<TMP_Text>();
        select.onClick.AddListener(() =>
        {
            if (selectedButtonLabel != null)
                selectedButtonLabel.text = "Select";
            selectedButtonLabel = label;
            selectedButtonLabel.text = "Selected";
            SelectedDeparture = flight;
            RefreshTripDetails();
            DepartureSelected?.Invoke();
        });
    }

    private void BuildMenus(DateSelectionPage style, UnityEngine.UI.Button modify, UnityEngine.UI.Button filters,
        UnityEngine.UI.Button sortButton, Action editDestination, Action editDates, Action back)
    {
        var editMenu = style.Panel("Modify Search menu", transform, 996, 181, 480, 292);
        editMenu.GetComponent<Image>().raycastTarget = true;
        AddMenuButton(style, editMenu, 16, "Edit destination", editDestination);
        AddMenuButton(style, editMenu, 68, "Edit departure and return dates", editDates);
        AddChoice(style, editMenu, 120, "Passengers", new[] { "1", "2", "3", "4", "5", "6" },
            index => { passengers = index + 1; RefreshTripDetails(); });
        AddChoice(style, editMenu, 172, "Cabin", Cabins, index => { cabin = index; RefreshResults(); });
        AddMenuButton(style, editMenu, 224, "Done", CloseMenu);
        if (back == null)
            BindMenu(modify, (RectTransform)editMenu.parent);
        else
        {
            editMenu.parent.gameObject.SetActive(false);
            modify.onClick.AddListener(() => back());
        }

        var filterMenu = style.Panel("Filters menu", transform, 996, 181, 480, 360);
        filterMenu.GetComponent<Image>().raycastTarget = true;
        AddChoice(style, filterMenu, 16, "Nonstop only", new[] { "Off", "On" },
            index => { nonstopOnly = index == 1; RefreshResults(); });
        AddChoice(style, filterMenu, 68, "Airline", new[] { "Any" }.Concat(flights.Select(flight => flight.Airline)).ToArray(),
            index => { airlineFilter = index; RefreshResults(); });
        AddChoice(style, filterMenu, 120, "Departure time", new[] { "Any", "12 AM–12 PM", "12 PM–6 PM", "6 PM–12 AM" },
            index => { timeFilter = index; RefreshResults(); });
        AddMaximumPriceSlider(style, filterMenu);
        AddChoice(style, filterMenu, 242, "Maximum duration", new[] { "Any", "4h 30m", "4h 45m" },
            index => { durationFilter = index; RefreshResults(); });
        AddMenuButton(style, filterMenu, 294, "Done", CloseMenu);
        BindMenu(filters, (RectTransform)filterMenu.parent);

        var sortMenu = style.Panel("Sort menu", transform, 996, 254, 480, 186);
        sortMenu.GetComponent<Image>().raycastTarget = true;
        var sortLabel = sortButton.GetComponentInChildren<TMP_Text>();
        for (int i = 0; i < SortOptions.Length; i++)
        {
            int option = i;
            AddMenuButton(style, sortMenu, 16 + i * 52, SortOptions[i], () =>
            {
                sort = option;
                sortLabel.text = "Sort: " + SortOptions[option];
                RefreshResults();
                CloseMenu();
            });
        }
        BindMenu(sortButton, (RectTransform)sortMenu.parent);
    }

    private void AddMaximumPriceSlider(DateSelectionPage style, Transform menu)
    {
        var label = Text("Maximum fare label", menu, 16, 171, 445, 30,
            "Max fare per traveler: Any", 19, Ink, false, TextAlignmentOptions.Left);
        var track = style.Shape("Maximum fare", menu, 29, 204, 419, 28, PaleBlue, 3);
        track.GetComponent<Image>().raycastTarget = true;
        var handle = style.Shape("Handle", track, 0, 0, 24, 28, Blue, 3);
        handle.anchorMin = handle.anchorMax = new Vector2(0, 0.5f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        var slider = track.gameObject.AddComponent<Slider>();
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 100;
        slider.maxValue = NoPriceLimit;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(maximumPrice);
        slider.onValueChanged.AddListener(value =>
        {
            maximumPrice = (int)value;
            label.text = maximumPrice == NoPriceLimit
                ? "Max fare per traveler: Any"
                : $"Max fare per traveler: ${maximumPrice:N0}";
            RefreshResults();
        });
    }

    private static UnityEngine.UI.Button AddMenuButton(DateSelectionPage style, Transform menu, float y,
        string caption, Action action)
    {
        var button = style.Control(caption, menu, 16, y, 445, 44, caption, PaleBlue, Blue, 19, 2);
        button.onClick.AddListener(() => action());
        return button;
    }

    private static void AddChoice(DateSelectionPage style, Transform menu, float y, string name,
        string[] options, Action<int> changed)
    {
        int index = 0;
        var button = style.Control(name, menu, 16, y, 445, 44, name + ": " + options[0] + "  ›",
            PaleBlue, Blue, 19, 2);
        var label = button.GetComponentInChildren<TMP_Text>();
        button.onClick.AddListener(() =>
        {
            index = (index + 1) % options.Length;
            label.text = name + ": " + options[index] + "  ›";
            changed(index);
        });
    }

    private void BindMenu(UnityEngine.UI.Button button, RectTransform menu)
    {
        menu.gameObject.SetActive(false);
        button.onClick.AddListener(() =>
        {
            bool shouldOpen = openMenu != menu.gameObject;
            CloseMenu();
            if (!shouldOpen)
                return;
            openMenu = menu.gameObject;
            menu.SetAsLastSibling();
            openMenu.SetActive(true);
        });
    }

    private void CloseMenu()
    {
        if (openMenu != null)
            openMenu.SetActive(false);
        openMenu = null;
    }

    private void OnDisable()
    {
        CloseMenu();
    }

    private void RefreshTripDetails()
    {
        string passengerLabel = passengers == 1 ? "1 passenger" : $"{passengers} passengers";
        string total = SelectedDeparture != null
            ? $"Est. total ${FareFor(SelectedDeparture) * passengers:N0}"
            : lowestVisibleFare.HasValue
                ? $"Est. total from ${lowestVisibleFare.Value * passengers:N0}"
                : "Est. total unavailable";
        dates.text = $"{travelDates}  ·  {passengerLabel}  ·  {Cabins[cabin]}  ·  {total}";
    }

    private int FareFor(Flight flight) => Mathf.RoundToInt(flight.Price * CabinFareMultipliers[cabin]);

    private void RefreshResults()
    {
        foreach (var flight in flights)
        {
            flight.PriceLabel.text = $"${FareFor(flight):N0}";
            flight.CardView.gameObject.SetActive(false);
        }

        var matches = flights.Where(MatchesFilters);
        var ordered = sort switch
        {
            1 => matches.OrderBy(flight => flight.DurationMinutes),
            2 => matches.OrderBy(flight => flight.DepartureTime),
            _ => matches.OrderBy(FareFor)
        };
        int count = 0;
        lowestVisibleFare = null;
        foreach (var flight in ordered)
        {
            int fare = FareFor(flight);
            lowestVisibleFare = Mathf.Min(lowestVisibleFare ?? fare, fare);
            flight.CardView.anchoredPosition = new Vector2(60 + count % 2 * 723, -276 - count / 2 * 272);
            flight.CardView.SetSiblingIndex(count);
            flight.CardView.gameObject.SetActive(true);
            count++;
        }
        resultCount.text = count == 1 ? "1 flight found" : $"{count} flights found";
        emptyResults.gameObject.SetActive(count == 0);
        RefreshTripDetails();
    }

    private bool MatchesFilters(Flight flight)
    {
        if (nonstopOnly && flight.Stops != 0 ||
            airlineFilter > 0 && flight.Code != flights[airlineFilter - 1].Code)
            return false;

        int hour = flight.DepartureTime.Hours;
        bool timeMatches = timeFilter switch
        {
            1 => hour < 12,
            2 => hour >= 12 && hour < 18,
            3 => hour >= 18,
            _ => true
        };
        bool priceMatches = maximumPrice == NoPriceLimit || FareFor(flight) <= maximumPrice;
        int maximumDuration = durationFilter == 1 ? 270 : 285;
        return timeMatches && priceMatches && (durationFilter == 0 || flight.DurationMinutes <= maximumDuration);
    }

    private static void AddBackdropLayer(RectTransform card, string layerName, Texture2D texture, Color tint)
    {
        var layer = new GameObject(layerName, typeof(RectTransform), typeof(RawImage));
        layer.layer = card.gameObject.layer;
        var rect = (RectTransform)layer.transform;
        rect.SetParent(card, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = layer.GetComponent<RawImage>();
        image.texture = texture;
        image.color = tint;
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        Destroy(cardShading);
    }
}
