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
    private DropdownChevron openChevron;
    private string travelDates;
    private DateTime routeTravelDate = new DateTime(2027,1,15);
    private int passengers = 1;
    private int cabin;
    private int stopsFilter;
    private RectTransform resultsContent;
    private ScrollRect resultsScroll;
    private RectTransform detailsOverlay;
    private RectTransform detailsContent;
    private ScrollRect detailsScroll;
    private DateSelectionPage detailsStyle;
    private TMP_Text detailsTitle;
    private readonly List<string> airlineCodes = new List<string>();
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
    public event Action BookingOptionsChanged;
    internal static IReadOnlyList<string> CabinChoices => Cabins;

    public sealed class Segment
    {
        public string OriginCode { get; }
        public string DestinationCode { get; }
        public string Airline { get; }
        public string FlightNumber { get; }
        public DateTime DepartureUtc { get; }
        public DateTime ArrivalUtc { get; }
        public DateTime TravelDate { get; }
        public DateTime DepartureLocal => TravelApp.FlightMap.AirportClock.Local(DepartureUtc,OriginCode);
        public DateTime ArrivalLocal => TravelApp.FlightMap.AirportClock.Local(ArrivalUtc,DestinationCode);
        public string DepartureLabel => TravelApp.FlightMap.AirportClock.Format(DepartureUtc,OriginCode,TravelDate);
        public string ArrivalLabel => TravelApp.FlightMap.AirportClock.Format(ArrivalUtc,DestinationCode,TravelDate);
        public int DepartureMinutes => (int)(DepartureUtc-DateTime.SpecifyKind(TravelDate.Date,DateTimeKind.Utc)).TotalMinutes;
        public int ArrivalMinutes => (int)(ArrivalUtc-DateTime.SpecifyKind(TravelDate.Date,DateTimeKind.Utc)).TotalMinutes;
        public int DurationMinutes => (int)(ArrivalUtc-DepartureUtc).TotalMinutes;
        public string AircraftType { get; }
        public string DepartureTerminal { get; }
        public string ArrivalTerminal { get; }
        public IReadOnlyList<string> Amenities { get; }
        internal Segment(string origin,string destination,string airline,string number,DateTime departure,DateTime arrival,DateTime travelDate)
        {
            OriginCode=origin; DestinationCode=destination; Airline=airline; FlightNumber=number;
            DepartureUtc=DateTime.SpecifyKind(departure,DateTimeKind.Utc); ArrivalUtc=DateTime.SpecifyKind(arrival,DateTimeKind.Utc);
            TravelDate=travelDate.Date;
            int seed=number.Sum(c=>(int)c);
            AircraftType=DurationMinutes>=480?"Boeing 787-9":seed%2==0?"Airbus A320":"Boeing 737-800";
            DepartureTerminal="Terminal "+(seed%3+1)+" (demo)"; ArrivalTerminal="Terminal "+((seed+1)%3+1)+" (demo)";
            var amenities=new List<string>{seed%3==0?"Wi-Fi unavailable":"Wi-Fi available"};
            if(DurationMinutes>=180)amenities.Add("Meal service");
            if(DurationMinutes>=120)amenities.Add("Power outlets");
            if(DurationMinutes>=240)amenities.Add("Seatback entertainment");
            Amenities=amenities.AsReadOnly();
        }
        public string Description => $"{OriginCode} → {DestinationCode} · {Airline} {FlightNumber}\n{DepartureLabel} – {ArrivalLabel} · {DurationLabel(DurationMinutes)}";
    }

    public sealed class Layover
    {
        public string AirportCode { get; }
        public string City { get; }
        public int DurationMinutes { get; }
        internal Layover(string code, string city, int duration) { AirportCode=code; City=city; DurationMinutes=duration; }
        public string Description => $"Layover in {City} ({AirportCode}): {DurationLabel(DurationMinutes)}";
    }

    // Synthetic itinerary allowances, deliberately independent of real airline policy.
    public sealed class BaggageAllowance
    {
        public bool CarryOnIncluded { get; }
        public int CheckedBagsIncluded { get; }
        public int CheckedBagFee { get; }
        public string CarryOnLabel => CarryOnIncluded ? "Carry-on included" : "No carry-on included";
        public string CheckedBagLabel => CheckedBagsIncluded > 0 ? $"{CheckedBagsIncluded} checked bag included" :
            CheckedBagFee > 0 ? $"Checked bag ${CheckedBagFee}" : "No checked bag included";
        public string Summary => CarryOnLabel + " · " + CheckedBagLabel;
        public const string PolicyNote = "Illustrative baggage allowances per traveler; not actual airline policies. Optional bag fees are excluded from the estimated fare.";
        internal BaggageAllowance(bool carryOn, int checkedBags, int fee)
        { CarryOnIncluded=carryOn; CheckedBagsIncluded=checkedBags; CheckedBagFee=fee; }
    }

    public sealed class Flight
    {
        public string Id { get; }
        public string Code { get; }
        public string Airline { get; }
        public string Departure => Segments.Count>0 ? Segments[0].DepartureLabel : Clock((int)DepartureTime.TotalMinutes);
        public string Arrival { get; private set; }
        public int DurationMinutes { get; private set; }
        public int Price { get; }
        public int Stops => Layovers.Count;
        public IReadOnlyList<Segment> Segments { get; private set; } = Array.Empty<Segment>();
        public IReadOnlyList<Layover> Layovers { get; private set; } = Array.Empty<Layover>();
        public string ConnectionSummary => Stops == 0 ? "Nonstop" :
            $"{Stops} stop{(Stops==1 ? "" : "s")} · " + string.Join(" · ", Layovers.Select(l => $"{l.AirportCode} {DurationLabel(l.DurationMinutes)} layover"));
        public string SegmentSummary => string.Join("\n", Segments.Select((segment,index) =>
            segment.Description + (index < Layovers.Count ? "\n" + Layovers[index].Description : "")));
        internal TimeSpan DepartureTime { get; }
        internal RectTransform CardView { get; set; }
        internal TMP_Text PriceLabel { get; set; }
        internal TMP_Text TimesLabel, StopsLabel, DurationLabelView, AirportLabel, TotalLabel;
        public BaggageAllowance Baggage { get; }
        internal int ExpectedSegmentCount => stopCount+1;
        readonly int stopCount;
        readonly float durationFactor;
        readonly int layoverMinutes;
        readonly int scheduleIndex;

        internal Flight(string code, string airline, string departure, int price, int index, int stops=0, float factor=1f, int layover=80)
        {
            Code=code; Airline=airline; Price=price; scheduleIndex=index; Id=code+"-"+(100+index);
            stopCount=stops; durationFactor=factor; layoverMinutes=layover;
            Baggage = new BaggageAllowance(true, index % 3 == 1 ? 1 : 0, index % 3 == 2 ? 0 : 35 + index % 3 * 5);
            DepartureTime=DateTime.ParseExact(departure,"h:mm tt",CultureInfo.InvariantCulture).TimeOfDay;
        }

        internal void ConfigureRoute(TravelApp.FlightMap.FlightLocation from, TravelApp.FlightMap.FlightLocation to,DateTime travelDate)
        {
            string origin=from?.code ?? "JFK", destination=to?.code ?? "LAX";
            double km=from!=null && to!=null ? TravelApp.FlightMap.FlightGeoMath.GreatCircleDistanceKm(from,to) : 3974;
            int direct=Math.Max(75,(int)Math.Round((45+km/850*60)*durationFactor/5)*5);
            // Deterministic intermediate airports; never duplicate an endpoint or another connection.
            var hubs=new[]{"ORD","ATL","DFW","LHR","AMS","DXB","SIN","SEA","DEN","HND"}
                .Where(code=>code!=origin && code!=destination).Select(TravelApp.FlightMap.TravelLocationCatalog.Find).Where(h=>h!=null)
                .OrderBy(h=>from!=null && to!=null ? TravelApp.FlightMap.FlightGeoMath.GreatCircleDistanceKm(from,h)+TravelApp.FlightMap.FlightGeoMath.GreatCircleDistanceKm(h,to) : 0)
                .Take(stopCount).ToArray();
            var airports=new[]{origin}.Concat(hubs.Select(h=>h.code)).Concat(new[]{destination}).ToArray();
            var segments=new List<Segment>(); var connections=new List<Layover>();
            DateTime start=TravelApp.FlightMap.AirportClock.ToUtc(travelDate.Date.Add(DepartureTime),origin);
            DateTime departureUtc=start;
            var points=new[]{from ?? TravelApp.FlightMap.TravelLocationCatalog.Find(origin)}.Concat(hubs)
                .Concat(new[]{to ?? TravelApp.FlightMap.TravelLocationCatalog.Find(destination)}).ToArray();
            var distances=Enumerable.Range(0,points.Length-1).Select(i=>TravelApp.FlightMap.FlightGeoMath.GreatCircleDistanceKm(points[i],points[i+1])).ToArray();
            double totalDistance=Math.Max(1,distances.Sum());
            int flying=stopCount==0 ? direct : Math.Max(direct,(int)Math.Round((45*(stopCount+1)+totalDistance/850*60)*durationFactor/5)*5);
            int flown=0;
            for(int i=0;i<airports.Length-1;i++)
            {
                int length=i==airports.Length-2 ? flying-flown : (int)Math.Round(flying*distances[i]/totalDistance);
                flown+=length;
                segments.Add(new Segment(airports[i],airports[i+1],Airline,Code+" "+(1000+scheduleIndex*10+i),start,start.AddMinutes(length),travelDate));
                start=start.AddMinutes(length);
                if(i<hubs.Length) { int wait=layoverMinutes+i*25; connections.Add(new Layover(hubs[i].code,hubs[i].city,wait)); start=start.AddMinutes(wait); }
            }
            Segments=segments.AsReadOnly(); Layovers=connections.AsReadOnly();
            DurationMinutes=(int)(start-departureUtc).TotalMinutes; Arrival=Segments[Segments.Count-1].ArrivalLabel;
        }
    }

    public static string DurationLabel(int minutes) => $"{minutes/60}h {minutes%60:00}m";
    public static string Clock(int minutes) => DateTime.Today.AddMinutes(minutes%1440).ToString("h:mm tt",CultureInfo.InvariantCulture) +
        (minutes>=1440 ? $" (+{minutes/1440} day{(minutes/1440==1 ? "" : "s")})" : "");

    private static IEnumerable<Flight> CreateSchedule()
    {
        yield return new Flight("B6","JetBlue","8:30 PM",189,0);
        yield return new Flight("DL","Delta Air Lines","6:00 AM",249,1);
        yield return new Flight("AA","American Airlines","2:15 PM",312,2);
        yield return new Flight("UA","United Airlines","11:00 AM",425,3);
        yield return new Flight("B6","JetBlue","7:10 AM",218,4,1,1f,80);
        yield return new Flight("DL","Delta Air Lines","9:20 AM",279,5,0,.94f);
        yield return new Flight("AA","American Airlines","5:40 AM",168,6,1,1.04f,95);
        yield return new Flight("UA","United Airlines","5:35 PM",238,7,1,.98f,65);
        yield return new Flight("DL","Delta Air Lines","10:25 PM",289,8,1,1.07f,110);
        yield return new Flight("AA","American Airlines","7:45 PM",359,9,0,1.08f);
        yield return new Flight("B6","JetBlue","12:10 PM",199,10,2,1.12f,75);
        yield return new Flight("UA","United Airlines","8:00 AM",460,11,1,.95f,55);
    }

    public static int ExpectedSegments(string itineraryId) => CreateSchedule().FirstOrDefault(f=>f.Id==itineraryId)?.ExpectedSegmentCount ?? 1;

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
        Text("Mock data label", transform, 830, 201, 372, 42, "Fares · USD", 20, Muted, false, TextAlignmentOptions.Right);
        var sortButton = style.Control("Sort", transform, 1220, 197, 256, 50,
            "Sort: Cheapest", PaleBlue, Blue, 19, 1.6f);

        BuildResultsScroll(style);
        // Stable schedule IDs distinguish different itineraries from the same airline.
        foreach(var flight in CreateSchedule()) AddFlight(style,flight);
        foreach(var code in flights.Select(f=>f.Code).Distinct()) airlineCodes.Add(code);
        ConfigureFlightRoutes("JFK", "LAX");
        BuildFlightDetails(style);
        BuildMenus(style, modify, filters, sortButton, editDestination, editDates, back);
        RefreshResults();
    }

    public void ShowTrip(string routeLabel, string travelDates, DateTime? departureDate = null)
    {
        DateTime nextDate=departureDate?.Date ?? new DateTime(2027,1,15);
        bool sameTrip = route.text == routeLabel && this.travelDates == travelDates && routeTravelDate==nextDate;
        routeTravelDate=nextDate;
        route.text = routeLabel;
        this.travelDates = travelDates;
        if (sameTrip)
        {
            RefreshTripDetails();
            return;
        }
        var endpoints=routeLabel.Split('→');
        ConfigureFlightRoutes(endpoints.Length==2 ? endpoints[0].Trim() : "JFK", endpoints.Length==2 ? endpoints[1].Trim() : "LAX");
        resultsScroll.verticalNormalizedPosition=1;
        SelectedDeparture = null;
        if (selectedButtonLabel != null)
            selectedButtonLabel.text = "Select";
        selectedButtonLabel = null;
        RefreshTripDetails();
    }

    internal void RestoreOptions(int count, string cabinName, string airlineCode, string itineraryId = null)
    {
        passengers = Mathf.Clamp(count, 1, 6);
        cabin = Mathf.Max(0, Array.IndexOf(Cabins, cabinName));
        SelectedDeparture = !string.IsNullOrEmpty(itineraryId) ? flights.Find(flight => flight.Id == itineraryId) : flights.Find(flight => flight.Code == airlineCode);
        if (selectedButtonLabel != null) selectedButtonLabel.text = "Select";
        selectedButtonLabel = null;
        if (SelectedDeparture != null)
        {
            selectedButtonLabel = SelectedDeparture.CardView.GetComponentsInChildren<TMP_Text>()
                .FirstOrDefault(label => label.transform.parent.name == "Select");
            if (selectedButtonLabel != null) selectedButtonLabel.text = "Selected";
        }
        foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if (button.name == "Passengers") button.GetComponentInChildren<TMP_Text>().text = "Passengers: " + passengers;
            if (button.name == "Cabin") button.GetComponentInChildren<TMP_Text>().text = "Cabin: " + CabinClass;
        }
        CloseMenu();
        RefreshResults();
    }

    internal void SetBookingOptions(int count,string cabinName)
    {
        passengers=Mathf.Clamp(count,1,6);
        cabin=Mathf.Max(0,Array.IndexOf(Cabins,cabinName));
        foreach(var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if(button.name=="Passengers") button.GetComponentInChildren<TMP_Text>().text="Passengers: "+passengers;
            if(button.name=="Cabin") button.GetComponentInChildren<TMP_Text>().text="Cabin: "+CabinClass;
        }
        RefreshResults();
    }

    public void CopyBookingOptions(FlightResultsPage departureResults)
    {
        passengers = departureResults.passengers;
        cabin = departureResults.cabin;
        RefreshResults();
    }

    private void AddFlight(DateSelectionPage style, Flight flight)
    {
        var card = style.Shape(flights.Any(f=>f.Airline==flight.Airline) ? flight.Airline+" "+flight.Id : flight.Airline, resultsContent, 0, 0, 1416, 180, Color.white, 1.6f);
        card.anchorMin=Vector2.up; card.anchorMax=Vector2.one; card.sizeDelta=new Vector2(0,180);
        card.GetComponent<Image>().raycastTarget=true;
        card.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        flight.CardView = card;
        flights.Add(flight);
        // Restrict the existing wing photograph to an accent strip; information stays on white.
        var accent=style.Shape("Aviation accent",card,0,0,180,180,Card,1.6f);
        var texture=Resources.Load<Texture2D>("FlightWings/"+flight.Code);
        if(texture!=null)
        {
            AddBackdropLayer(accent,"Wing photo",texture,Color.white);
            AddBackdropLayer(accent,"Navy shading",cardShading,Color.white);
        }
        AirlineLogo.Build(style,card,200,20,106,42,flight.Code);
        Text("Airline",card,322,22,390,36,flight.Airline,23,Ink,true,TextAlignmentOptions.Left);
        flight.TimesLabel=Text("Flight times",card,200,68,470,38,"",26,Ink,true,TextAlignmentOptions.Left);
        flight.TimesLabel.enableAutoSizing=true; flight.TimesLabel.fontSizeMin=19; flight.TimesLabel.fontSizeMax=26;
        flight.AirportLabel=Text("Airports",card,200,111,470,28,"",19,Muted,false,TextAlignmentOptions.Left);
        flight.DurationLabelView=Text("Duration",card,730,25,300,32,"",23,Ink,true,TextAlignmentOptions.Left);
        flight.StopsLabel=Text("Stops",card,730,65,300,50,"",18,Muted,false,TextAlignmentOptions.TopLeft);
        flight.StopsLabel.textWrappingMode=TextWrappingModes.Normal;
        BaggageIcon.Add(card,200,145,false,Muted);
        Text("Carry-on",card,228,143,225,25,flight.Baggage.CarryOnLabel,16,Muted,false,TextAlignmentOptions.Left);
        BaggageIcon.Add(card,465,145,true,Muted);
        Text("Checked baggage",card,493,143,240,25,flight.Baggage.CheckedBagLabel,16,Muted,false,TextAlignmentOptions.Left);
        var detail=style.Control("Flight details",card,730,125,200,44,"Flight details",PaleBlue,Blue,18,2);
        DropdownChevron.Attach(detail).PointRight();
        detail.onClick.AddListener(()=>ShowFlightDetails(flight));
        flight.PriceLabel=Text("Price",card,1188,23,196,42,"",32,Ink,true,TextAlignmentOptions.Right);
        Text("Fare type",card,1188,65,196,24,"per traveler",16,Muted,false,TextAlignmentOptions.Right);
        flight.TotalLabel=Text("Traveler total",card,1138,93,246,24,"",16,Muted,false,TextAlignmentOptions.Right);
        var select=style.Control("Select",card,1266,125,118,44,"Select",Blue,Color.white,20,1.8f);
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
            index => { passengers = index + 1; RefreshResults(); }, () => passengers - 1);
        AddChoice(style, editMenu, 172, "Cabin", Cabins, index => { cabin = index; RefreshResults(); }, () => cabin);
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
        AddChoice(style, filterMenu, 16, "Stops", new[] { "Any", "Nonstop", "1 stop or fewer", "2 stops or fewer" },
            index => { stopsFilter = index; RefreshResults(); });
        AddChoice(style, filterMenu, 68, "Airline", new[] { "Any" }.Concat(airlineCodes.Select(code=>flights.First(f=>f.Code==code).Airline)).ToArray(),
            index => { airlineFilter = index; RefreshResults(); });
        AddChoice(style, filterMenu, 120, "Departure time", new[] { "Any", "12 AM–12 PM", "12 PM–6 PM", "6 PM–12 AM" },
            index => { timeFilter = index; RefreshResults(); });
        AddMaximumPriceSlider(style, filterMenu);
        AddChoice(style, filterMenu, 242, "Maximum duration", new[] { "Any", "6h", "9h", "15h" },
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
        string[] options, Action<int> changed, Func<int> currentIndex = null)
    {
        int index = 0;
        var button = style.Control(name, menu, 16, y, 445, 44, name + ": " + options[0],
            PaleBlue, Blue, 19, 2);
        DropdownChevron.Attach(button);
        var label = button.GetComponentInChildren<TMP_Text>();
        button.onClick.AddListener(() =>
        {
            index = ((currentIndex?.Invoke() ?? index) + 1) % options.Length;
            label.text = name + ": " + options[index];
            changed(index);
        });
    }

    private void BindMenu(UnityEngine.UI.Button button, RectTransform menu)
    {
        var chevron=DropdownChevron.Attach(button);
        menu.gameObject.SetActive(false);
        button.onClick.AddListener(() =>
        {
            bool shouldOpen = openMenu != menu.gameObject;
            CloseMenu();
            if (!shouldOpen)
                return;
            openMenu = menu.gameObject;
            openChevron=chevron; chevron.SetExpanded(true);
            menu.SetAsLastSibling();
            openMenu.SetActive(true);
        });
    }

    private void CloseMenu()
    {
        if (openMenu != null)
            openMenu.SetActive(false);
        openMenu = null;
        openChevron?.SetExpanded(false); openChevron=null;
    }

    private void OnDisable()
    {
        if(detailsOverlay!=null) detailsOverlay.gameObject.SetActive(false);
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
        BookingOptionsChanged?.Invoke();
    }

    internal int AdjustFare(int economyFare) => Mathf.RoundToInt(economyFare * CabinFareMultipliers[cabin]);
    private int FareFor(Flight flight) => AdjustFare(flight.Price);

    private void RefreshResults()
    {
        foreach (var flight in flights)
        {
            flight.PriceLabel.text = $"${FareFor(flight):N0}";
            flight.TotalLabel.text=passengers>1 ? $"Est. total ${FareFor(flight)*passengers:N0} · {passengers} travelers" : "";
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
            flight.CardView.anchoredPosition = new Vector2(0, -count * 198);
            flight.CardView.SetSiblingIndex(count);
            flight.CardView.gameObject.SetActive(true);
            count++;
        }
        resultsContent.sizeDelta = new Vector2(0,Mathf.Max(resultsScroll.viewport.rect.height,count*198-18));
        resultCount.text = count == 1 ? "1 flight found" : $"{count} flights found";
        emptyResults.gameObject.SetActive(count == 0);
        RefreshTripDetails();
    }

    private bool MatchesFilters(Flight flight)
    {
        if (stopsFilter > 0 && flight.Stops > stopsFilter-1 ||
            airlineFilter > 0 && flight.Code != airlineCodes[airlineFilter - 1])
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
        int maximumDuration = durationFilter == 1 ? 360 : durationFilter == 2 ? 540 : 900;
        return timeMatches && priceMatches && (durationFilter == 0 || flight.DurationMinutes <= maximumDuration);
    }

    private void ConfigureFlightRoutes(string fromLabel,string toLabel)
    {
        TravelApp.FlightMap.FlightLocation Resolve(string label) => TravelApp.FlightMap.TravelLocationCatalog.Airports.FirstOrDefault(a=>
            a.BookingDestination==label || a.code==label || label.EndsWith("("+a.code+")",StringComparison.Ordinal));
        var from=Resolve(fromLabel); var to=Resolve(toLabel);
        foreach(var flight in flights)
        {
            flight.ConfigureRoute(from,to,routeTravelDate);
            flight.TimesLabel.text=flight.Departure+" – "+flight.Arrival.Replace(" days)","d)").Replace(" day)","d)");
            flight.StopsLabel.text=flight.ConnectionSummary;
            flight.DurationLabelView.text=DurationLabel(flight.DurationMinutes);
            flight.AirportLabel.text=flight.Segments[0].OriginCode+" → "+flight.Segments.Last().DestinationCode;
        }
    }

    private void BuildResultsScroll(DateSelectionPage style)
    {
        var root=style.Shape("Flight results scroll",transform,60,276,1416,620,new Color(0,0,0,0));
        root.GetComponent<Image>().raycastTarget=true;
        root.anchorMin=new Vector2(0,0); root.anchorMax=Vector2.one;
        root.offsetMin=new Vector2(60,30); root.offsetMax=new Vector2(-60,-276);
        resultsScroll=root.gameObject.AddComponent<ScrollRect>();
        var viewport=CreateRect("Viewport",root); viewport.anchorMax=Vector2.one;
        viewport.gameObject.AddComponent<RectMask2D>();
        resultsContent=CreateRect("Flight cards",viewport);
        resultsContent.anchorMin=Vector2.up; resultsContent.anchorMax=Vector2.one; resultsContent.pivot=Vector2.up;
        resultsScroll.viewport=viewport; resultsScroll.content=resultsContent;
        resultsScroll.horizontal=false; resultsScroll.vertical=true;
        resultsScroll.movementType=ScrollRect.MovementType.Clamped; resultsScroll.scrollSensitivity=50;
        var track=CreateRect("Flight scrollbar",root); track.anchorMin=Vector2.right; track.anchorMax=Vector2.one;
        track.offsetMin=new Vector2(12,0); track.offsetMax=new Vector2(24,0);
        track.gameObject.AddComponent<Image>().color=new Color32(223,228,235,255);
        var handle=CreateRect("Handle",track); handle.anchorMax=Vector2.one;
        var image=handle.gameObject.AddComponent<Image>(); image.color=Muted;
        var scrollbar=track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect=handle; scrollbar.targetGraphic=image;
        scrollbar.direction=Scrollbar.Direction.BottomToTop; resultsScroll.verticalScrollbar=scrollbar;
        resultsScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void BuildFlightDetails(DateSelectionPage style)
    {
        detailsStyle=style;
        detailsOverlay=CreateRect("Flight details overlay",transform); detailsOverlay.anchorMax=Vector2.one;
        var shade=detailsOverlay.gameObject.AddComponent<Image>(); shade.color=new Color(0.04f,.08f,.15f,.5f);
        var panel=style.Panel("Flight itinerary details",detailsOverlay,238,100,1060,740);
        detailsTitle=Text("Details title",panel,32,24,800,44,"",28,Ink,true,TextAlignmentOptions.Left);
        style.Control("Close flight details",panel,910,26,110,40,"Close",PaleBlue,Blue,19,2)
            .onClick.AddListener(()=>detailsOverlay.gameObject.SetActive(false));
        var root=style.Shape("Details scroll",panel,32,92,996,616,Color.clear);root.GetComponent<Image>().raycastTarget=true;
        var viewport=CreateRect("Viewport",root);viewport.anchorMax=Vector2.one;viewport.offsetMax=new Vector2(-20,0);viewport.gameObject.AddComponent<RectMask2D>();
        detailsContent=CreateRect("Itinerary sections",viewport);detailsContent.anchorMin=Vector2.up;detailsContent.anchorMax=Vector2.one;detailsContent.pivot=Vector2.up;
        detailsScroll=root.gameObject.AddComponent<ScrollRect>();detailsScroll.viewport=viewport;detailsScroll.content=detailsContent;detailsScroll.horizontal=false;detailsScroll.movementType=ScrollRect.MovementType.Clamped;detailsScroll.scrollSensitivity=50;
        var track=CreateRect("Scrollbar",root);track.anchorMin=Vector2.right;track.anchorMax=Vector2.one;track.offsetMin=new Vector2(-10,0);track.gameObject.AddComponent<Image>().color=PaleBlue;
        var handle=CreateRect("Handle",track);handle.anchorMax=Vector2.one;var image=handle.gameObject.AddComponent<Image>();image.color=Muted;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=image;bar.direction=Scrollbar.Direction.BottomToTop;detailsScroll.verticalScrollbar=bar;detailsScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        detailsOverlay.gameObject.AddComponent<KeyboardFocusScope>();
        detailsOverlay.gameObject.SetActive(false);
    }

    private void ShowFlightDetails(Flight flight)
    {
        CloseMenu();
        detailsTitle.text=flight.Airline+" · "+flight.Segments[0].OriginCode+" → "+flight.Segments.Last().DestinationCode;
        ItineraryDetailsView.Clear(detailsContent);
        ItineraryDetailsView.Build(detailsStyle,detailsContent,972,flight,CabinClass,FareFor(flight),passengers);
        detailsScroll.verticalNormalizedPosition=1;
        detailsOverlay.SetAsLastSibling(); detailsOverlay.gameObject.SetActive(true);
    }

    private static RectTransform CreateRect(string name,Transform parent)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=Vector2.zero; rect.pivot=new Vector2(.5f,.5f);
        rect.sizeDelta=Vector2.zero; return rect;
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
        if(layerName=="Wing photo")
        {
            float visibleWidth=Mathf.Min(1,card.sizeDelta.x/card.sizeDelta.y*texture.height/texture.width);
            image.uvRect=new Rect((1-visibleWidth)*.5f,0,visibleWidth,1);
        }
        image.color = tint;
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        Destroy(cardShading);
    }
}
