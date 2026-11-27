using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

public sealed class TripReviewPage : MonoBehaviour
{
    static readonly Color Ink=new Color32(29,40,59,255), Muted=new Color32(96,113,139,255),
        Blue=new Color32(47,80,247,255), PaleBlue=new Color32(235,240,255,255), Border=new Color32(222,230,241,255),
        Done=new Color32(38,151,119,255), Pending=new Color32(166,98,20,255);
    DateSelectionPage style;
    TMP_Text total,priceRows,priceValues,travelerSummary,error,travelerStatus,continueLabel;
    RectTransform priceEdge,priceSurface,priceDivider,breakdownNote,totalDivider,grandLabel,travelerLink,continueRect;
    RectTransform legContent,drawer,drawerContent,structuredDetails;
    ScrollRect legScroll,drawerScroll;
    GameObject overlay;
    TMP_Text drawerTitle,drawerBody,drawerContext;
    UnityEngine.UI.Button changesTab,baggageTab;
    SeatSelectionPage.Booking outbound,inbound,drawerBooking;
    Func<bool> continueAction;
    LegCard departureCard,returnCard;
    TravelerDetailsForm travelerForm;
    RectTransform travelerRoot;
    public int GrandTotal { get; private set; }
    public int DisplayBaseFare { get; private set; }
    public int DisplayTaxes { get; private set; }
    public int DisplayAdditionalCharges => 0;
    sealed class LegCard
    {
        public RectTransform Panel;
        public TMP_Text Heading,Route,Cabin,Duration,Departure,Arrival,From,To,Details,Rules;
        public RectTransform Timeline;
        public UnityEngine.UI.Button FlightDetails,FareRules;
    }

