using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public class FrontendRefinementTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Private).GetValue(o);
    static void Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Private).Invoke(o,a);
    static UIButton Button(Transform root,string n)=>root.GetComponentsInChildren<UIButton>().First(b=>b.name==n);
    static RectTransform Card(FlightResultsPage.Flight f)=>(RectTransform)f.GetType().GetProperty("CardView",Private).GetValue(f);
    static List<FlightResultsPage.Flight> Visible(FlightResultsPage p)=>Field<List<FlightResultsPage.Flight>>(p,"flights").Where(f=>Card(f).gameObject.activeSelf).ToList();
    static void Pointer(Selectable target)
    {
        Canvas.ForceUpdateCanvases();var r=(RectTransform)target.transform;
        var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        Assert.IsNotEmpty(hits);
        Assert.AreEqual(target.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),"The visible option must win the actual UI raycast");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    [UnityTest,Timeout(45000)] public IEnumerator FilterPopupsMultiAirlineLongHaulChipsAndEmptyResults()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.SearchExploreDestination("SYD");
        var calendar=Field<DateSelectionPage>(nav,"datePage");var page=Field<FlightResultsPage>(nav,"flightResults");
        var departure=new DateTime(2027,6,14);
        Invoke(calendar,"RestoreDates",(DateTime?)departure,(DateTime?)departure.AddDays(10),false);
        Invoke(nav,"ShowFlightResults",departure,(DateTime?)departure.AddDays(10));yield return null;
        var all=Field<List<FlightResultsPage.Flight>>(page,"flights");Assert.AreEqual(12,Visible(page).Count);
        Assert.Greater(Field<Slider>(page,"durationSlider").maxValue,15*60,"Duration limits must update as soon as a long-haul route opens, before touching a filter");
        var progress=page.GetComponentInChildren<BookingProgress>();Assert.AreEqual(TripDraft.Step.DepartureFlights,progress.Step);
        Assert.IsTrue(progress.RoundTrip);Assert.IsFalse(progress.GetComponentsInChildren<UIButton>().Any());
        Assert.AreEqual(7,Object.FindObjectsByType<BookingProgress>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        Pointer(Button(page.transform,"Filters"));yield return null;
        Pointer(Button(page.transform,"Stops 1 stop or fewer"));
        string firstCode=all.Select(f=>f.Code).Distinct().First(),secondCode=all.Select(f=>f.Code).Distinct().Skip(1).First();
        string firstName=all.First(f=>f.Code==firstCode).Airline;
        var airline=page.GetComponentsInChildren<Toggle>().First(t=>t.name=="Airline "+firstCode);Pointer(airline);
        Pointer(page.GetComponentsInChildren<Toggle>().First(t=>t.name=="Airline "+secondCode));
        CollectionAssert.AreEquivalent(all.Where(f=>f.Stops<=1&&(f.Code==firstCode||f.Code==secondCode)),Visible(page));
        Pointer(Button(page.transform,"Departure Morning · 6–12"));
        CollectionAssert.AreEquivalent(all.Where(f=>f.Stops<=1&&(f.Code==firstCode||f.Code==secondCode)&&f.Segments[0].DepartureLocal.Hour>=6&&f.Segments[0].DepartureLocal.Hour<12),Visible(page));
        var duration=Field<Slider>(page,"durationSlider");Assert.Greater(duration.maxValue,15*60);
        duration.value=26*60;var price=Field<Slider>(page,"priceSlider");price.value=400;
        CollectionAssert.AreEquivalent(all.Where(f=>f.Stops<=1&&(f.Code==firstCode||f.Code==secondCode)&&f.Segments[0].DepartureLocal.Hour>=6&&f.Segments[0].DepartureLocal.Hour<12&&f.DurationMinutes<=1560&&f.Price<=400),Visible(page));
        Pointer(Button(page.transform,"Done"));yield return null;
        Pointer(Button(page.transform,"Remove "+firstName));
        Assert.IsFalse(Field<HashSet<string>>(page,"selectedAirlines").Contains(firstCode));Assert.IsTrue(Field<HashSet<string>>(page,"selectedAirlines").Contains(secondCode));
        Pointer(Button(page.transform,"Filters"));yield return null;Pointer(Button(page.transform,"Clear Filters"));Assert.AreEqual(12,Visible(page).Count);
        price.value=100;Assert.AreEqual(0,page.VisibleResultCount);Pointer(Button(page.transform,"Done"));
        Assert.IsTrue(Field<TMP_Text>(page,"emptyResults").gameObject.activeInHierarchy);
        Pointer(Button(page.transform,"Clear empty filters"));Assert.AreEqual(12,page.VisibleResultCount);
        Pointer(Button(page.transform,"Filters"));yield return null;
        Pointer(Button(page.transform,"Stops 1 stop or fewer"));Pointer(Button(page.transform,"Departure Morning · 6–12"));
        price.value=400;duration.value=25*60;Pointer(Button(page.transform,"Done"));yield return null;
        Pointer(Button(page.transform,"Remove 1 stop or fewer"));yield return null;
        Assert.AreEqual(0,Field<int>(page,"stopsFilter"));Assert.AreEqual(1,Field<int>(page,"timeFilter"));
        Pointer(Button(page.transform,"Remove Morning · 6–12"));yield return null;Assert.AreEqual(0,Field<int>(page,"timeFilter"));
        Assert.AreEqual(400,Field<int>(page,"maximumPrice"));
        Pointer(Button(page.transform,"Remove Up to $400"));yield return null;Assert.AreEqual(int.MaxValue,Field<int>(page,"maximumPrice"));
        Assert.AreEqual(1500,Field<int>(page,"durationFilter"));
        Pointer(Button(page.transform,"Remove Up to 25h 00m"));yield return null;Assert.AreEqual(12,page.VisibleResultCount);
        Pointer(Button(page.transform,"Sort"));yield return null;Pointer(Button(page.transform,"Shortest"));
        CollectionAssert.AreEqual(all.OrderBy(f=>f.DurationMinutes),Visible(page).OrderBy(f=>Card(f).GetSiblingIndex()));
        Invoke(nav,"ShowPage",0);yield return null;
        Assert.AreEqual(TripDraft.Step.Search,Field<GameObject[]>(nav,"pages")[0].GetComponentInChildren<BookingProgress>().Step);
    }
    static BookingProgress Progress(Transform root)=>root.GetComponentInChildren<BookingProgress>();
    static void ChooseFlight(FlightResultsPage page)
    {
        var flight=Field<List<FlightResultsPage.Flight>>(page,"flights").First();
        Button(Card(flight),"Select").onClick.Invoke();
    }
    static void ChooseSeats(SeatSelectionPage page)
    {
        for(int i=0;i<page.CurrentBooking.Flight.Segments.Count;i++)
        {
            page.SelectSegment(i);
            page.GetComponentsInChildren<UIButton>().First(b=>b.name.StartsWith("Seat ")&&b.interactable).onClick.Invoke();
        }
        Button(page.transform,"Confirm Seat").onClick.Invoke();
    }
    [UnityTest,Timeout(45000)] public IEnumerator ProgressFollowsBothLegsAndSafeBackwardEdits()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");
        var dates=Field<DateSelectionPage>(nav,"datePage");var results=Field<FlightResultsPage>(nav,"flightResults");
        foreach(bool round in new[]{false,true})
        {
            var departure=new DateTime(2027,7,14);DateTime? returning=round?departure.AddDays(7):(DateTime?)null;
            Invoke(dates,"RestoreDates",(DateTime?)departure,returning,!round);Invoke(nav,"ShowFlightResults",departure,returning);
            yield return null;Assert.AreEqual(TripDraft.Step.DepartureFlights,Progress(results.transform).Step);
            ChooseFlight(results);yield return null;
            var seats=Field<SeatSelectionPage>(nav,"seatPage");Assert.AreEqual(TripDraft.Step.DepartureSeats,Progress(seats.transform).Step);
            ChooseSeats(seats);yield return null;
            if(round)
            {
                var returningFlights=Field<FlightResultsPage>(nav,"returnResults");
                Assert.AreEqual(TripDraft.Step.ReturnFlights,Progress(returningFlights.transform).Step);
                StringAssert.Contains("Return",string.Join(" ",Progress(returningFlights.transform).GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
                ChooseFlight(returningFlights);yield return null;
                var returnSeats=Field<SeatSelectionPage>(nav,"returnSeats");
                Assert.AreEqual(TripDraft.Step.ReturnSeats,Progress(returnSeats.transform).Step);
                ChooseSeats(returnSeats);yield return null;
            }
            var review=Field<TripReviewPage>(nav,"tripReview");Assert.AreEqual(TripDraft.Step.Review,Progress(review.transform).Step);
            Assert.IsFalse(review.GetComponentsInChildren<UIButton>().Any(b=>b.name.StartsWith("Progress ")));
            Invoke(nav,"ReturnToFlightResults");yield return null;
            Assert.AreEqual(TripDraft.Step.DepartureFlights,Progress(results.transform).Step);
            Assert.AreEqual(BookingProgress.State.Upcoming,BookingProgress.StateFor(Progress(results.transform).Step,round,3));
            Invoke(nav,"ShowTripReview");PassengerTestData.Complete(nav);Assert.IsTrue((bool)nav.GetType().GetMethod("ContinueToPayment",Private).Invoke(nav,null));yield return null;
            var payment=Field<PaymentPlaceholderPage>(nav,"paymentPage");Assert.AreEqual(TripDraft.Step.Payment,Progress(payment.transform).Step);
            Assert.AreEqual(review.GrandTotal,payment.Booking.EstimatedTotal);
        }
    }

}
