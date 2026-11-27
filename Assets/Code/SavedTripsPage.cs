using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

// One trips view; drafts and paid receipts keep their existing storage and ownership rules.
public sealed class SavedTripsPage : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29,40,59,255);
    private static readonly Color Muted = new Color32(96,113,139,255);
    private static readonly Color Blue = new Color32(47,80,247,255);
    private static readonly Color Pale = new Color32(235,240,255,255);
    private DateSelectionPage style;
    private RectTransform content;
    private TMP_Text empty;
    private Action<TripDraft> resume;
    private Action<CompletedDemoBooking> openBooking;
    private int filter, sort;
    private AppDialog dialog;
    private AppNotification notifications;
    private GameObject sortMenu;
    private DropdownChevron sortChevron;
    private static readonly string[] SortLabels={"Newest saved","Oldest saved","Upcoming travel date"};
    private sealed class Entry { public DateTime Saved;public DateTime? Travel;public string Id;public Action<int> Render; }

    private readonly UnityEngine.UI.Button[] tabs = new UnityEngine.UI.Button[4];
    public void Build(DateSelectionPage dateStyle, Action<TripDraft> reopen, Action newTrip, Action<CompletedDemoBooking> open)
    {
        style = dateStyle; resume = reopen; openBooking = open;
        dialog=AppDialog.Get(style,transform);notifications=AppNotification.Get(style,transform);
        var background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        background.color = new Color32(245,247,250,255);
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.raycastTarget = false;
        Text("Trip explanation",transform,60,26,750,40,"All your plans and bookings in one place",22,Muted,false,TextAlignmentOptions.Left);
        style.Control("Plan another trip",transform,1210,22,266,46,"Plan another trip",Pale,Blue,20,2).onClick.AddListener(()=>newTrip());
        var sortButton=style.Control("Sort trips",transform,840,22,340,46,"Sort: Newest saved",Pale,Blue,19,2);
        var chevron=DropdownChevron.Attach(sortButton);sortChevron=chevron;
        var menu=style.Panel("Trip sorting",transform,840,76,340,174);menu.GetComponent<Image>().raycastTarget=true;
        sortMenu=menu.parent.gameObject;sortMenu.SetActive(false);
        for(int i=0;i<SortLabels.Length;i++)
        {
            int choice=i;
            style.Control(SortLabels[i],menu,12,12+i*52,312,44,SortLabels[i],Pale,Blue,18,2).onClick.AddListener(()=>{
                sort=choice;sortButton.GetComponentInChildren<TMP_Text>().text="Sort: "+SortLabels[choice];sortMenu.SetActive(false);chevron.SetExpanded(false);Refresh();});
        }
        sortButton.onClick.AddListener(()=>{sortMenu.SetActive(!sortMenu.activeSelf);chevron.SetExpanded(sortMenu.activeSelf);sortMenu.transform.SetAsLastSibling();});
        string[] labels={"All trips","Incomplete","Needs Payment","Paid"};
        for(int i=0;i<labels.Length;i++)
        {
            int choice=i;
            tabs[i]=style.Control("Filter "+labels[i],transform,60+i*350,83,326,46,labels[i],Pale,Blue,20,2);
            tabs[i].onClick.AddListener(()=>{filter=choice;Refresh();});
        }
        empty=Text("No trips",transform,60,164,1416,80,"No trips in this category yet.",24,Ink,false,TextAlignmentOptions.Left);
        var viewport=Rect("Trips viewport",transform);viewport.offsetMin=new Vector2(60,30);viewport.offsetMax=new Vector2(-60,-153);
        viewport.gameObject.AddComponent<Image>().color=background.color;viewport.gameObject.AddComponent<RectMask2D>();
        content=Rect("Trips list",viewport);content.anchorMin=Vector2.up;content.pivot=Vector2.up;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
        scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=50;
        Refresh();
    }
    private void OnEnable() { if(content!=null) Refresh(); }
    private void OnDisable() {if(sortMenu!=null)sortMenu.SetActive(false);sortChevron?.SetExpanded(false); }
    public void Refresh()
    {
        if(content==null)return;
        foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        var runtime=TravelAppRuntime.Instance;
        var drafts=runtime.Services.DraftTrips;var paid=runtime.CompletedFlights;
        int incomplete=drafts.Count(d=>!d.NeedsPayment), needsPayment=drafts.Count(d=>d.NeedsPayment);
        string[] labels={$"All trips ({drafts.Count+paid.Count})",$"Incomplete ({incomplete})",$"Needs Payment ({needsPayment})",$"Paid ({paid.Count})"};
        for(int i=0;i<tabs.Length;i++)
        {
            tabs[i].GetComponentInChildren<TMP_Text>().text=labels[i];
            tabs[i].targetGraphic.color=i==filter?Blue:Pale;
            tabs[i].GetComponentInChildren<TMP_Text>().color=i==filter?Color.white:Blue;
        }
        var entries=new List<Entry>();
        foreach(var draft in drafts)
        {
            bool ready=draft.NeedsPayment;
            if(filter==3 || filter==1 && ready || filter==2 && !ready)continue;
            entries.Add(new Entry{Id=draft.id,Saved=SavedDate(draft.updatedUtc),Travel=draft.Departure,Render=row=>RenderDraft(draft,row)});
        }
        if(filter==0 || filter==3)foreach(var record in paid)
            entries.Add(new Entry{Id=record.Reference,Saved=record.CreatedAtUtc,Travel=record.Booking.DepartureDate,Render=row=>RenderPaid(record,row)});
        IEnumerable<Entry> ordered=sort==1?entries.OrderBy(e=>e.Saved).ThenBy(e=>e.Id):sort==2?
            entries.OrderBy(e=>e.Travel.HasValue && e.Travel.Value.Date>=DateTime.Today?e.Travel.Value.Date:DateTime.MaxValue)
                .ThenBy(e=>e.Travel??DateTime.MaxValue).ThenByDescending(e=>e.Saved).ThenBy(e=>e.Id):
            entries.OrderByDescending(e=>e.Saved).ThenBy(e=>e.Id);
        int index=0;foreach(var entry in ordered)entry.Render(index++);
        string[] emptyStates={"No trips yet. Save a plan or complete a booking to see it here.",
            "No incomplete trips. Save your plans to pick them up here later.",
            "Nothing waiting for payment. Finish your flights and seats to review and pay.",
            "No paid trips yet. Completed bookings will appear here after payment."};
        empty.text=emptyStates[filter];empty.textWrappingMode=TextWrappingModes.Normal;
        empty.gameObject.SetActive(index==0);content.parent.gameObject.SetActive(index>0);
        content.sizeDelta=new Vector2(0,Mathf.Max(800,index*272));
    }
    private void RenderDraft(TripDraft draft,int row)
    {
        bool ready=draft.NeedsPayment;
        var panel=Card(draft.id,row,ready?"Needs Payment":"Incomplete",ready?new Color32(143,94,15,255):Blue,ready?new Color32(255,244,218,255):Pale);
        var name=Text("Trip name",panel,240,14,850,32,TripName(draft),21,Ink,true,TextAlignmentOptions.MidlineLeft);name.overflowMode=TextOverflowModes.Ellipsis;name.richText=false;
        Route(panel,Display(draft.origin,"Choose origin")+" → "+Display(draft.destination,"Choose destination"));
        string dates=draft.Departure.HasValue?draft.Departure.Value.ToString("MMM d, yyyy"):"Choose dates";
        if(draft.Return.HasValue)dates+=" – "+draft.Return.Value.ToString("MMM d, yyyy");
        Text("Trip details",panel,24,100,1050,104,$"{dates} · {draft.PassengerDetails.Count} traveler(s) · {draft.cabin}\n"+
            (ready?"Ready to review and pay · Not booked":$"Resume: {StepName(draft.step)} · Not booked")+DraftSegments(draft),18,Muted,false,TextAlignmentOptions.TopLeft);
        Text("Last saved",panel,24,222,1050,24,"Last saved: "+LocalDate(SavedDate(draft.updatedUtc)),15,Muted,false,TextAlignmentOptions.MidlineLeft);
        style.Control("Resume draft",panel,1128,64,226,48,ready?"Review & Pay":"Resume",Pale,Blue,20,2).onClick.AddListener(()=>resume(draft));
        style.Control("Rename trip",panel,1128,124,226,38,"Rename",Pale,Blue,18,2).onClick.AddListener(()=>Rename(draft));
        style.Control("Delete trip",panel,1128,174,226,38,"Delete",Pale,Blue,18,2).onClick.AddListener(()=>Delete(draft));
    }
    private void RenderPaid(CompletedDemoBooking record,int row)
    {
        var booking=record.Booking;
        var panel=Card(record.Reference,row,"Paid",new Color32(22,128,87,255),new Color32(228,246,237,255));
        Text("Trip name",panel,240,14,850,32,"Booking "+record.Reference,21,Ink,true,TextAlignmentOptions.MidlineLeft);
        Route(panel,booking.Origin+" → "+booking.Destination);
        Text("Trip details",panel,24,100,1050,90,$"{booking.DepartureDate:MMM d, yyyy}"+
            (booking.ReturnDate.HasValue?$" – {booking.ReturnDate.Value:MMM d, yyyy}":" · One way")+
            $" · {booking.PassengerCount} traveler(s) · {booking.CabinClass}\n{record.Reference} · Total ${booking.EstimatedTotal:N0} {booking.Currency}",19,Muted,false,TextAlignmentOptions.TopLeft);
        Text("Booked at",panel,24,222,1050,24,"Booked: "+LocalDate(record.CreatedAtUtc),15,Muted,false,TextAlignmentOptions.MidlineLeft);
        style.Control("View details",panel,1128,74,226,52,"View details",Pale,Blue,20,2).onClick.AddListener(()=>openBooking(record));
    }
    public static string TripName(TripDraft draft)=>string.IsNullOrWhiteSpace(draft.title)?
        string.IsNullOrWhiteSpace(draft.destination)?"Untitled trip":"Trip to "+draft.destination.Split(',')[0]:draft.title;
    private static DateTime SavedDate(string value)=>DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var date)?date.ToUniversalTime():DateTime.MinValue;
    private static string LocalDate(DateTime date)=>date==DateTime.MinValue?"Not available":date.ToLocalTime().ToString("MMM d, yyyy · h:mm tt",CultureInfo.InvariantCulture);
    private void Rename(TripDraft draft)
    {
        dialog.Show("Rename saved trip","Choose a name to find this plan easily.",new[]{"Cancel","Save name"},choice=>{
            if(choice==0)return true;
            var result=TravelAppRuntime.Instance.Services.RenameDraft(draft.id,dialog.InputText);
            if(!result.success){dialog.Error(result.message);return false;}
            Refresh();notifications.Show("Trip renamed");return true;
        },TripName(draft));
    }
    private void Delete(TripDraft draft)
    {
        dialog.Show("Delete saved trip?",TripName(draft)+" will be removed from your saved plans.",new[]{"Cancel","Delete saved trip"},choice=>{
            if(choice==0)return true;
            var result=TravelAppRuntime.Instance.Services.RemoveDraft(draft.id);
            if(!result.success){dialog.Error(result.message);return false;}
            Refresh();notifications.Show("Saved trip deleted");return true;
        });
    }
    private static string DraftSegments(TripDraft draft)
    {
        string Leg(string title,TripDraft.Leg leg)
        {
            if(leg?.segments==null || leg.segments.Length==0) return "";
            int assigned=leg.segments.Sum(s=>s.seats?.Count(seat=>!string.IsNullOrWhiteSpace(seat)) ?? 0);
            return $"{title}: {leg.segments.Length} segment(s) · {assigned}/{leg.segments.Length*draft.PassengerDetails.SeatCount} seats";
        }
        var labels=new[]{Leg("Outbound",draft.outbound),Leg("Return",draft.inbound)}.Where(s=>s.Length>0);
        string summary=string.Join("   /   ",labels);
        return summary.Length==0 ? "" : "\n"+summary;
    }

    private RectTransform Card(string name,int row,string status,Color ink,Color fill)
    {
        var panel=style.Panel(name,content,0,row*272,1380,252);
        style.Shape("Status badge",panel,24,16,200,29,fill,2);
        Text("Trip status",panel,24,16,200,29,status,17,ink,true);
        return panel;
    }
    private static void Route(Transform panel,string text) => Text("Trip route",panel,24,54,1050,36,text,24,Ink,true,TextAlignmentOptions.Left).overflowMode=TextOverflowModes.Ellipsis;
    private static string Display(string value,string fallback)=>string.IsNullOrWhiteSpace(value)?fallback:value;
    private static string StepName(TripDraft.Step step)
    {
        switch(step)
        {
            case TripDraft.Step.Search:return "Search flights";case TripDraft.Step.Dates:return "Choose dates";
            case TripDraft.Step.DepartureFlights:return "Departure flight";case TripDraft.Step.DepartureSeats:return "Departure seats";
            case TripDraft.Step.ReturnFlights:return "Return flight";case TripDraft.Step.ReturnSeats:return "Return seats";
            case TripDraft.Step.Review:return "Trip review";default:return "Payment";
        }
    }
    private static RectTransform Rect(string name,Transform parent)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.gameObject.layer=parent.gameObject.layer;rect.SetParent(parent,false);
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
}