    public void Build(DateSelectionPage theme,Action editRoute,Action editDates,Action editDeparture,
        Action editDepartureSeats,Action editReturn,Action editReturnSeats,Func<bool> continueToPayment)
    {
        style=theme; continueAction=continueToPayment;
        var background=gameObject.AddComponent<Image>(); background.color=new Color32(245,247,250,255); background.raycastTarget=false;
        Label("Review guidance",transform,60,119,700,34,"Review your flights and fare before continuing",23,Ink,true);
        Link("Edit route",transform,1170,115,142,46,editRoute);
        Link("Edit dates",transform,1324,115,152,46,editDates);
        var view=style.Shape("Review itinerary viewport",transform,60,176,934,700,Color.clear); view.GetComponent<Image>().raycastTarget=true;
        view.anchorMin=Vector2.zero;view.anchorMax=new Vector2(0,1);view.offsetMin=new Vector2(60,30);view.offsetMax=new Vector2(994,-176);
        legScroll=MakeScroll(view,"Legs",out legContent);
        departureCard=BuildLeg("Departure",editDeparture,editDepartureSeats);
        returnCard=BuildLeg("Return",editReturn,editReturnSeats);
        travelerRoot=NewRect("Traveler details",legContent);travelerRoot.anchorMin=Vector2.up;travelerRoot.anchorMax=Vector2.one;travelerRoot.pivot=Vector2.up;
        travelerForm=travelerRoot.gameObject.AddComponent<TravelerDetailsForm>();
        travelerForm.DetailsChanged+=()=>{if(outbound?.Passengers.HasValidDetails==true)error.text="";RefreshTravelerStatus();};
        var price=style.Panel("Trip total",transform,1010,176,466,500);priceSurface=price;priceEdge=(RectTransform)price.parent;
        Label("Title",price,26,22,410,38,"Price summary",27,Ink,true);
        travelerSummary=Label("Passengers",price,26,72,410,126,"",18,Muted,false);
        priceDivider=style.Shape("Price divider",price,26,212,410,1,Border);
        priceRows=Label("Price components",price,26,230,290,98,"Base airfare\nTaxes & fees\nAdditional charges",19,Ink,false);
        priceRows.lineSpacing=14;
        priceValues=Label("Price component values",price,306,230,130,98,"",19,Ink,false,TextAlignmentOptions.TopRight); priceValues.lineSpacing=14;
        breakdownNote=Label("Breakdown note",price,26,338,410,50,"Illustrative breakdown · same total as checkout.\nOptional checked bags have not been added.",15,Muted,false).rectTransform;
        totalDivider=style.Shape("Total divider",price,26,405,410,1,Border);
        grandLabel=Label("Grand total label",price,26,425,190,42,"Grand total",23,Ink,true).rectTransform;
        total=Label("Estimated total",price,212,421,224,48,"",30,Ink,true,TextAlignmentOptions.Right);
        total.enableAutoSizing=true;total.fontSizeMin=24;total.fontSizeMax=30;
        // One status line replaces the old "Traveler details" pill: it says what's missing and jumps to the form.
        var status=Link("Traveler details",transform,1010,694,466,44,()=>travelerForm.FocusFirst());travelerLink=(RectTransform)status.transform;
        travelerStatus=status.GetComponentInChildren<TMP_Text>();travelerStatus.alignment=TextAlignmentOptions.MidlineLeft;travelerStatus.margin=new Vector4(4,0,4,0);travelerStatus.fontStyle=FontStyles.Normal;
        var proceed=style.Control("Continue to Payment",transform,1010,748,466,54,"Continue to Payment",Blue,Color.white,22,1.6f);proceed.onClick.AddListener(Continue);
        continueRect=(RectTransform)proceed.transform;continueLabel=proceed.GetComponentInChildren<TMP_Text>();
        error=Label("Booking validation",transform,1010,814,466,58,"",17,new Color32(168,49,54,255),false);
        BuildDrawer();
    }
    void Continue()
    {
        if(!ValidateTravelerDetails()){error.text="Complete the highlighted traveler and contact fields.";return;}
        error.text=continueAction()?"":"Complete your flight and seat selections before continuing.";
    }
    // Secondary actions are quiet text links so the page has one obvious next step.
    UnityEngine.UI.Button Link(string name,Transform parent,float x,float y,float width,float height,Action action)
    {
        var b=style.Control(name,parent,x,y,width,height,name,Color.clear,Blue,18,2);b.onClick.AddListener(()=>action());
        var hover=b.gameObject.AddComponent<LinkHover>();hover.Bind(b.GetComponent<Image>());
        return b;
    }
    void RefreshTravelerStatus()
    {
        if(travelerStatus==null)return;
        bool ready=outbound?.Passengers.HasValidDetails==true;
        string status=ready?"Traveler details complete":"Traveler details  ·  Required before payment";
        if(travelerStatus.text==status)return;
        travelerStatus.text=status;travelerStatus.color=ready?Done:Pending;
        continueLabel.text=ready?"Continue to Payment":"Add traveler details";
    }
    // Details can also change from a resumed draft, so keep the status line in step every frame (cheap: text only changes on a flip).
    void Update()=>RefreshTravelerStatus();
    void LayoutPrice()
    {
        if(travelerSummary.font==null)travelerSummary.font=TMP_Settings.defaultFontAsset;
        float summary=Mathf.Max(24,travelerSummary.GetPreferredValues(travelerSummary.text,410,0).y);
        travelerSummary.rectTransform.sizeDelta=new Vector2(410,summary);
        float y=72+summary+18;
        priceDivider.anchoredPosition=new Vector2(26,-y);y+=18;
        priceRows.rectTransform.anchoredPosition=new Vector2(26,-y);priceValues.rectTransform.anchoredPosition=new Vector2(306,-y);y+=108;
        breakdownNote.anchoredPosition=new Vector2(26,-y);y+=62;
        totalDivider.anchoredPosition=new Vector2(26,-y);y+=20;
        grandLabel.anchoredPosition=new Vector2(26,-y);total.rectTransform.anchoredPosition=new Vector2(212,-(y-4));y+=72;
        priceEdge.sizeDelta=new Vector2(466,y);priceSurface.sizeDelta=new Vector2(463,y-3);
        float below=176+y+18;
        travelerLink.anchoredPosition=new Vector2(1010,-below);continueRect.anchoredPosition=new Vector2(1010,-(below+54));
        error.rectTransform.anchoredPosition=new Vector2(1010,-(below+120));
    }
    internal bool ValidateTravelerDetails()=>travelerForm!=null && travelerForm.ValidateAll();
    UnityEngine.UI.Button Control(string name,Transform parent,float x,float y,float width,float height,Action action)
    {
        var b=style.Control(name,parent,x,y,width,height,name,PaleBlue,Blue,18,2); b.onClick.AddListener(()=>action()); return b;
    }
    TMP_Text Label(string name,Transform parent,float x,float y,float width,float height,string value,float size,Color color,bool bold,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
    {
        var t=Text(name,parent,x,y,width,height,value,size,color,bold,align); t.textWrappingMode=TextWrappingModes.Normal; return t;
    }
    LegCard BuildLeg(string heading,Action editFlight,Action editSeats)
    {
        var c=new LegCard(); c.Panel=style.Panel(heading,legContent,0,0,910,440);
        c.Heading=Label("Leg title",c.Panel,26,20,500,34,heading,24,Ink,true);
        c.Cabin=Label("Cabin",c.Panel,26,110,180,30,"",19,Blue,true,TextAlignmentOptions.TopLeft);
        c.Route=Label("Cities",c.Panel,26,66,630,32,"",21,Ink,false);
        c.Duration=Label("Stops and duration",c.Panel,212,117,450,30,"",19,Muted,false,TextAlignmentOptions.Center);
        c.Departure=Label("Departure local time",c.Panel,26,192,220,38,"",28,Ink,true);
        c.Arrival=Label("Arrival local time",c.Panel,650,192,230,38,"",28,Ink,true,TextAlignmentOptions.TopRight);
        c.Departure.enableAutoSizing=c.Arrival.enableAutoSizing=true; c.Departure.fontSizeMin=c.Arrival.fontSizeMin=21; c.Departure.fontSizeMax=c.Arrival.fontSizeMax=28;
        c.From=Label("Origin airport",c.Panel,26,237,150,26,"",19,Muted,false);
        c.To=Label("Destination airport",c.Panel,730,237,150,26,"",19,Muted,false,TextAlignmentOptions.TopRight);
        c.Timeline=NewRect("Route timeline",c.Panel); c.Timeline.anchorMin=c.Timeline.anchorMax=c.Timeline.pivot=Vector2.up;
        c.Timeline.anchoredPosition=new Vector2(230,-192); c.Timeline.sizeDelta=new Vector2(420,78);
        c.Details=Label("Details",c.Panel,26,285,850,70,"",18,Muted,false);
        c.FlightDetails=Link("Flight details",c.Panel,696,78,184,44,()=>OpenFlight(c==departureCard?outbound:inbound));
        DropdownChevron.Attach(c.FlightDetails).PointRight();
        Link("Edit flight",c.Panel,576,20,144,44,editFlight); Link("Edit seats",c.Panel,736,20,144,44,editSeats);
        c.Rules=Label("Basic fare rules",c.Panel,26,386,850,72,"",17,Muted,false);
        c.FareRules=Link("Fare rules",c.Panel,696,128,184,44,()=>OpenFare(c==departureCard?outbound:inbound));
        DropdownChevron.Attach(c.FareRules).PointRight(); return c;
    }
    public void ShowBooking(SeatSelectionPage.Booking departure,SeatSelectionPage.Booking returning)
    {
        CloseDrawer(); outbound=departure; inbound=returning; error.text="";
        float first=ShowLeg(departureCard,departure,"Departure");
        returnCard.Panel.parent.gameObject.SetActive(returning!=null);
        float height=first;
        if(returning!=null) { ((RectTransform)returnCard.Panel.parent).anchoredPosition=new Vector2(0,-first-22); height+=22+ShowLeg(returnCard,returning,"Return"); }
        travelerRoot.anchoredPosition=new Vector2(0,-height-22);
        travelerForm.Bind(style,departure.Passengers,formHeight=>{legContent.sizeDelta=new Vector2(0,Mathf.Max(legScroll.viewport.rect.height,height+22+formHeight));});
        legContent.sizeDelta=new Vector2(0,Mathf.Max(legScroll.viewport.rect.height,height+22+travelerForm.Height));legScroll.verticalNormalizedPosition=1;
        // Use the same snapshot/calculation as the payment handoff; this is display-only allocation.
        GrandTotal=new PaymentBooking(departure,returning).EstimatedTotal;
        DisplayTaxes=Mathf.RoundToInt(GrandTotal*.12f);DisplayBaseFare=GrandTotal-DisplayTaxes;
        var party=departure.Passengers;
        travelerSummary.text=Travelers(departure.PassengerCount)+" · "+departure.Cabin+"\n"+
            string.Join("\n",Enum.GetValues(typeof(PassengerParty.Category)).Cast<PassengerParty.Category>().Where(c=>party.CountFor(c)>0).Select(c=>
                $"{party.CountFor(c)} {PassengerParty.Plural(c,party.CountFor(c))} × ${party.FareFor(departure.FarePerTraveler,c)+(returning==null?0:party.FareFor(returning.FarePerTraveler,c)):N0}"));
        priceValues.text=$"${DisplayBaseFare:N0}\n${DisplayTaxes:N0}\n$0";total.text=$"${GrandTotal:N0} USD";
        LayoutPrice();RefreshTravelerStatus();
    }
    float ShowLeg(LegCard c,SeatSelectionPage.Booking booking,string direction)
    {
        var flight=booking.Flight;var segments=flight.Segments;string from=segments.First().OriginCode,to=segments.Last().DestinationCode;
        c.Heading.text=direction+" · "+booking.DepartureDate.ToString("ddd, MMM d",CultureInfo.InvariantCulture);
        c.Route.text=City(from)+" ("+from+") → "+City(to)+" ("+to+")";c.Cabin.text=booking.Cabin;
        c.Departure.text=flight.Departure;c.Arrival.text=flight.Arrival.Replace(" days)","d)").Replace(" day)","d)");c.From.text=from;c.To.text=to;
        c.Duration.text=(flight.Stops==0?"Nonstop":$"{flight.Stops} stop{(flight.Stops==1?"":"s")}")+" · "+FlightResultsPage.DurationLabel(flight.DurationMinutes)+" total";
        foreach(Transform child in c.Timeline) { child.gameObject.SetActive(false);Destroy(child.gameObject); }
        style.Shape("Route line",c.Timeline,8,20,404,2,Border);
        for(int i=0;i<=segments.Count;i++) style.Shape("Route point "+i,c.Timeline,4+i*404f/segments.Count,16,10,10,Blue,1);
        for(int i=0;i<flight.Layovers.Count;i++)
        {
            var stop=flight.Layovers[i];float x=(i+1)*404f/segments.Count-64;
            Label("Connection "+i,c.Timeline,x,36,144,42,stop.AirportCode+"\n"+FlightResultsPage.DurationLabel(stop.DurationMinutes)+" layover",15,Muted,false,TextAlignmentOptions.Center);
        }
        c.Details.text=flight.CarrierLabel+$" · ${booking.FarePerTraveler:N0} per adult · Leg total ${booking.EstimatedTotal:N0}\n"+string.Join("\n",segments.Select((s,i)=>s.OriginCode+" → "+s.DestinationCode+" · "+s.Airline+" "+s.FlightNumber+" · "+string.Join(" · ",booking.SeatsForSegment(i).Select((seat,t)=>"Seat "+seat))));
        if(c.Details.font==null)c.Details.font=TMP_Settings.defaultFontAsset;
        float body=Mathf.Max(58,c.Details.GetPreferredValues(c.Details.text,850,0).y+10);
        c.Details.rectTransform.sizeDelta=new Vector2(850,body);
        float footer=285+body+22;
        c.Rules.rectTransform.anchoredPosition=new Vector2(26,-footer);
        var rules=ReviewFareRules.For(booking.Cabin);
        c.Rules.text=rules.RefundLabel+" · "+rules.ChangeLabel+"\n"+flight.Baggage.Summary+"\nIllustrative fare rules; not airline policies.";
        float height=footer+90;
        ((RectTransform)c.Panel.parent).sizeDelta=new Vector2(910,height);c.Panel.sizeDelta=new Vector2(907,height-3);
        return height;
    }
    void BuildDrawer()
    {
        var rect=NewRect("Review detail overlay",transform);rect.anchorMax=Vector2.one;overlay=rect.gameObject;
        var dim=overlay.AddComponent<Image>();dim.color=new Color(.04f,.08f,.15f,.45f);dim.raycastTarget=true;
        drawer=style.Panel("Review side panel",rect,828,16,690,898);
        var edge=(RectTransform)drawer.parent;edge.anchorMin=new Vector2(1,0);edge.anchorMax=Vector2.one;edge.pivot=new Vector2(1,1);edge.offsetMin=new Vector2(-708,16);edge.offsetMax=new Vector2(-18,-16);
        drawer.anchorMin=Vector2.zero;drawer.anchorMax=Vector2.one;drawer.offsetMin=Vector2.one*1.5f;drawer.offsetMax=Vector2.one*-1.5f;
        drawerTitle=Label("Panel title",drawer,30,24,550,44,"",30,Ink,true);
        Control("Close review details",drawer,610,24,48,44,CloseDrawer).GetComponentInChildren<TMP_Text>().text="×";
        drawerContext=Label("Panel context",drawer,30,86,620,80,"",20,Ink,false);
        changesTab=Control("Changes & cancellations",drawer,30,180,370,44,()=>ShowFareTab(false));
        baggageTab=Control("Baggage",drawer,416,180,242,44,()=>ShowFareTab(true));
        var view=style.Shape("Panel scroll",drawer,30,244,628,560,Color.clear);view.GetComponent<Image>().raycastTarget=true;
        view.anchorMin=Vector2.zero;view.anchorMax=Vector2.one;view.offsetMin=new Vector2(30,92);view.offsetMax=new Vector2(-30,-244);
        drawerScroll=MakeScroll(view,"Panel content",out drawerContent);
        drawerBody=Label("Panel text",drawerContent,0,0,594,560,"",20,Ink,false);
        Control("Close panel",drawer,30,0,628,48,CloseDrawer);
        var close=(RectTransform)drawer.Find("Close panel");close.anchorMin=close.anchorMax=new Vector2(0,0);close.pivot=Vector2.zero;close.anchoredPosition=new Vector2(30,24);
        overlay.AddComponent<KeyboardFocusScope>();
        overlay.SetActive(false);
    }
    void Open(SeatSelectionPage.Booking booking,bool fare)
    {
        if(booking==null)return;drawerBooking=booking;overlay.SetActive(true);overlay.transform.SetAsLastSibling();
        drawerTitle.text=fare?"Fare rules":"Flight details";
        drawerContext.text=City(booking.Flight.Segments.First().OriginCode)+" ("+booking.Flight.Segments.First().OriginCode+") → "+City(booking.Flight.Segments.Last().DestinationCode)+" ("+booking.Flight.Segments.Last().DestinationCode+")\n"+
            booking.DepartureDate.ToString("ddd, MMM d, yyyy",CultureInfo.InvariantCulture)+" · "+FlightResultsPage.DurationLabel(booking.Flight.DurationMinutes)+" total · "+booking.Cabin;
        changesTab.gameObject.SetActive(fare);baggageTab.gameObject.SetActive(fare);
        drawerScroll.GetComponent<RectTransform>().offsetMax=new Vector2(-30,fare?-244:-186);
    }
    void OpenFlight(SeatSelectionPage.Booking booking)
    {
        Open(booking,false); if(booking==null)return;
        drawerBody.gameObject.SetActive(false);
        if(structuredDetails!=null){structuredDetails.gameObject.SetActive(false);Destroy(structuredDetails.gameObject);}
        structuredDetails=NewRect("Structured flight details",drawerContent);structuredDetails.anchorMin=Vector2.up;structuredDetails.anchorMax=Vector2.one;structuredDetails.pivot=Vector2.up;
        float height=ItineraryDetailsView.Build(style,structuredDetails,594,booking.Flight,booking.Cabin,booking.FarePerTraveler,booking.PassengerCount,booking.SegmentSeats,booking.Passengers);
        drawerContent.sizeDelta=new Vector2(0,Mathf.Max(drawerScroll.viewport.rect.height,height));drawerScroll.verticalNormalizedPosition=1;
    }

    void OpenFare(SeatSelectionPage.Booking booking) { Open(booking,true); if(booking!=null)ShowFareTab(false); }
    void ShowFareTab(bool baggage)
    {
        var rule=ReviewFareRules.For(drawerBooking.Cabin);
        changesTab.GetComponent<Image>().color=baggage?Color.white:PaleBlue;baggageTab.GetComponent<Image>().color=baggage?PaleBlue:Color.white;
        SetDrawerText(baggage ? "<b>Baggage allowance</b>\n\n"+drawerBooking.Flight.Baggage.Summary+"\n\n"+FlightResultsPage.BaggageAllowance.PolicyNote+
            "\n\nOptional checked bags are not purchased on this page and are excluded from your total.\n\nDemo fare rules; not actual airline policies." :
            "<b>Changes</b>\n\n"+rule.ChangeDescription+"\n\n<b>Cancellations</b>\n\n"+rule.RefundDescription+
            "\n\n<b>24-hour cancellation</b>\n\nCancel within 24 hours of booking for a full refund under this illustrative policy. No refund processing is implemented.\n\n"+
            "These are generalized demo fare rules for "+drawerBooking.Cabin+", not airline policies. These rules apply to the travelers and shared cabin in this booking.");
    }
    void SetDrawerText(string value)
    {
        if(structuredDetails!=null){structuredDetails.gameObject.SetActive(false);Destroy(structuredDetails.gameObject);structuredDetails=null;}
        drawerBody.gameObject.SetActive(true);
        drawerBody.text=value;if(drawerBody.font==null)drawerBody.font=TMP_Settings.defaultFontAsset;
        float height=drawerBody.GetPreferredValues(value,594,0).y+24;
        drawerBody.rectTransform.sizeDelta=new Vector2(594,height);drawerContent.sizeDelta=new Vector2(0,Mathf.Max(drawerScroll.viewport.rect.height,height));drawerScroll.verticalNormalizedPosition=1;
    }
    void CloseDrawer() { if(overlay!=null)overlay.SetActive(false); }
    void OnDisable()=>CloseDrawer();
    static string City(string code)=>TravelApp.FlightMap.TravelLocationCatalog.Find(code)?.city??code;
    static string Airport(string code)=>TravelApp.FlightMap.TravelLocationCatalog.Find(code)?.airport??code;
    static string Travelers(int count)=>count==1?"1 traveler":$"{count} travelers";
    ScrollRect MakeScroll(RectTransform root,string contentName,out RectTransform content)
    {
        var clip=NewRect("Viewport",root);clip.anchorMax=Vector2.one;clip.offsetMax=new Vector2(-24,0);clip.gameObject.AddComponent<RectMask2D>();
        content=NewRect(contentName,clip);content.anchorMin=Vector2.up;content.anchorMax=Vector2.one;content.pivot=Vector2.up;
        var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=clip;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=50;
        var track=NewRect("Scrollbar",root);track.anchorMin=Vector2.right;track.anchorMax=Vector2.one;track.offsetMin=new Vector2(-12,0);track.offsetMax=new Vector2(-2,0);track.gameObject.AddComponent<Image>().color=Border;
        var handle=NewRect("Handle",track);handle.anchorMax=Vector2.one;var image=handle.gameObject.AddComponent<Image>();image.color=Muted;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=image;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;return scroll;
    }
    static RectTransform NewRect(string name,Transform parent)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.gameObject.layer=parent.gameObject.layer;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.sizeDelta=Vector2.zero;return rect;
    }
}

// Link-style buttons get a pale pill only while hovered, so they read as text until pointed at.
public sealed class LinkHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    Image surface;
    public void Bind(Image image) {surface=image;}
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _) {if(surface!=null)surface.color=new Color32(235,240,255,255);}
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _) {if(surface!=null)surface.color=Color.clear;}
    void OnDisable() {if(surface!=null)surface.color=Color.clear;}
}
