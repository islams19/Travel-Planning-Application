using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using static DateSelectionPage;

// A view over the existing route, calendar, and results controllers. No parallel booking values.
public sealed class CompactFlightSearch : MonoBehaviour
{
    const string DatesRequired="Choose the required travel dates.", DatesPassed="Those dates have passed. Choose new travel dates.";
    static readonly Color Ink=new Color32(29,40,59,255), Muted=new Color32(96,113,139,255),
        Blue=new Color32(47,80,247,255), Pale=new Color32(235,240,255,255);
    DateSelectionPage calendar;
    FlightResultsPage options;
    DestinationAutocomplete origin,destination;
    Func<bool> validRoute;
    Action submit;
    RectTransform surface;
    UnityEngine.UI.Button tripType,travelers,cabin,departure,returning,swap,search;
    TMP_Text returnHeading,message;
    GameObject openMenu;
    KeyboardFocusOrder focusOrder;
    public bool IsCalendarOpen { get; private set; }
    internal Transform SearchSurface => surface;

    public void Build(DateSelectionPage style,FlightResultsPage results,DestinationAutocomplete from,DestinationAutocomplete to,
        Func<bool> routeIsValid,Action searchFlights)
    {
        focusOrder=gameObject.AddComponent<KeyboardFocusOrder>();
        calendar=style; options=results; origin=from; destination=to; validRoute=routeIsValid; submit=searchFlights;
        surface=style.Panel("Trip search",transform,40,84,1456,200);
        tripType=style.Control("Search trip type",surface,20,20,184,40,"",Pale,Blue,19,2);
        travelers=style.Control("Search travelers",surface,224,20,178,40,"",Pale,Blue,19,2);
        cabin=style.Control("Search cabin",surface,424,20,244,40,"",Pale,Blue,19,2);
        Label("From heading",20,"From"); Label("To heading",410,"To");
        PlaceField(origin,20); PlaceField(destination,410);
        origin.ConfigureCompact(surface.parent,"From — city or airport");
        destination.ConfigureCompact(surface.parent,"To — city or airport");
        swap=style.Control("Swap airports",surface,354,110,44,44,"↔",Pale,Blue,25,1);
        swap.onClick.AddListener(Swap);
        Label("Departure heading",754,"Departure"); returnHeading=Label("Return heading",968,"Return");
        departure=style.Control("Departure date",surface,754,100,198,64,"",Pale,Ink,20,2);
        returning=style.Control("Return date",surface,968,100,198,64,"",Pale,Ink,20,2);
        departure.onClick.AddListener(()=>OpenCalendar(false)); returning.onClick.AddListener(()=>OpenCalendar(true));
        // Date pickers look like the From/To fields beside them: white, outlined, value aligned left.
        foreach(var field in new[]{departure,returning})
        {
            field.targetGraphic.color=Color.white;
            var edge=field.gameObject.AddComponent<Outline>(); edge.effectColor=new Color32(220,228,240,255); edge.effectDistance=new Vector2(1,-1);
            var text=field.GetComponentInChildren<TMP_Text>(); text.alignment=TextAlignmentOptions.MidlineLeft; text.margin=new Vector4(18,0,12,0); text.fontStyle=FontStyles.Normal;
        }
        search=style.Control("Search Flights",surface,1182,100,248,64,"Search Flights",Blue,Color.white,23,1.4f);
        search.onClick.AddListener(()=>Search());
        message=Text("Search validation",surface,754,173,676,24,"",16,new Color32(168,49,54,255),false,TextAlignmentOptions.Left);
        BuildMenus(style);
        focusOrder.Configure(tripType,travelers,cabin,origin.GetComponentInChildren<TMP_InputField>(true),swap,destination.GetComponentInChildren<TMP_InputField>(true),departure,returning,search);
        var dateRect=(RectTransform)calendar.transform; dateRect.SetParent(transform,false);
        dateRect.anchorMin=Vector2.zero; dateRect.anchorMax=Vector2.one; dateRect.offsetMin=dateRect.offsetMax=Vector2.zero;
        var background=calendar.GetComponent<Image>(); background.color=new Color(.04f,.08f,.15f,.38f); background.raycastTarget=true;
        style.Control("Close dates",calendar.transform,968,778,176,56,"Close",Pale,Blue,20,2).onClick.AddListener(CloseCalendar);
        calendar.transform.Find("Trip summary/Surface/Confirm Dates").GetComponentInChildren<TMP_Text>().text="Done";
        calendar.gameObject.AddComponent<KeyboardFocusScope>();
        calendar.gameObject.SetActive(false);
        calendar.ConfigureDateEstimates(date=>
        {
            if(!validRoute()) return null;
            var estimate=DateFareEstimates.EconomyFare(date,origin.SelectedAirportCode,destination.SelectedAirportCode);
            return estimate.HasValue ? options.AdjustFare(estimate.Value) : (int?)null;
        });
        var popular=transform.Find("popular_destinations") as RectTransform;
        if(popular!=null) popular.anchoredPosition=new Vector2(popular.anchoredPosition.x,-336);
        var cards=transform.Find("Scroll View") as RectTransform;
        if(cards!=null) { cards.anchorMin=Vector2.zero; cards.anchorMax=Vector2.one; cards.offsetMin=new Vector2(20,30); cards.offsetMax=new Vector2(0,-374); }
        origin.ContinuationChanged+=RouteChanged; destination.ContinuationChanged+=RouteChanged;
        origin.SuggestionsOpened+=CloseMenu; destination.SuggestionsOpened+=CloseMenu;
        options.BookingOptionsChanged+=Refresh; calendar.DatesChanged+=Refresh;
        Refresh();
    }

