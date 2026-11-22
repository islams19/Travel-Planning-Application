using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

public sealed class SeatSelectionPage : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29, 40, 59, 255);
    private static readonly Color Muted = new Color32(96, 113, 139, 255);
    private static readonly Color Blue = new Color32(47, 80, 247, 255);
    private static readonly Color PaleBlue = new Color32(235, 240, 255, 255);
    private static readonly Color Background = new Color32(245, 247, 250, 255);
    private static readonly Color Unavailable = new Color32(219, 226, 236, 255);
    private readonly List<Seat> seats = new List<Seat>();
    private readonly List<UnityEngine.UI.Button> travelers = new List<UnityEngine.UI.Button>();
    private DateSelectionPage style;
    private ScrollRect scroll;
    private RectTransform map;
    private RectTransform travelerPanel;
    private TMP_Text instruction;
    private TMP_Text cabin;
    private TMP_Text airline;
    private TMP_Text destination;
    private TMP_Text times;
    private TMP_Text dates;
    private TMP_Text fare;
    private TMP_Text total;
    private TMP_Text status;
    private UnityEngine.UI.Button confirm;
    private int activeTraveler;
    private int activeSegment;
    private RectTransform segmentPanel;
    private readonly List<UnityEngine.UI.Button> segmentButtons = new List<UnityEngine.UI.Button>();

    public Booking CurrentBooking { get; private set; }
    public event Action SeatsConfirmed;

    public sealed class Booking
    {
        private readonly string[][] assignments;
        public string Origin { get; }
        public string Destination { get; }
        public DateTime DepartureDate { get; }
        public DateTime? ReturnDate { get; }
        public string Cabin { get; }
        public FlightResultsPage.Flight Flight { get; }
        public int FarePerTraveler { get; }
        public IReadOnlyList<string> Seats => SegmentSeats[0];
        public IReadOnlyList<IReadOnlyList<string>> SegmentSeats { get; }
        public IReadOnlyList<string> SeatsForSegment(int segment) => SegmentSeats[segment];
        public bool HasAllSeats => assignments.All(row=>row.All(seat=>!string.IsNullOrWhiteSpace(seat)) && row.Distinct().Count()==PassengerCount);
        public int AssignedSeatCount => assignments.Sum(row=>row.Count(seat=>!string.IsNullOrWhiteSpace(seat)));
        public bool Confirmed { get; internal set; }
        public int PassengerCount => assignments[0].Length;

        internal Booking(string origin, string destination, DateTime departure, DateTime? returnDate, int passengers,
            string cabin, FlightResultsPage.Flight flight, int fare)
        {
            Origin = origin;
            Destination = destination;
            DepartureDate = departure;
            ReturnDate = returnDate;
            Cabin = cabin;
            Flight = flight;
            FarePerTraveler = fare;
            assignments = Enumerable.Range(0,flight.Segments.Count).Select(_=>new string[passengers]).ToArray();
            SegmentSeats = Array.AsReadOnly(assignments.Select(row=>(IReadOnlyList<string>)Array.AsReadOnly(row)).ToArray());
        }

        internal int TravelerFor(string seat,int segment=0) => Array.IndexOf(assignments[segment], seat);

        internal void Assign(int traveler, string seat,int segment=0)
        {
            assignments[segment][traveler] = seat;
            Confirmed = false;
        }
    }

    private sealed class Seat
    {
        public string Code;
        public bool Unavailable;
        public UnityEngine.UI.Button Button;
        public TMP_Text Label;
    }

    public void Build(DateSelectionPage dateStyle, Action back)
    {
        style = dateStyle;
        var background = gameObject.AddComponent<Image>();
        background.color = Background;
        background.raycastTarget = false;

        var viewport = CreateRect("Seat viewport", transform);
        viewport.anchorMax = Vector2.one;
        viewport.offsetMax = new Vector2(-20, 0);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Background;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Scrollable seat content", viewport);
        content.anchorMin = Vector2.up;
        content.anchorMax = Vector2.one;
        content.pivot = Vector2.up;
        content.sizeDelta = new Vector2(20, 1470);
        scroll = gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50;
        AddScrollbar();

        style.Control("Back to flights", content, 60, 34, 192, 44,
            "‹  Flight details", PaleBlue, Blue, 19, 2).onClick.AddListener(() => back());
        Text("Booking step", content, 1040, 34, 436, 44, "01  Flight     /     02  Seats",
            20, Blue, true, TextAlignmentOptions.Right);

        segmentPanel = style.Shape("Flight segments", content, 60, 88, 1416, 54, Background);
        segmentPanel.gameObject.SetActive(false);
        var mapPanel = style.Panel("Seat map panel", content, 60, 105, 840, 1290);
        Text("Map title", mapPanel, 34, 30, 650, 42, "Find your favorite spot", 28, Ink, true, TextAlignmentOptions.Left);
        instruction = Text("Map instructions", mapPanel, 34, 78, 770, 34, "", 20, Muted, false, TextAlignmentOptions.Left);
        Legend(mapPanel, 34, "Available", PaleBlue);
        Legend(mapPanel, 225, "Selected", Blue);
        Legend(mapPanel, 416, "Unavailable", Unavailable);
        var backdrop = style.Shape("Cabin backdrop", mapPanel, 34, 176, 768, 1050, Background, 1.3f);
        map = style.Panel("Aircraft cabin", backdrop, 120, 28, 528, 990);
        Text("Aircraft front", map, 24, 30, 474, 34, "FRONT ↑", 18, Muted, true);
        cabin = Text("Booked cabin", map, 24, 78, 474, 42, "", 24, Ink, true);
        Text("Aircraft rear", map, 24, 925, 474, 34, "REAR", 18, Muted, true);
        Text("Map disclaimer", mapPanel, 34, 1230, 770, 38,
            "Choose an available seat", 18, Muted, false, TextAlignmentOptions.Left);

        var flight = style.Panel("Flight summary", content, 940, 105, 536, 248);
        airline = SummaryText("Airline", flight, 27, 28, true);
        destination = SummaryText("Destination", flight, 72, 22, true);
        times = SummaryText("Times and duration", flight, 111, 21, true);
        dates = SummaryText("Trip dates", flight, 153, 18, false);
        fare = SummaryText("Cabin and fare", flight, 192, 19, false);

        var summary = style.Panel("Seat summary", content, 940, 386, 536, 468);
        SummaryText("Your selected seats", summary, 26, 27, true).text = "Your selected seats";
        travelerPanel = style.Shape("Travelers", summary, 32, 79, 468, 124, Background, 2);
        SummaryText("Seat fee", summary, 213, 19, false).text = "Seat fee: included";
        total = SummaryText("Estimated total", summary, 254, 24, true);
        confirm = style.Control("Confirm Seat", summary, 32, 310, 468, 64,
            "Confirm Seat / Continue", Blue, Color.white, 22);
        confirm.transition = Selectable.Transition.ColorTint;
        var colors = confirm.colors;
        colors.disabledColor = new Color32(150, 160, 188, 255);
        confirm.colors = colors;
        confirm.onClick.AddListener(ConfirmSeats);
        status = SummaryText("Seat status", summary, 393, 19, false);
    }

    public void ShowBooking(string origin, string destinationName, DateTime departure, DateTime? returnDate, FlightResultsPage results)
    {
        var previous = CurrentBooking;
        if (previous == null || previous.Origin != origin || previous.Destination != destinationName || previous.DepartureDate != departure ||
            previous.ReturnDate != returnDate || previous.PassengerCount != results.PassengerCount ||
            previous.Cabin != results.CabinClass || previous.Flight != results.SelectedDeparture)
        {
            CurrentBooking = new Booking(origin, destinationName, departure, returnDate, results.PassengerCount,
                results.CabinClass, results.SelectedDeparture, results.SelectedFarePerTraveler);
            activeTraveler = activeSegment = 0;
            RebuildMap();
            BuildSegmentControls();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
        }

        var booking = CurrentBooking;
        RefreshSegmentInformation();
        fare.text = $"{booking.Cabin}  ·  ${booking.FarePerTraveler:N0} per traveler";
        total.text = $"Estimated total  ·  ${booking.FarePerTraveler * booking.PassengerCount:N0} USD";
        RefreshSeats();
    }

    internal void ClearBooking() => CurrentBooking = null;
    internal void RestoreAssignments(TripDraft.Leg saved)
    {
        if (CurrentBooking == null || saved == null) return;
        for(int segment=0;segment<CurrentBooking.Flight.Segments.Count;segment++)
        {
            var flight=CurrentBooking.Flight.Segments[segment];
            var savedSegment=saved.segments!=null && segment<saved.segments.Length ? saved.segments[segment] : null;
            string[] values=savedSegment!=null && savedSegment.originCode==flight.OriginCode && savedSegment.destinationCode==flight.DestinationCode
                ? savedSegment.seats : segment==0 && (saved.segments==null || saved.segments.Length==0) ? saved.seats : null;
            for(int i=0;i<Mathf.Min(values?.Length ?? 0,CurrentBooking.PassengerCount);i++)
            {
                var seat=seats.Find(candidate=>candidate.Code==values[i] && !candidate.Unavailable);
                if(seat!=null && CurrentBooking.TravelerFor(seat.Code,segment)<0) CurrentBooking.Assign(i,seat.Code,segment);
            }
        }
        CurrentBooking.Confirmed = saved.confirmed && CurrentBooking.HasAllSeats;
        activeSegment=0;
        for(int segment=0;segment<CurrentBooking.SegmentSeats.Count;segment++)
            if(CurrentBooking.SeatsForSegment(segment).Any(seat=>seat==null)) { activeSegment=segment; break; }
        SelectSegment(activeSegment);
    }

    private void BuildSegmentControls()
    {
        foreach(var button in segmentButtons) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
        segmentButtons.Clear();
        int count=CurrentBooking.Flight.Segments.Count;
        segmentPanel.gameObject.SetActive(count>1);
        if(count>1) for(int i=0;i<count;i++)
        {
            int segment=i; var flight=CurrentBooking.Flight.Segments[i];
            float width=(1416-16*(count-1))/count;
            var button=style.Control("Segment "+(i+1),segmentPanel,i*(width+16),0,width,46,
                $"{i+1} · {flight.OriginCode} → {flight.DestinationCode}",PaleBlue,Blue,21,2);
            button.onClick.AddListener(()=>SelectSegment(segment)); segmentButtons.Add(button);
        }
        float shift=count>1 ? 64 : 0;
        var content=scroll.content;
        foreach(var pair in new[]{("Seat map panel",105f),("Flight summary",105f),("Seat summary",386f),("Save for Later",880f)})
        {
            var rect=content.Find(pair.Item1) as RectTransform;
            if(rect!=null) rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-pair.Item2-shift);
        }
        content.sizeDelta=new Vector2(content.sizeDelta.x,1470+shift);
    }

    public void SelectSegment(int segment)
    {
        if(CurrentBooking==null || segment<0 || segment>=CurrentBooking.SegmentSeats.Count) return;
        activeSegment=segment; activeTraveler=0;
        for(int i=0;i<CurrentBooking.PassengerCount;i++)
            if(CurrentBooking.SeatsForSegment(segment)[i]==null) { activeTraveler=i; break; }
        RefreshSeats();
    }

    private void RefreshSegmentInformation()
    {
        var flight=CurrentBooking.Flight.Segments[activeSegment];
        bool connecting=CurrentBooking.Flight.Stops>0;
        airline.text=flight.Airline+(connecting ? " · "+flight.FlightNumber : "");
        destination.text=connecting ? flight.OriginCode+" → "+flight.DestinationCode : CurrentBooking.Origin+" → "+CurrentBooking.Destination;
        times.text=flight.DepartureLabel+" – "+flight.ArrivalLabel+" · "+FlightResultsPage.DurationLabel(flight.DurationMinutes);
        dates.text=flight.DepartureLocal.ToString("MMM d, yyyy",CultureInfo.InvariantCulture)+(connecting ? $" · Segment {activeSegment+1} of {CurrentBooking.SegmentSeats.Count}" : "");
        foreach(var button in segmentButtons)
        {
            bool active=segmentButtons.IndexOf(button)==activeSegment;
            button.targetGraphic.color=active ? Blue : PaleBlue;
            button.GetComponentInChildren<TMP_Text>().color=active ? Color.white : Blue;
        }
    }

    private void RebuildMap()
    {
        foreach (var seat in seats)
        {
            seat.Button.gameObject.SetActive(false);
            Destroy(seat.Button.gameObject);
        }
        seats.Clear();
        foreach (var button in travelers)
        {
            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }
        travelers.Clear();

        cabin.text = CurrentBooking.Cabin + " cabin";
        bool wide = CurrentBooking.Cabin == "Economy" || CurrentBooking.Cabin == "Premium Economy";
        string letters = wide ? "ABCDEF" : "ABCD";
        int rows = CurrentBooking.Cabin == "First" ? 4 : CurrentBooking.Cabin == "Business" ? 7 : 14;
        for (int row = 1; row <= rows; row++)
        for (int column = 0; column < letters.Length; column++)
        {
            string code = row.ToString(CultureInfo.InvariantCulture) + letters[column];
            float x = (wide ? 40 : 101) + column * 66 + (column >= letters.Length / 2 ? 48 : 0);
            var button = style.Control("Seat " + code, map, x, 130 + (row - 1) * 54, 59, 46,
                code, PaleBlue, Blue, 19, 3);
            var seat = new Seat
            {
                Code = code,
                Unavailable = (row * 3 + column * 2) % 7 == 0,
                Button = button,
                Label = button.GetComponentInChildren<TMP_Text>()
            };
            button.onClick.AddListener(() => AssignSeat(seat));
            seats.Add(seat);
        }

        for (int i = 0; i < CurrentBooking.PassengerCount; i++)
        {
            int traveler = i;
            var button = style.Control("Traveler " + (i + 1), travelerPanel,
                9 + i % 3 * 151, 8 + i / 3 * 56, 144, 50, "", PaleBlue, Blue, 18, 3);
            button.onClick.AddListener(() => { activeTraveler = traveler; RefreshSeats(); });
            travelers.Add(button);
        }
    }

    private void AssignSeat(Seat seat)
    {
        if (seat.Unavailable || CurrentBooking.TravelerFor(seat.Code,activeSegment) >= 0)
            return;

        CurrentBooking.Assign(activeTraveler, seat.Code,activeSegment);
        for (int i = 0; i < CurrentBooking.PassengerCount; i++)
        {
            if (CurrentBooking.SeatsForSegment(activeSegment)[i] != null)
                continue;
            activeTraveler = i;
            break;
        }
        RefreshSeats();
    }

    private void RefreshSeats()
    {
        var booking = CurrentBooking;
        RefreshSegmentInformation();
        instruction.text = (booking.Flight.Stops>0 ? $"Segment {activeSegment+1} of {booking.SegmentSeats.Count} · " : "")+$"Choose a seat for traveler {activeTraveler + 1} of {booking.PassengerCount}.";
        foreach (var seat in seats)
        {
            bool assigned = booking.TravelerFor(seat.Code,activeSegment) >= 0;
            seat.Button.interactable = !seat.Unavailable && !assigned;
            seat.Button.targetGraphic.color = assigned ? Blue : seat.Unavailable ? Unavailable : PaleBlue;
            seat.Label.color = assigned ? Color.white : seat.Unavailable ? Muted : Blue;
        }

        int assignedCount = 0;
        for (int i = 0; i < travelers.Count; i++)
        {
            string seat = booking.SeatsForSegment(activeSegment)[i];
            if (seat != null)
                assignedCount++;
            bool active = i == activeTraveler;
            var button = travelers[i];
            button.targetGraphic.color = active ? Blue : PaleBlue;
            var label = button.GetComponentInChildren<TMP_Text>();
            label.color = active ? Color.white : Blue;
            label.text = $"P{i + 1}  ·  {seat ?? "Choose"}";
        }

        confirm.interactable = booking.HasAllSeats;
        status.text = booking.Confirmed
            ? "Seats confirmed for this booking."
            : $"{booking.AssignedSeatCount} of {booking.PassengerCount*booking.SegmentSeats.Count} seats assigned";
    }

    private void ConfirmSeats()
    {
        if (!confirm.interactable)
            return;
        CurrentBooking.Confirmed = true;
        RefreshSeats();
        SeatsConfirmed?.Invoke();
    }

    private void Legend(Transform parent, float x, string caption, Color color)
    {
        style.Shape(caption + " key", parent, x, 128, 24, 24, color, 4);
        Text(caption, parent, x + 34, 123, 150, 34, caption, 18, Muted, false, TextAlignmentOptions.Left);
    }

    private static TMP_Text SummaryText(string name, Transform parent, float y, float size, bool bold)
    {
        var label = Text(name, parent, 32, y, 468, 35, "", size, bold ? Ink : Muted, bold, TextAlignmentOptions.Left);
        label.enableAutoSizing = true;
        label.fontSizeMin = 15;
        label.fontSizeMax = size;
        return label;
    }

    private void AddScrollbar()
    {
        var track = CreateRect("Seat scrollbar", transform);
        track.anchorMin = Vector2.right;
        track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(-16, 0);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = Unavailable;
        var slidingArea = CreateRect("Sliding area", track);
        slidingArea.anchorMax = Vector2.one;
        var handle = CreateRect("Handle", slidingArea);
        handle.anchorMax = Vector2.one;
        var image = handle.gameObject.AddComponent<Image>();
        image.color = Muted;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = image;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
