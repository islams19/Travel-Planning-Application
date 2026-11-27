using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public class FrontendUsabilityTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private).Invoke(o,args);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>().First(b=>b.name==name);
    Mouse mouse;Keyboard keyboard;
    [SetUp] public void Devices() {mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();}
    [TearDown] public void Cleanup() {if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);}
    static Vector2 Point(Component target)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)target.transform;var canvas=target.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
    }
    IEnumerator Click(Selectable target)
    {
        var point=Point(target);
        var hits=new List<RaycastResult>();var e=new PointerEventData(EventSystem.current){position=point};EventSystem.current.RaycastAll(e,hits);
        Assert.IsNotEmpty(hits);Assert.AreEqual(target.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),target.name+" must receive pointer input");
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
    }
    IEnumerator Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
    }
    [UnityTest,Timeout(45000)] public IEnumerator AirportPopupSupportsRealPointerKeyboardValidationAndTab()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();
        var from=Field<DestinationAutocomplete>(nav,"originSearch");var to=Field<DestinationAutocomplete>(nav,"destinationSearch");
        var input=Field<TMP_InputField>(to,"input");var popup=Field<RectTransform>(to,"results");
        yield return Click(input);input.text="";yield return null;
        // TMP consumes character events through its text-editing event handler.
        foreach(char character in "New York")input.ProcessEvent(new Event{type=EventType.KeyDown,character=character});
        yield return null;yield return null;
        Assert.AreEqual("New York",input.text,"Typing must continue to work alongside suggestion navigation");
        Assert.GreaterOrEqual(popup.rect.width,620);
        var feedbackCorners=new Vector3[4];var popupCorners=new Vector3[4];
        Field<TMP_Text>(to,"feedback").rectTransform.GetWorldCorners(feedbackCorners);popup.GetWorldCorners(popupCorners);
        Assert.GreaterOrEqual(feedbackCorners[0].y,popupCorners[1].y,"Field feedback must not overlap the first suggestion");
        foreach(string code in new[]{"JFK","EWR","LGA"})Assert.IsTrue(popup.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Airport code"&&t.text==code));
        Assert.IsTrue(popup.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="City"&&t.text.Contains("Kennedy")));
        Assert.IsFalse(to.CanContinue);StringAssert.Contains("destination",Field<TMP_Text>(to,"feedback").text);
        yield return Press(Key.DownArrow);Assert.AreEqual(0,Field<int>(to,"highlighted"));
        yield return Press(Key.DownArrow);Assert.AreEqual(1,Field<int>(to,"highlighted"));
        yield return Press(Key.UpArrow);Assert.AreEqual(0,Field<int>(to,"highlighted"));
        yield return Press(Key.DownArrow);yield return Press(Key.Enter);
        Assert.AreEqual("JFK",to.SelectedAirportCode);Assert.IsFalse(popup.gameObject.activeSelf);Assert.AreEqual("",Field<TMP_Text>(to,"feedback").text);
        yield return Click(input);input.text="New York";yield return null;
        yield return Click(Button(popup,"Airport EWR"));Assert.AreEqual("EWR",to.SelectedAirportCode);
        yield return Click(input);input.text="EWR";yield return null;
        yield return Press(Key.Enter);Assert.AreEqual("EWR",to.SelectedAirportCode,"An exact airport code must not resolve to a different city-default airport");
        yield return Click(input);input.text="not-an-airport-xyz";yield return null;
        Assert.IsFalse(to.CanContinue);Assert.IsTrue(Field<TMP_Text>(to,"noResults").gameObject.activeInHierarchy);
        Assert.AreEqual(0,popup.GetComponentsInChildren<UIButton>().Length);
        yield return Press(Key.Enter);Assert.IsFalse(to.CanContinue,"Arbitrary text cannot become a selected airport");
        KeyboardFocusNavigation.Instance.Focus(input);input.text="Tokyo";yield return null;
        yield return Press(Key.Escape);Assert.IsFalse(popup.gameObject.activeSelf);Assert.AreEqual("Tokyo",input.text);
        KeyboardFocusNavigation.Instance.Focus(input);yield return null;
        yield return Press(Key.Tab);
        Assert.AreEqual(Button(Field<CompactFlightSearch>(nav,"compactSearch").transform,"Departure date").gameObject,EventSystem.current.currentSelectedGameObject);
        Assert.IsFalse(popup.gameObject.activeSelf);Assert.AreEqual("Tokyo",input.text);
        Field<TMP_InputField>(from,"input").text="unvalidated origin";
        Assert.IsFalse(Field<CompactFlightSearch>(nav,"compactSearch").Search());
        yield return null; // Allow TMP to activate the first invalid field before correcting another field.
        Assert.AreEqual(Field<TMP_InputField>(from,"input").gameObject,EventSystem.current.currentSelectedGameObject);
        StringAssert.Contains("departure",Field<TMP_Text>(from,"feedback").text);StringAssert.Contains("destination",Field<TMP_Text>(to,"feedback").text);
        KeyboardFocusNavigation.Instance.Focus(input);input.text="Narita";yield return null;
        yield return Press(Key.DownArrow);yield return Press(Key.UpArrow);yield return Press(Key.Enter);
        Assert.AreEqual("NRT",to.SelectedAirportCode);
        from.TrySelectAirportDestination("JFK");
        var originInput=Field<TMP_InputField>(from,"input");KeyboardFocusNavigation.Instance.Focus(originInput);
        yield return Press(Key.Tab);Assert.AreEqual(Button(Field<CompactFlightSearch>(nav,"compactSearch").transform,"Swap airports").gameObject,EventSystem.current.currentSelectedGameObject);
        yield return Press(Key.Tab);Assert.AreEqual(input.gameObject,EventSystem.current.currentSelectedGameObject);
        yield return Press(Key.LeftShift,Key.Tab);Assert.AreNotEqual(input.gameObject,EventSystem.current.currentSelectedGameObject);
        originInput.text="Unvalidated origin";Invoke(from,"ValidateAirport");StringAssert.Contains("departure",Field<TMP_Text>(from,"feedback").text);
    }

    [UnityTest,Timeout(45000)] public IEnumerator SidebarSelectionSurvivesHoverFocusAndBookingNavigation()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();var items=Field<UIButton[]>(nav,"buttons");
        foreach(int index in new[]{1,2,0})
        {
            yield return Click(items[index]);
            for(int i=0;i<items.Length;i++)Assert.AreEqual(i==index,items[i].GetComponent<SidebarItemVisual>().IsSelected);
        }
        var sidebar=items[0].transform.parent;
        Assert.IsNotNull(sidebar.Find("Voyage logo"));Assert.AreEqual("Voyage",sidebar.Find("Voyage brand").GetComponent<TMP_Text>().text);
        Assert.IsFalse(items[3].gameObject.activeInHierarchy);
        for(int i=0;i<3;i++)
        {
            Assert.IsNotNull(items[i].GetComponentInChildren<ReviewIcon>());
            if(i>0)Assert.AreEqual(0,items[i].targetGraphic.color.a,"Inactive navigation has no background or outline");
            foreach(var graphic in items[i].GetComponentsInChildren<Graphic>())if(graphic!=items[i].targetGraphic)
                Assert.AreEqual(i==0?Color.white:(Color)new Color32(153,169,192,255),graphic.color);
        }
        var selected=items[0].GetComponent<SidebarItemVisual>();var selectedColor=items[0].targetGraphic.color;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=Vector2.zero});yield return null;yield return null;
        Assert.IsTrue(selected.IsSelected);Assert.IsFalse(selected.IsHovered);Assert.AreNotEqual(selectedColor,items[0].targetGraphic.color);
        KeyboardFocusNavigation.Instance.Focus(items[1]);yield return null;
        Assert.IsTrue(items[1].GetComponentsInChildren<FocusOutline>().Any(r=>r.gameObject.activeInHierarchy));Assert.IsTrue(selected.IsSelected);
        yield return Press(Key.Space);Assert.IsTrue(items[1].GetComponent<SidebarItemVisual>().IsSelected);
        nav.SearchExploreDestination("CUN");var departure=new DateTime(2027,7,14);
        Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)departure,(DateTime?)null,true);Invoke(nav,"ShowFlightResults",departure,(DateTime?)null);yield return null;
        Assert.IsTrue(selected.IsSelected);Assert.IsFalse(items[1].GetComponent<SidebarItemVisual>().IsSelected);
    }

    [UnityTest,Timeout(60000)] public IEnumerator SeatSummaryStaysFixedAndConnectionsRetainAllTravelerAssignments()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");
        var departure=new DateTime(2027,7,14);Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)departure,(DateTime?)null,true);
        var results=Field<FlightResultsPage>(nav,"flightResults");Invoke(results,"SetBookingOptions",3,"Economy");
        foreach(string id in new[]{"B6-100","UA-107","B6-110"})
        {
            Invoke(nav,"ShowFlightResults",departure,(DateTime?)null);
            var flight=Field<List<FlightResultsPage.Flight>>(results,"flights").Single(f=>MockFlightTestData.Matches(f,id));
            var card=(RectTransform)flight.GetType().GetProperty("CardView",Private).GetValue(flight);
            Button(card,"Select").onClick.Invoke();yield return null;
            var page=Field<SeatSelectionPage>(nav,"seatPage");var scroll=Field<ScrollRect>(page,"scroll");var confirm=Button(page.transform,"Confirm Seat");
            Assert.AreEqual((Color)new Color32(219,226,236,255),confirm.targetGraphic.color,"Incomplete seats need a clearly disabled Continue button");
            var selector=Button(page.transform,"Traveler 1");var summary=page.transform.Find("Seat summary");
            Vector3 selectorPosition=selector.transform.position,confirmPosition=confirm.transform.position,summaryPosition=summary.position;
            var mapPoint=Point(scroll.viewport);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=mapPoint,scroll=new Vector2(0,-4)});yield return null;yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y,0,"Only the map needs to scroll");
            Assert.AreEqual(selectorPosition,selector.transform.position);Assert.AreEqual(confirmPosition,confirm.transform.position);Assert.AreEqual(summaryPosition,summary.position);
            page.SelectSegment(0);yield return null;
            if(flight.Segments.Count==1){Assert.IsFalse(Field<RectTransform>(page,"segmentPanel").gameObject.activeSelf);Assert.IsFalse(Field<UIButton>(page,"nextFlight").gameObject.activeSelf);}
            for(int segment=0;segment<flight.Segments.Count;segment++)
            {
                yield return Click(Button(page.transform,"Seat 1A"));
                Assert.AreEqual(1,page.CurrentBooking.SeatsForSegment(segment).Count(s=>s!=null));
                Assert.IsFalse(Button(page.transform,"Seat 1A").interactable);
                yield return Click(Button(page.transform,"Seat 1B"));yield return Click(Button(page.transform,"Seat 1D"));
                CollectionAssert.AreEqual(new[]{"1A","1B","1D"},page.CurrentBooking.SeatsForSegment(segment));
                if(flight.Segments.Count>1)StringAssert.Contains("3/3 seats selected",Button(page.transform,"Segment "+(segment+1)).GetComponentInChildren<TMP_Text>().text);
                if(segment<flight.Segments.Count-1){Assert.IsFalse(confirm.interactable);yield return Click(Button(page.transform,"Next flight seats"));}
            }
            Assert.IsTrue(confirm.interactable);Assert.IsTrue(page.CurrentBooking.HasAllSeats);
            if(flight.Segments.Count>1){yield return Click(Button(page.transform,"Segment 1"));CollectionAssert.AreEqual(new[]{"1A","1B","1D"},page.CurrentBooking.SeatsForSegment(0));}
            yield return Click(confirm);Assert.IsTrue(Field<TripReviewPage>(nav,"tripReview").gameObject.activeInHierarchy);
        }
    }
}