    PassengerSelector passengerControl;
    TMP_Text Label(string name,float x,string value)=>Text(name,surface,x,76,324,22,value,16,Muted,true,TextAlignmentOptions.Left);
    void PlaceField(DestinationAutocomplete field,float x)
    {
        var rect=(RectTransform)field.transform; rect.SetParent(surface,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up; rect.anchoredPosition=new Vector2(x,-100); rect.sizeDelta=new Vector2(324,64);
        var image=field.GetComponent<Image>(); var reference=calendar.Shape("Field style",surface,0,0,1,1,Color.white);
        image.sprite=reference.GetComponent<Image>().sprite; image.type=Image.Type.Sliced; image.pixelsPerUnitMultiplier=2; image.color=Color.white;
        reference.gameObject.SetActive(false); Destroy(reference.gameObject);
        var edge=field.GetComponent<Outline>() ?? field.gameObject.AddComponent<Outline>();
        edge.effectColor=new Color32(220,228,240,255); edge.effectDistance=new Vector2(1,-1);
    }

    void BuildMenus(DateSelectionPage style)
    {
        var type=Menu(style,"Trip type choices",20,68,240,124);
        Choice(style,type,"Round trip",16,()=>calendar.SetOneWay(false)); Choice(style,type,"One way",68,()=>calendar.SetOneWay(true));
        Toggle(tripType,type);
        var cabins=Menu(style,"Cabin choices",424,68,300,228);
        for(int i=0;i<FlightResultsPage.CabinChoices.Count;i++)
        { string value=FlightResultsPage.CabinChoices[i]; Choice(style,cabins,"Cabin "+value,16+i*52,()=>options.SetBookingOptions(options.PassengerCount,value),value); }
        Toggle(cabin,cabins);
        var passengers=Menu(style,"Traveler choices",224,68,448,474);
        passengerControl=passengers.gameObject.AddComponent<PassengerSelector>();
        passengerControl.Build(style,passengers,options,CloseMenu);
        Toggle(travelers,passengers);
    }
    RectTransform Menu(DateSelectionPage style,string name,float x,float y,float width,float height)
    {
        var panel=style.Panel(name,surface,x,y,width,height); panel.GetComponent<Image>().raycastTarget=true;
        panel.parent.gameObject.SetActive(false); return panel;
    }
    void Choice(DateSelectionPage style,RectTransform menu,string name,float y,Action action,string caption=null)
    {
        var button=style.Control(name,menu,14,y,menu.parent.GetComponent<RectTransform>().sizeDelta.x-28,42,caption ?? name,Pale,Blue,19,2);
        button.onClick.AddListener(()=>{ action(); CloseMenu(); });
    }
    void Toggle(UnityEngine.UI.Button button,RectTransform panel)
    {
        focusOrder.RegisterPopup(button,panel.parent);
        var chevron=DropdownChevron.Attach(button);
        button.onClick.AddListener(()=>
        {
            bool show=openMenu!=panel.parent.gameObject; CloseMenu(); origin.HideSuggestions(); destination.HideSuggestions();
            if(show) { openMenu=panel.parent.gameObject; surface.parent.SetAsLastSibling(); panel.parent.SetAsLastSibling(); openMenu.SetActive(true); chevron.SetExpanded(true); }
        });
    }
    void CloseMenu() { foreach(var control in new[]{tripType,travelers,cabin}) if(control!=null) control.GetComponent<DropdownChevron>()?.SetExpanded(false); if(openMenu!=null) {focusOrder.ClosingPopup(openMenu.transform);openMenu.SetActive(false);} openMenu=null; }
    void RouteChanged(bool _) { CloseMenu(); if(message!=null) message.text=""; Refresh(); }
    internal void Refresh()
    {
        if(options==null || tripType==null) return;
        if(IsCalendarOpen) calendar.RefreshDateEstimates();
        tripType.GetComponentInChildren<TMP_Text>().text=(calendar.IsOneWay?"One way":"Round trip");
        travelers.GetComponentInChildren<TMP_Text>().text=options.PassengerCount+(options.PassengerCount==1?" traveler":" travelers");
        cabin.GetComponentInChildren<TMP_Text>().text=options.CabinClass;
        departure.GetComponentInChildren<TMP_Text>().text=calendar.Departure?.ToString("ddd, MMM d",CultureInfo.InvariantCulture) ?? "Add date";
        returning.GetComponentInChildren<TMP_Text>().text=calendar.Return?.ToString("ddd, MMM d",CultureInfo.InvariantCulture) ?? "Add date";
        departure.GetComponentInChildren<TMP_Text>().color=calendar.Departure.HasValue ? Ink : Muted;
        returning.GetComponentInChildren<TMP_Text>().color=calendar.Return.HasValue ? Ink : Muted;
        returning.gameObject.SetActive(!calendar.IsOneWay); returnHeading.gameObject.SetActive(!calendar.IsOneWay);
        ((RectTransform)departure.transform).sizeDelta=new Vector2(calendar.IsOneWay?414:198,64);
        var label=departure.GetComponentInChildren<TMP_Text>().rectTransform;
        label.anchorMin=Vector2.zero; label.anchorMax=Vector2.one; label.offsetMin=label.offsetMax=Vector2.zero;
        swap.interactable=origin.CanContinue && destination.CanContinue; search.interactable=validRoute();
        search.targetGraphic.color=search.interactable ? Blue : new Color32(227,232,241,255);
        search.GetComponentInChildren<TMP_Text>().color=search.interactable ? Color.white : Muted;
        swap.GetComponentInChildren<TMP_Text>().color=swap.interactable ? Blue : Muted;
        if(origin.CanContinue && destination.CanContinue && !validRoute()) message.text="Choose different origin and destination cities.";
        if((message.text==DatesRequired || message.text==DatesPassed) && DatesReady) message.text="";
        passengerControl?.Refresh();
    }
    public void Swap()
    {
        if(!origin.CanContinue || !destination.CanContinue) return;
        string from=origin.SelectedDestination,fromCode=origin.SelectedAirportCode;
        string to=destination.SelectedDestination,toCode=destination.SelectedAirportCode;
        origin.RestoreSelection(to,to,toCode); destination.RestoreSelection(from,from,fromCode);
        origin.HideSuggestions(); destination.HideSuggestions(); Refresh();
    }
    public void OpenCalendar(bool returnField=false)
    {
        CloseMenu(); origin.HideSuggestions(); destination.HideSuggestions();
        calendar.SetDestination(destination.SelectedDestination ?? "Choose destination"); calendar.BeginDateEdit(returnField);
        IsCalendarOpen=true; calendar.gameObject.SetActive(true); calendar.transform.SetAsLastSibling();
    }
    public void CloseCalendar() { IsCalendarOpen=false; if(calendar!=null) calendar.gameObject.SetActive(false); Refresh(); }
    public bool Search()
    {
        bool fromValid=origin.ValidateAirport(),toValid=destination.ValidateAirport();
        if(!validRoute())
        {
            message.text="Select different origin and destination cities from the suggestions.";
            if(!fromValid)origin.FocusForCorrection();else {if(toValid)destination.ShowValidationError("Choose a different destination city.");destination.FocusForCorrection();}
            return false;
        }
        if(calendar.Departure.HasValue && calendar.Departure.Value.Date<DateSelectionPage.Today().Date)
        { message.text=DatesPassed; OpenCalendar(false); return false; }
        if(!DatesReady)
        { message.text=DatesRequired; OpenCalendar(calendar.Departure.HasValue); return false; }
        if(options.PassengerCount<1 || options.PassengerCount>6) { message.text="Choose 1–6 travelers."; return false; }
        CloseMenu(); CloseCalendar(); submit(); return true;
    }
    bool DatesReady => calendar.Departure.HasValue && calendar.Departure.Value.Date>=DateSelectionPage.Today().Date &&
        (calendar.IsOneWay || calendar.Return.HasValue && calendar.Return>calendar.Departure);
    void LateUpdate()
    {
        if(origin==null) return;
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMenu(); origin.HideSuggestions(); destination.HideSuggestions();
            if(IsCalendarOpen) CloseCalendar();
        }
        if(Mouse.current==null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var canvas=GetComponentInParent<Canvas>();
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        DismissPopupsAt(Mouse.current.position.ReadValue(),camera);
    }
    internal void DismissPopupsAt(Vector2 point,Camera camera)
    {
        bool onTrigger=new[]{tripType,travelers,cabin}.Any(button=>RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform,point,camera));
        if(openMenu!=null && !onTrigger && !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)openMenu.transform,point,camera)) CloseMenu();
        origin.DismissSuggestionsOutside(point,camera); destination.DismissSuggestionsOutside(point,camera);
    }

    void OnEnable() { Refresh(); }
    void OnDisable() { CloseMenu(); if(calendar!=null) {IsCalendarOpen=false; calendar.gameObject.SetActive(false);} }
    void OnDestroy()
    {
        if(origin!=null) {origin.ContinuationChanged-=RouteChanged; origin.SuggestionsOpened-=CloseMenu;}
        if(destination!=null) {destination.ContinuationChanged-=RouteChanged; destination.SuggestionsOpened-=CloseMenu;}
        if(options!=null) options.BookingOptionsChanged-=Refresh;
        if(calendar!=null) calendar.DatesChanged-=Refresh;
    }
}
