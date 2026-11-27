using TMPro;
using UnityEngine;
using static DateSelectionPage;

// Read-only projection of the existing navigation step. It owns no booking data.
public sealed class BookingProgress : MonoBehaviour
{
    public enum State { Upcoming, Current, Completed, Incomplete }
    public static readonly string[] Steps = { "Search", "Flights", "Seats", "Review", "Payment" };
    static readonly Color Blue = new Color32(47,80,247,255), Muted = new Color32(96,113,139,255);
    TMP_Text summary, direction;
    readonly ReviewIcon[] icons = new ReviewIcon[5];
    static readonly ReviewIcon.Symbol[] Symbols = { ReviewIcon.Symbol.Search, ReviewIcon.Symbol.Flight, ReviewIcon.Symbol.Seat, ReviewIcon.Symbol.Review, ReviewIcon.Symbol.Payment };
    readonly TMP_Text[] labels = new TMP_Text[5];
    readonly UnityEngine.UI.Image[] indicators = new UnityEngine.UI.Image[5];
    public TripDraft.Step Step { get; private set; }
    public bool RoundTrip { get; private set; }

    public static State StateFor(TripDraft.Step step, bool roundTrip, int index)
    {
        int current = step == TripDraft.Step.Search || step == TripDraft.Step.Dates ? 0 :
            step == TripDraft.Step.DepartureFlights || step == TripDraft.Step.ReturnFlights ? 1 :
            step == TripDraft.Step.DepartureSeats || step == TripDraft.Step.ReturnSeats ? 2 :
            step == TripDraft.Step.Review ? 3 : 4;
        if(index == current) return State.Current;
        if(roundTrip && index == 1 && step == TripDraft.Step.DepartureSeats) return State.Incomplete;
        if(roundTrip && index == 2 && step == TripDraft.Step.ReturnFlights) return State.Incomplete;
        return index < current ? State.Completed : State.Upcoming;
    }
    public static BookingProgress Build(DateSelectionPage style, Transform parent, float y = 8)
    {
        var surface = style.Panel("Booking progress", parent, 60, y, 1416, 48);
        var view = surface.gameObject.AddComponent<BookingProgress>();
        // Route and dates already appear in the page header and search form, so the bar only says which leg this is.
        view.summary = Text("Route and dates",surface,16,5,0,36,"",17,Muted,false,TextAlignmentOptions.MidlineLeft);
        view.summary.gameObject.SetActive(false);
        view.direction = Text("Journey leg",surface,20,6,480,34,"",17,Muted,true,TextAlignmentOptions.MidlineLeft);
        for(int i=0;i<5;i++)
        {
            float x=720+i*136;
            ReviewIcon.Add(surface,x,12,22,Symbols[i],Blue);
            view.icons[i]=surface.GetChild(surface.childCount-1).GetComponent<ReviewIcon>();
            view.labels[i]=Text("Progress "+Steps[i],surface,x+27,6,100,30,Steps[i],17,Muted,false,TextAlignmentOptions.MidlineLeft);
            var line=style.Shape("State "+Steps[i],surface,x,42,120,3,Muted);
            view.indicators[i]=line.GetComponent<UnityEngine.UI.Image>();
            view.indicators[i].sprite=null;
        }
        return view;
    }
    public void Show(TripDraft.Step step, bool roundTrip, string context)
    {
        Step=step; RoundTrip=roundTrip;
        bool flightsOrSeats=step==TripDraft.Step.DepartureFlights || step==TripDraft.Step.DepartureSeats || step==TripDraft.Step.ReturnFlights || step==TripDraft.Step.ReturnSeats;
        direction.text=roundTrip && flightsOrSeats ?
            (step==TripDraft.Step.ReturnFlights || step==TripDraft.Step.ReturnSeats ? "Return · 2 of 2" : "Departure · 1 of 2") : roundTrip ? "Round trip" : "One way"; summary.text=context;
        for(int i=0;i<5;i++)
        {
            var state=StateFor(step,roundTrip,i);
            labels[i].text=Steps[i];
            labels[i].color=state==State.Current?Blue:Muted;
            labels[i].fontStyle=state==State.Current?FontStyles.Bold:FontStyles.Normal;
            icons[i].SetSymbol(state==State.Completed?ReviewIcon.Symbol.Completed:Symbols[i]);
            icons[i].color=state==State.Completed?new Color32(38,151,119,255):state==State.Current?Blue:Muted;
            indicators[i].color=state==State.Completed?new Color32(38,151,119,255):state==State.Current?Blue:new Color32(222,230,241,255);
        }
    }
}
