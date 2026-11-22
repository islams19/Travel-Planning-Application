using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UIButton=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

public class TripReviewPresentationTests
{
    sealed class MemoryStore:ISaveStore { public SaveData Load()=>new SaveData();public void Save(SaveData data){} }
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Private).GetValue(o);
    static void Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Private).Invoke(o,args);
    static UIButton Button(Transform t,string n)=>t.GetComponentsInChildren<UIButton>(true).First(b=>b.name==n&&b.gameObject.activeInHierarchy);
    static string Text(Transform t)=>string.Join("\n",t.GetComponentsInChildren<TMP_Text>().Select(x=>x.text));
    static void Click(Transform t,string n)=>Button(t,n).onClick.Invoke();
    static void PointerClick(UIButton button)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsTrue(hits.Count>0);Assert.AreEqual(button.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),"Visible drawer controls must receive pointer input");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static void Select(FlightResultsPage p,string id)
    {
        var f=Field<List<FlightResultsPage.Flight>>(p,"flights").Single(x=>x.Id==id);
        Click((Transform)f.GetType().GetProperty("CardView",Private).GetValue(f),"Select");
    }
    static void Seats(SeatSelectionPage page)
    {
        for(int i=0;i<page.CurrentBooking.Flight.Segments.Count;i++)
        {
            page.SelectSegment(i);
            foreach(var b in page.GetComponentsInChildren<UIButton>().Where(x=>x.name.StartsWith("Seat ")&&x.interactable).Take(2).ToArray())b.onClick.Invoke();
        }
        Click(page.transform,"Confirm Seat");
    }
    AppServices original;TravelCatalog catalog;
    [TearDown] public void Cleanup() {if(original!=null)typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);if(catalog!=null)Object.DestroyImmediate(catalog);}
    [UnityTest,Timeout(45000)] public IEnumerator ReviewLegsPanelsTotalsAndExistingNavigationUseSharedBooking()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var runtime=TravelAppRuntime.Instance;original=runtime.Services;catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,new AppServices(catalog,new MemoryStore()));
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");
        var dates=Field<DateSelectionPage>(nav,"datePage");var results=Field<FlightResultsPage>(nav,"flightResults");
        foreach(var pair in new[]{new{Id="B6-100",Round=false},new{Id="UA-107",Round=false},new{Id="B6-110",Round=true}})
        {
            var dep=new DateTime(2027,7,14);DateTime? ret=pair.Round?dep.AddDays(7):(DateTime?)null;
            Invoke(dates,"RestoreDates",(DateTime?)dep,ret,!pair.Round);Invoke(results,"SetBookingOptions",2,"Business");Invoke(nav,"ShowFlightResults",dep,ret);
            Select(results,pair.Id);Seats(Field<SeatSelectionPage>(nav,"seatPage"));
            if(pair.Round){Select(Field<FlightResultsPage>(nav,"returnResults"),"UA-107");Seats(Field<SeatSelectionPage>(nav,"returnSeats"));}
            var review=Field<TripReviewPage>(nav,"tripReview");yield return null;Canvas.ForceUpdateCanvases();
            var outbound=nav.CurrentSeatBooking;var inbound=nav.CurrentReturnSeatBooking;
            Assert.IsTrue(review.gameObject.activeInHierarchy);StringAssert.Contains("JFK → CUN",Text(review.transform));
            Assert.AreEqual((outbound.FarePerTraveler+(pair.Round?inbound.FarePerTraveler:0))*2,review.GrandTotal);
            Assert.AreEqual(review.GrandTotal,review.DisplayBaseFare+review.DisplayTaxes+review.DisplayAdditionalCharges);
            var legs=review.transform.Find("Review itinerary viewport/Viewport/Legs");
            Assert.AreEqual(pair.Round,legs.Find("Return").gameObject.activeSelf);
            var departure=legs.Find("Departure");
            var ef=(RectTransform)Button(departure,"Edit flight").transform;var es=(RectTransform)Button(departure,"Edit seats").transform;
            var fd=(RectTransform)Button(departure,"Flight details").transform;var fr=(RectTransform)Button(departure,"Fare rules").transform;
            Assert.AreEqual(ef.anchoredPosition.y,es.anchoredPosition.y);Assert.Greater(ef.anchoredPosition.y,fd.anchoredPosition.y);Assert.Greater(fd.anchoredPosition.y,fr.anchoredPosition.y);
            Assert.AreEqual(fd.anchoredPosition.x,fr.anchoredPosition.x);Assert.AreEqual(fd.rect.height,fr.rect.height);
            foreach(var control in new[]{ef,es,fd,fr})Assert.AreEqual(TextAlignmentOptions.Center,control.GetComponentInChildren<TMP_Text>().alignment);
            Assert.IsNull(review.transform.Find("24-hour cancellation"));
            Click(departure,"Flight details");yield return null;
            var overlay=Field<GameObject>(review,"overlay");Assert.IsTrue(overlay.activeSelf);
            string details=Text(overlay.transform);
            foreach(var s in outbound.Flight.Segments)
            {
                StringAssert.Contains(s.FlightNumber,details);StringAssert.Contains(s.DepartureLabel,details);StringAssert.Contains(s.ArrivalLabel,details);
                StringAssert.Contains(s.AircraftType,details);foreach(var a in s.Amenities)StringAssert.Contains(a,details);
                StringAssert.Contains(TravelApp.FlightMap.TravelLocationCatalog.Find(s.OriginCode).airport,details);
            }
            foreach(var l in outbound.Flight.Layovers)StringAssert.Contains(l.Description,details);
            foreach(var row in outbound.SegmentSeats)foreach(var seat in row)StringAssert.Contains("Seat "+seat,details);
            var panelScroll=Field<ScrollRect>(review,"drawerScroll");
            if(outbound.Flight.Stops>0){Assert.Greater(panelScroll.content.rect.height,panelScroll.viewport.rect.height);panelScroll.verticalNormalizedPosition=0;yield return null;Assert.Greater(panelScroll.content.anchoredPosition.y,0);}
            var underlying=Button(review.transform,"Edit route");var underlyingRect=(RectTransform)underlying.transform;
            var blocked=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,underlyingRect.TransformPoint(underlyingRect.rect.center))};
            var blockingHits=new List<RaycastResult>();EventSystem.current.RaycastAll(blocked,blockingHits);
            Assert.AreNotEqual(underlying.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(blockingHits[0].gameObject),"The open panel must block input to the review page");
            PointerClick(Button(overlay.transform,"Close review details"));Assert.IsFalse(overlay.activeSelf);
            Click(departure,"Fare rules");StringAssert.Contains("Changes",Text(overlay.transform));StringAssert.Contains("24-hour",Text(overlay.transform));
            Click(overlay.transform,"Baggage");StringAssert.Contains(outbound.Flight.Baggage.Summary,Text(overlay.transform));StringAssert.Contains("not actual airline policies",Text(overlay.transform));
            Click(overlay.transform,"Close panel");
            if(pair.Round)
            {
                Click(legs.Find("Return"),"Flight details");StringAssert.Contains("CUN",Text(overlay.transform));StringAssert.Contains(inbound.Flight.Segments[0].FlightNumber,Text(overlay.transform));Click(overlay.transform,"Close panel");
            }
            Assert.IsTrue(nav.SaveActiveDraft().success);var saved=runtime.Services.DraftTrips.Last();nav.ResumeDraft(saved);
            Assert.AreEqual(review.GrandTotal,(outbound.FarePerTraveler+(pair.Round?inbound.FarePerTraveler:0))*2);
            Click(review.transform,"Progress Seats");Assert.AreEqual(outbound.Flight.Id,nav.CurrentSeatBooking.Flight.Id);Assert.IsTrue(Field<SeatSelectionPage>(nav,"seatPage").gameObject.activeInHierarchy);
            Invoke(nav,"ShowTripReview");Click(review.transform,"Continue to Payment");
            var payment=Field<PaymentPlaceholderPage>(nav,"paymentPage");Assert.AreEqual(review.GrandTotal,payment.Booking.EstimatedTotal);
            Invoke(nav,"ShowTripReview");Click(departure,"Edit seats");Assert.IsTrue(Field<SeatSelectionPage>(nav,"seatPage").gameObject.activeInHierarchy);
            Invoke(nav,"ShowTripReview");Click(departure,"Edit flight");Assert.IsTrue(results.gameObject.activeInHierarchy);
            Invoke(nav,"ShowTripReview");Click(review.transform,"Edit dates");Assert.IsTrue(Field<CompactFlightSearch>(nav,"compactSearch").IsCalendarOpen);
            Field<CompactFlightSearch>(nav,"compactSearch").CloseCalendar();Invoke(nav,"ShowTripReview");Click(review.transform,"Edit route");Assert.IsTrue(Field<GameObject[]>(nav,"pages")[0].activeInHierarchy);
        }
    }
}
