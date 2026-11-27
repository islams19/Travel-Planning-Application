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
    private TMP_Text legTitle;
    private bool returnLeg;
    private PassengerParty party = PassengerParty.Adults(1);
    private int passengers => party.Count;
    public PassengerParty Passengers => party;
    PassengerSelector passengerPopup;
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
    private readonly List<Toggle> airlineToggles=new List<Toggle>();
    private readonly HashSet<string> selectedAirlines = new HashSet<string>();
    private readonly List<Action> refreshFilterControls = new List<Action>();
    private RectTransform filterChips;
    private UnityEngine.UI.Button emptyClear;
    private Slider priceSlider, durationSlider;
    private TMP_Text priceLimitLabel, durationLimitLabel;
    private DateSelectionPage filterStyle;
    public int VisibleResultCount { get; private set; }
    public IReadOnlyCollection<string> SelectedAirlines => selectedAirlines;
    public int MaximumDurationMinutes => durationFilter;
    private static readonly string[] StopsOptions = { "Any", "Nonstop", "1 stop or fewer", "2 stops or fewer" };
    private int timeFilter;
    private static readonly string[] TimeOptions = { "Any time", "Morning · 6–12", "Afternoon · 12–18", "Evening · 18–24", "Night · 0–6" };
    private const int NoPriceLimit = int.MaxValue;
    private int PriceUpperBound => Math.Max(2000,(int)Math.Ceiling((flights.Count==0?2000:flights.Max(FareFor))/100d)*100+100);
    private bool refreshingFilterControls;
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
        public string CarrierLabel => FlightResultsPage.CarrierLabel(Airline,Segments);
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
        public int MockOptionIndex { get; }
        internal Flight(string id,string code,string airline,int departure,int price,int index,IReadOnlyList<Segment> segments,IReadOnlyList<Layover> layovers)
        {
            Id=id;Code=code;Airline=airline;Price=price;MockOptionIndex=index;DepartureTime=TimeSpan.FromMinutes(departure);
            Segments=segments;Layovers=layovers;DurationMinutes=(int)(segments.Last().ArrivalUtc-segments.First().DepartureUtc).TotalMinutes;Arrival=segments.Last().ArrivalLabel;
            Baggage=new BaggageAllowance(true,index%3==1?1:0,index%3==2?0:35+index%3*5);
        }
    }

    // The marketed airline plus any partner that operates a leg, e.g. "ANA + JetBlue".
    public static string CarrierLabel(string airline,IEnumerable<Segment> segments)
    {
        var partners=segments.Select(s=>s.Airline).Where(a=>!string.IsNullOrEmpty(a) && a!=airline).Distinct().ToArray();
        return partners.Length==0 ? airline : airline+" + "+string.Join(" + ",partners);
    }
    public static string DurationLabel(int minutes) => $"{minutes/60}h {minutes%60:00}m";
    public static string Clock(int minutes) => DateTime.Today.AddMinutes(minutes%1440).ToString("h:mm tt",CultureInfo.InvariantCulture) +
        (minutes>=1440 ? $" (+{minutes/1440} day{(minutes/1440==1 ? "" : "s")})" : "");
    public static int ExpectedSegments(string itineraryId)=>MockFlightGenerator.ExpectedSegments(itineraryId);

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
        // The page header already names the whole trip; this card names only the leg being chosen.
        returnLeg = back != null;
        route = Text("Destination", summary, 34, 21, 960, 36, "", 26, Ink, true, TextAlignmentOptions.Left);
        route.gameObject.SetActive(false);
        legTitle = Text("Leg title", summary, 34, 21, 960, 36, "", 26, Ink, true, TextAlignmentOptions.Left);
        dates = Text("Travel dates", summary, 34, 59, 960, 32, "", 20, Muted, false, TextAlignmentOptions.Left);
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

        filterStyle=style;
        BuildFilterChips(style);
        BuildResultsScroll(style);
        // Stable schedule IDs distinguish different itineraries from the same airline.

        ConfigureFlightRoutes("JFK", "LAX");
        BuildFlightDetails(style);
        BuildMenus(style, modify, filters, sortButton, editDestination, editDates, back);
        emptyClear=style.Control("Clear empty filters",transform,590,382,350,48,"Clear filters and show all flights",PaleBlue,Blue,19,2);
        emptyClear.onClick.AddListener(ClearFilters);
        RefreshResults();
    }

    public void ShowTrip(string routeLabel, string travelDates, DateTime? departureDate = null)
    {
        DateTime nextDate=departureDate?.Date ?? new DateTime(2027,1,15);
        bool sameTrip = route.text == routeLabel && this.travelDates == travelDates && routeTravelDate==nextDate;
        routeTravelDate=nextDate;
        route.text = routeLabel;
        legTitle.text = (returnLeg ? "Return flight" : "Departure flight") +
            (departureDate.HasValue ? "  ·  " + nextDate.ToString("dddd, MMM d", CultureInfo.InvariantCulture) : "");
        this.travelDates = travelDates;
        if (sameTrip)
        {
            RefreshResults();
            return;
        }
        var endpoints=routeLabel.Split('→');
        ConfigureFlightRoutes(endpoints.Length==2 ? endpoints[0].Trim() : "JFK", endpoints.Length==2 ? endpoints[1].Trim() : "LAX");
        resultsScroll.verticalNormalizedPosition=1;
        SelectedDeparture = null;
        RefreshSelectionButtons();
        if (selectedButtonLabel != null)
            selectedButtonLabel.text = "Select";
        selectedButtonLabel = null;
        RefreshResults();
    }

    internal void RestoreOptions(int count, string cabinName, string airlineCode, string itineraryId = null, PassengerParty passengerParty = null)
    {
        party = PassengerParty.Restore(passengerParty,count);
        cabin = Mathf.Max(0, Array.IndexOf(Cabins, cabinName));
        SelectedDeparture = !string.IsNullOrEmpty(itineraryId) ? flights.Find(flight => flight.Id==itineraryId) : flights.Find(flight => flight.Code == airlineCode);
        if (selectedButtonLabel != null) selectedButtonLabel.text = "Select";
        selectedButtonLabel = null;
        if (SelectedDeparture != null)
        {
            selectedButtonLabel = SelectedDeparture.CardView.GetComponentsInChildren<TMP_Text>()
                .FirstOrDefault(label => label.transform.parent.name == "Select");
            if (selectedButtonLabel != null) selectedButtonLabel.text = "Selected";
        }
        RefreshSelectionButtons();
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
        if(count!=passengers)party=PassengerParty.Adults(count);
        cabin=Mathf.Max(0,Array.IndexOf(Cabins,cabinName));
        foreach(var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if(button.name=="Passengers") button.GetComponentInChildren<TMP_Text>().text="Passengers: "+passengers;
            if(button.name=="Cabin") button.GetComponentInChildren<TMP_Text>().text="Cabin: "+CabinClass;
        }
        RefreshResults();
    }

    public bool SetPassengerCategories(int adults,int children,int infantsInSeat,int infantsOnLap)
    {
        if(!party.SetCounts(adults,children,infantsInSeat,infantsOnLap))return false;
        RefreshResults();return true;
    }
    internal void UsePassengers(PassengerParty value) {party=value;RefreshResults();}

    public void CopyBookingOptions(FlightResultsPage departureResults)
    {
        party = departureResults.party;
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
        // Prefer a photo of where this flight lands; fall back to the wing photos for other cities.
        var destinationPhoto=DestinationPhotos.ForAirport(flight.Segments.Count>0?flight.Segments[flight.Segments.Count-1].DestinationCode:null);
        var texture=destinationPhoto!=null?null:Resources.Load<Texture2D>("FlightWings/"+flight.Code)??Resources.Load<Texture2D>("FlightWings/"+new[]{"B6","DL","AA","UA"}[flight.MockOptionIndex%4]);
        if(destinationPhoto!=null)
        {
            var photo=new GameObject("Destination photo",typeof(RectTransform),typeof(Image));photo.layer=card.gameObject.layer;
            var rect=(RectTransform)photo.transform;rect.SetParent(accent,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=photo.GetComponent<Image>();image.sprite=destinationPhoto;image.preserveAspect=false;image.raycastTarget=false;
            var fitter=photo.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio=destinationPhoto.rect.width/Mathf.Max(1,destinationPhoto.rect.height);
            accent.gameObject.AddComponent<RectMask2D>();
        }
        else if(texture!=null)
        {
            AddBackdropLayer(accent,"Wing photo",texture,Color.white);
            AddBackdropLayer(accent,"Navy shading",cardShading,Color.white);
        }
        AirlineLogo.Build(style,card,200,20,106,42,flight.Code);
        var airlineName=Text("Airline",card,322,22,390,36,flight.CarrierLabel,23,Ink,true,TextAlignmentOptions.Left);
        airlineName.enableAutoSizing=true; airlineName.fontSizeMin=17; airlineName.fontSizeMax=23; airlineName.overflowMode=TextOverflowModes.Ellipsis;
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
        Text("Fare type",card,1188,65,196,24,"per adult",16,Muted,false,TextAlignmentOptions.Right);
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
            RefreshSelectionButtons();
            RefreshTripDetails();
            DepartureSelected?.Invoke();
        });
    }

    private void RefreshSelectionButtons()
    {
        foreach(var flight in flights)
        {
            if(flight.CardView==null)continue;
            var button=flight.CardView.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="Select");
            bool selected=ReferenceEquals(flight,SelectedDeparture);
            button.targetGraphic.color=selected || SelectedDeparture==null ? Blue : new Color32(232,236,243,255);
            var label=button.GetComponentInChildren<TMP_Text>();label.text=selected?"Selected":"Select";
            label.color=selected || SelectedDeparture==null?Color.white:Ink;
        }
    }

    private void BuildMenus(DateSelectionPage style, UnityEngine.UI.Button modify, UnityEngine.UI.Button filters,
        UnityEngine.UI.Button sortButton, Action editDestination, Action editDates, Action back)
    {
        var editMenu = style.Panel("Modify Search menu", transform, 996, 181, 480, 292);
        editMenu.GetComponent<Image>().raycastTarget = true;
        AddMenuButton(style, editMenu, 16, "Edit destination", editDestination);
        AddMenuButton(style, editMenu, 68, "Edit departure and return dates", editDates);
        var passengerButton=style.Control("Passengers",editMenu,16,120,448,44,"Travelers",PaleBlue,Blue,19,2);
        var passengerMenu=style.Panel("Result traveler choices",transform,1028,181,448,474);
        passengerMenu.GetComponent<Image>().raycastTarget=true;
        passengerPopup=passengerMenu.gameObject.AddComponent<PassengerSelector>();passengerPopup.Build(style,passengerMenu,this,CloseMenu);
        BindMenu(passengerButton,(RectTransform)passengerMenu.parent);
        AddChoice(style, editMenu, 172, "Cabin", Cabins, index => { cabin = index; RefreshResults(); }, () => cabin);
        AddMenuButton(style, editMenu, 224, "Done", CloseMenu);
        if (back == null)
            BindMenu(modify, (RectTransform)editMenu.parent);
        else
        {
            editMenu.parent.gameObject.SetActive(false);
            modify.onClick.AddListener(() => back());
        }

        var filterMenu = style.Panel("Filters menu", transform, 796, 181, 680, 568);
        filterMenu.GetComponent<Image>().raycastTarget = true;
        Text("Filters title",filterMenu,20,14,400,34,"Refine flights",24,Ink,true,TextAlignmentOptions.Left);
        Text("Stops label",filterMenu,20,60,620,26,"Stops",18,Ink,true,TextAlignmentOptions.Left);
        ChoiceRow(style,filterMenu,"Stops",94,StopsOptions,()=>stopsFilter,value=>stopsFilter=value);
        Text("Airlines label",filterMenu,20,147,620,26,"Airlines · all included when none checked",18,Ink,true,TextAlignmentOptions.Left);
        for(int i=0;i<4;i++)
        {
            int slot=i;string code=i<airlineCodes.Count?airlineCodes[i]:"";string airline=flights.FirstOrDefault(f=>f.Code==code)?.Airline??"";
            float x=20+(i%2)*330, y=180+(i/2)*42;
            var toggle=style.Shape("Airline "+code,filterMenu,x,y,310,36,PaleBlue,2).gameObject.AddComponent<Toggle>();
            toggle.GetComponent<Image>().raycastTarget=true;
            var box=style.Shape("Checkbox",toggle.transform,8,7,22,22,Color.white,3);
            box.GetComponent<Image>().sprite=null;
            var check=style.Shape("Check",box,4,4,14,14,Blue,3);
            check.GetComponent<Image>().sprite=null;
            toggle.targetGraphic=box.GetComponent<Image>(); toggle.graphic=check.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(selectedAirlines.Contains(code));
            Text("Airline name",toggle.transform,42,0,260,36,airline,18,Ink,false,TextAlignmentOptions.MidlineLeft);
            airlineToggles.Add(toggle);
            toggle.onValueChanged.AddListener(value=>{if(slot>=airlineCodes.Count)return;string chosen=airlineCodes[slot];if(value)selectedAirlines.Add(chosen);else selectedAirlines.Remove(chosen);RefreshResults();});
            refreshFilterControls.Add(()=>{
                bool visible=slot<airlineCodes.Count;toggle.gameObject.SetActive(visible);if(!visible)return;
                string chosen=airlineCodes[slot];toggle.name="Airline "+chosen;
                toggle.transform.Find("Airline name").GetComponent<TMP_Text>().text=flights.First(f=>f.Code==chosen).Airline;
                toggle.SetIsOnWithoutNotify(selectedAirlines.Contains(chosen));});
        }
        Text("Departure label",filterMenu,20,268,620,26,"Departure · airport-local time",18,Ink,true,TextAlignmentOptions.Left);
        ChoiceRow(style,filterMenu,"Departure",296,TimeOptions,()=>timeFilter,value=>timeFilter=value);
        priceLimitLabel=Text("Maximum fare label",filterMenu,20,354,620,28,"",18,Ink,false,TextAlignmentOptions.Left);
        priceSlider=FilterSlider(style,filterMenu,"Maximum fare",390,50,PriceUpperBound,PriceUpperBound,value=>maximumPrice=value>=(priceSlider==null?PriceUpperBound:priceSlider.maxValue)?NoPriceLimit:value);
        durationLimitLabel=Text("Maximum duration label",filterMenu,20,430,620,28,"",18,Ink,false,TextAlignmentOptions.Left);
        durationSlider=FilterSlider(style,filterMenu,"Maximum duration",466,60,MaximumAvailableDuration,MaximumAvailableDuration,value=>durationFilter=value>=MaximumAvailableDuration?0:value);
        var clear=style.Control("Clear Filters",filterMenu,20,510,300,42,"Clear filters",PaleBlue,Blue,19,2);
        clear.onClick.AddListener(ClearFilters);
        style.Control("Done",filterMenu,340,510,300,42,"Done",Blue,Color.white,19,2).onClick.AddListener(CloseMenu);
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

    private int MaximumAvailableDuration => Math.Max(120,(int)Math.Ceiling((flights.Count==0?120:flights.Max(f=>f.DurationMinutes))/60.0)*60);

    private void ChoiceRow(DateSelectionPage style,Transform parent,string name,float y,string[] choices,Func<int> current,Action<int> assign)
    {
        for(int i=0;i<choices.Length;i++)
        {
            int value=i;
            string caption=name=="Departure" ? new[]{"Any time","Morning\n6 AM–12 PM","Afternoon\n12 PM–6 PM","Evening\n6 PM–12 AM","Night\n12 AM–6 AM"}[i] : choices[i];
            var button=style.Control(name+" "+choices[i],parent,20+i*(640f/choices.Length),y,640f/choices.Length-8,name=="Departure"?48:40,caption,PaleBlue,Blue,16,2);
            button.onClick.AddListener(()=>{assign(value);RefreshResults();});
            refreshFilterControls.Add(()=>{
                button.GetComponent<Image>().color=current()==value?Blue:PaleBlue;
                button.GetComponentInChildren<TMP_Text>().color=current()==value?Color.white:Blue;
            });
        }
    }

    private Slider FilterSlider(DateSelectionPage style,Transform parent,string name,float y,int min,int max,int value,Action<int> assign)
    {
        var track=style.Shape(name,parent,32,y,596,24,PaleBlue,3);
        track.GetComponent<Image>().raycastTarget=true;
        var handle=style.Shape("Handle",track,0,0,24,28,Blue,3);
        handle.anchorMin=handle.anchorMax=new Vector2(0,.5f);handle.pivot=new Vector2(.5f,.5f);handle.anchoredPosition=Vector2.zero;handle.sizeDelta=new Vector2(24,0);
        var slider=track.gameObject.AddComponent<Slider>();slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();
        slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=true;slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v=>{if(refreshingFilterControls)return;assign((int)v);RefreshResults();});
        return slider;
    }

    public void ClearFilters()
    {
        stopsFilter=0;selectedAirlines.Clear();timeFilter=0;maximumPrice=NoPriceLimit;durationFilter=0;
        RefreshResults();
    }

    private void BuildFilterChips(DateSelectionPage style)
    {
        var viewport=style.Shape("Active filters",transform,60,245,1416,30,Color.clear);
        viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        filterChips=CreateRect("Filter chips",viewport);
        filterChips.anchorMin=filterChips.anchorMax=filterChips.pivot=Vector2.up;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=filterChips;
        scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
    }

    private void RefreshFilterControls()
    {
        refreshingFilterControls=true;
        foreach(var refresh in refreshFilterControls) refresh();
        if(priceSlider!=null)
        {
            priceSlider.maxValue=Math.Max(PriceUpperBound,maximumPrice==NoPriceLimit?0:maximumPrice+100);
            priceSlider.SetValueWithoutNotify(maximumPrice==NoPriceLimit?(int)priceSlider.maxValue:maximumPrice);
            priceLimitLabel.text=maximumPrice==NoPriceLimit?"Maximum price per adult · Any":$"Maximum price per adult · ${maximumPrice:N0}";
            durationSlider.maxValue=MaximumAvailableDuration;
            durationSlider.SetValueWithoutNotify(durationFilter==0?MaximumAvailableDuration:durationFilter);
            durationLimitLabel.text=durationFilter==0?"Maximum duration · Any (up to "+DurationLabel(MaximumAvailableDuration)+")":"Maximum duration · "+DurationLabel(durationFilter);
        }
        refreshingFilterControls=false;
        if(filterChips==null)return;
        foreach(Transform child in filterChips){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        float x=0;
        void Chip(string caption,Action remove)
        {
            float width=Mathf.Clamp(caption.Length*7.5f+36,110,255);
            var button=filterStyle.Control("Remove "+caption,filterChips,x,0,width,28,caption+"  ×",PaleBlue,Blue,15,2);
            button.onClick.AddListener(()=>{remove();RefreshResults();});x+=width+8;
        }
        if(stopsFilter>0)Chip(StopsOptions[stopsFilter],()=>stopsFilter=0);
        foreach(string code in airlineCodes.Where(selectedAirlines.Contains)) {string chosen=code;Chip(flights.First(f=>f.Code==chosen).Airline,()=>selectedAirlines.Remove(chosen));}
        if(timeFilter>0)Chip(TimeOptions[timeFilter],()=>timeFilter=0);
        if(maximumPrice!=NoPriceLimit)Chip("Up to $"+maximumPrice,()=>maximumPrice=NoPriceLimit);
        if(durationFilter>0)Chip("Up to "+DurationLabel(durationFilter),()=>durationFilter=0);
        filterChips.sizeDelta=new Vector2(x,30);
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
            ? $"Est. total ${party.TotalFare(FareFor(SelectedDeparture)):N0}"
            : lowestVisibleFare.HasValue
                ? $"Est. total from ${party.TotalFare(lowestVisibleFare.Value):N0}"
                : "Est. total unavailable";
        dates.text = $"{passengerLabel}  ·  {Cabins[cabin]}  ·  {total}";
        BookingOptionsChanged?.Invoke();
    }

    internal int AdjustFare(int economyFare) => Mathf.RoundToInt(economyFare * CabinFareMultipliers[cabin]);
    private int FareFor(Flight flight) => AdjustFare(flight.Price);

    private void RefreshResults()
    {
        passengerPopup?.Refresh();
        foreach (var flight in flights)
        {
            flight.PriceLabel.text = $"${FareFor(flight):N0}";
            flight.TotalLabel.text=passengers>1 ? $"Est. total ${party.TotalFare(FareFor(flight)):N0} · {passengers} travelers" : "";
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
        VisibleResultCount=count;
        emptyResults.gameObject.SetActive(count == 0);
        if(emptyClear!=null)emptyClear.gameObject.SetActive(count==0);
        RefreshFilterControls();
        RefreshTripDetails();
    }

    private bool MatchesFilters(Flight flight)
    {
        if (stopsFilter > 0 && flight.Stops > stopsFilter-1 ||
            selectedAirlines.Count > 0 && !selectedAirlines.Contains(flight.Code))
            return false;

        int hour = flight.DepartureTime.Hours;
        bool timeMatches = timeFilter switch
        {
            1 => hour >= 6 && hour < 12,
            2 => hour >= 12 && hour < 18,
            3 => hour >= 18,
            4 => hour < 6,
            _ => true
        };
        bool priceMatches = maximumPrice == NoPriceLimit || FareFor(flight) <= maximumPrice;
        return timeMatches && priceMatches && (durationFilter == 0 || flight.DurationMinutes <= durationFilter);
    }

    private void ConfigureFlightRoutes(string fromLabel,string toLabel)
    {
        TravelApp.FlightMap.FlightLocation Resolve(string label) => TravelApp.FlightMap.TravelLocationCatalog.Airports.FirstOrDefault(a=>
            a.BookingDestination==label || a.code==label || label.EndsWith("("+a.code+")",StringComparison.Ordinal));
        var from=Resolve(fromLabel); var to=Resolve(toLabel);
        from=from??TravelApp.FlightMap.TravelLocationCatalog.Find("JFK");to=to??TravelApp.FlightMap.TravelLocationCatalog.Find("LAX");
        foreach(var flight in flights){flight.CardView.gameObject.SetActive(false);Destroy(flight.CardView.gameObject);}flights.Clear();
        foreach(var flight in MockFlightGenerator.Generate(from,to,routeTravelDate))
        {
            AddFlight(filterStyle,flight);
            flight.TimesLabel.text=flight.Departure+" – "+flight.Arrival.Replace(" days)","d)").Replace(" day)","d)");
            flight.StopsLabel.text=flight.ConnectionSummary;flight.DurationLabelView.text=DurationLabel(flight.DurationMinutes);
            flight.AirportLabel.text=from.code+" → "+to.code;
        }
        airlineCodes.Clear();airlineCodes.AddRange(flights.Select(f=>f.Code).Distinct());
        selectedAirlines.RemoveWhere(code=>!airlineCodes.Contains(code));
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
        detailsTitle.text=flight.CarrierLabel+" · "+flight.Segments[0].OriginCode+" → "+flight.Segments.Last().DestinationCode;
        ItineraryDetailsView.Clear(detailsContent);
        ItineraryDetailsView.Build(detailsStyle,detailsContent,972,flight,CabinClass,FareFor(flight),passengers,null,party);
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
