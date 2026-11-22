using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public class ConnectingFlightTests
{
    sealed class MemoryStore : TravelApp.ISaveStore
    {
        public TravelApp.SaveData Load()=>new TravelApp.SaveData();
        public void Save(TravelApp.SaveData data) { }
    }
    TravelApp.AppServices original;
    TravelCatalog catalog;
    [TearDown] public void Cleanup()
    {
        if(original!=null) typeof(TravelApp.TravelAppRuntime).GetProperty("Services").SetValue(TravelApp.TravelAppRuntime.Instance,original);
        if(catalog!=null) Object.DestroyImmediate(catalog);
    }
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>(true).First(b=>b.name==name && b.gameObject.activeInHierarchy);
    static List<FlightResultsPage.Flight> Visible(FlightResultsPage page)=>Field<List<FlightResultsPage.Flight>>(page,"flights")
        .Where(f=>((RectTransform)typeof(FlightResultsPage.Flight).GetProperty("CardView",Private).GetValue(f)).gameObject.activeSelf)
        .OrderBy(f=>((RectTransform)typeof(FlightResultsPage.Flight).GetProperty("CardView",Private).GetValue(f)).GetSiblingIndex()).ToList();
    static RectTransform Card(FlightResultsPage.Flight flight)=>(RectTransform)typeof(FlightResultsPage.Flight).GetProperty("CardView",Private).GetValue(flight);
    static void Refresh(FlightResultsPage page)=>Invoke(page,"RefreshResults");
    [UnityTest,Timeout(45000)] public IEnumerator RepeatedAirlinesConnectionsFiltersSortScrollDetailsAndReviewPersist()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        var runtime=TravelApp.TravelAppRuntime.Instance; original=runtime.Services;
        catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        typeof(TravelApp.TravelAppRuntime).GetProperty("Services").SetValue(runtime,new TravelApp.AppServices(catalog,new MemoryStore()));
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();
        var search=Field<GameObject[]>(nav,"pages")[0]; Button(search.transform,"card-Tokyo").onClick.Invoke();
        Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)new DateTime(2027,5,14),(DateTime?)null,true);
        Invoke(nav,"ShowFlightResults",new DateTime(2027,5,14),(DateTime?)null);
        var page=Field<FlightResultsPage>(nav,"flightResults");
        var all=Field<List<FlightResultsPage.Flight>>(page,"flights");
        Assert.That(all.Count,Is.InRange(10,15)); Assert.AreEqual(all.Count,all.Select(f=>f.Id).Distinct().Count());
        Assert.IsTrue(all.GroupBy(f=>f.Code).All(g=>g.Count()>1));
        Assert.IsTrue(all.Any(f=>f.Stops==0)); Assert.IsTrue(all.Any(f=>f.Stops==1)); Assert.IsTrue(all.Any(f=>f.Stops==2));
        Assert.IsTrue(all.Any(f=>f.Stops>0 && f.Price<all.Where(n=>n.Stops==0).Max(n=>n.Price)));
        foreach(var flight in all)
        {
            Assert.AreEqual(flight.Stops+1,flight.Segments.Count);
            Assert.AreEqual("JFK",flight.Segments.First().OriginCode); Assert.AreEqual("HND",flight.Segments.Last().DestinationCode);
            Assert.AreEqual(flight.DurationMinutes,flight.Segments.Sum(s=>s.DurationMinutes)+flight.Layovers.Sum(l=>l.DurationMinutes));
            Assert.AreEqual(flight.Arrival,flight.Segments.Last().ArrivalLabel);
            for(int i=0;i<flight.Layovers.Count;i++)
            {
                Assert.AreEqual(flight.Segments[i].DestinationCode,flight.Segments[i+1].OriginCode);
                Assert.AreEqual(flight.Layovers[i].DurationMinutes,flight.Segments[i+1].DepartureMinutes-flight.Segments[i].ArrivalMinutes);
            }
        }
        Assert.IsTrue(all.Any(f=>f.Arrival.Contains("+1")));
        Button(page.transform,"Filters").onClick.Invoke();
        foreach(int maxStops in new[]{0,1,2})
        {
            Button(page.transform,"Stops").onClick.Invoke();
            CollectionAssert.AreEquivalent(all.Where(f=>f.Stops<=maxStops),Visible(page));
        }
        Button(page.transform,"Stops").onClick.Invoke(); Button(page.transform,"Done").onClick.Invoke();
        Set(page,"airlineFilter",2); Refresh(page); Assert.IsTrue(Visible(page).All(f=>f.Code=="DL")); Assert.Greater(Visible(page).Count,1);
        Set(page,"airlineFilter",0);
        for(int t=1;t<=3;t++)
        {
            Set(page,"timeFilter",t); Refresh(page);
            CollectionAssert.AreEquivalent(all.Where(f=>{int h=f.Segments[0].DepartureLocal.Hour; return t==1?h<12:t==2?h>=12&&h<18:h>=18;}),Visible(page));
        }
        Set(page,"timeFilter",0); Set(page,"cabin",2); Set(page,"passengers",3); Set(page,"maximumPrice",500); Refresh(page);
        CollectionAssert.AreEquivalent(all.Where(f=>Mathf.RoundToInt(f.Price*2.2f)<=500),Visible(page));
        Set(page,"maximumPrice",2000);
        Set(page,"durationFilter",2); Refresh(page); CollectionAssert.AreEquivalent(all.Where(f=>f.DurationMinutes<=540),Visible(page));
        Assert.IsTrue(page.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="No flights match these filters."));
        Set(page,"durationFilter",0);
        for(int order=0;order<3;order++)
        {
            Set(page,"sort",order); Refresh(page);
            var expected=order==0?all.OrderBy(f=>Mathf.RoundToInt(f.Price*2.2f)):order==1?all.OrderBy(f=>f.DurationMinutes):all.OrderBy(f=>f.Segments[0].DepartureLocal.TimeOfDay);
            CollectionAssert.AreEqual(expected,Visible(page));
        }
        var scroll=Field<ScrollRect>(page,"resultsScroll"); Canvas.ForceUpdateCanvases();
        Assert.Greater(scroll.content.rect.height,scroll.viewport.rect.height);
        Assert.IsTrue(scroll.GetComponent<Image>().raycastTarget,"Scrolling must receive pointer events over card backgrounds and gaps");
        float summaryY=page.transform.Find("Trip summary").position.y;
        scroll.verticalNormalizedPosition=0; yield return null;
        Assert.Greater(scroll.content.anchoredPosition.y,100); Assert.AreEqual(summaryY,page.transform.Find("Trip summary").position.y);
        var connecting=all.First(f=>f.Id=="AA-106");
        Button(Card(connecting),"Flight details").onClick.Invoke();
        var detailRoot=Field<RectTransform>(page,"detailsContent");
        string detailCopy=string.Join("\n",detailRoot.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
        foreach(var segment in connecting.Segments)
        {StringAssert.Contains(segment.OriginCode+" → "+segment.DestinationCode,detailCopy);StringAssert.Contains(segment.DepartureLabel,detailCopy);StringAssert.Contains(segment.ArrivalLabel,detailCopy);StringAssert.Contains(segment.FlightNumber,detailCopy);}
        StringAssert.Contains(connecting.Layovers[0].Description,detailCopy);
        Canvas.ForceUpdateCanvases();
        var detailText=detailRoot.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Summary route");detailText.ForceMeshUpdate();
        Assert.GreaterOrEqual(detailText.fontSize,18);Assert.Greater(detailCopy.Length,100);
        var detailScroll=Field<ScrollRect>(page,"detailsScroll");Assert.Greater(detailScroll.content.rect.height,detailScroll.viewport.rect.height);
        foreach(var text in page.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Flight times"))
        { text.ForceMeshUpdate(true); Assert.GreaterOrEqual(text.fontSize,19); Assert.Greater(text.textInfo.characterCount,10);
          Assert.IsTrue(text.textInfo.characterInfo[text.textInfo.characterCount-1].isVisible,"Arrival times must fit in the card"); }
        Button(page.transform,"Close flight details").onClick.Invoke();
        Button(Card(connecting),"Select").onClick.Invoke();
        Assert.AreEqual(connecting,nav.CurrentSeatBooking.Flight); Assert.AreEqual(3,nav.CurrentSeatBooking.PassengerCount); Assert.AreEqual("Business",nav.CurrentSeatBooking.Cabin);
        var seats=Field<SeatSelectionPage>(nav,"seatPage");
        for(int segment=0;segment<connecting.Segments.Count;segment++)
        {
            seats.SelectSegment(segment);
            foreach(string code in new[]{"1A","1B","1D"}) Button(seats.transform,"Seat "+code).onClick.Invoke();
        }
        Button(seats.transform,"Confirm Seat").onClick.Invoke();
        var review=Field<TripReviewPage>(nav,"tripReview"); Assert.IsTrue(review.gameObject.activeInHierarchy);
        foreach(var segment in connecting.Segments) StringAssert.Contains(segment.OriginCode+" → "+segment.DestinationCode,string.Join("\n",review.GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
        var draft=nav.SaveActiveDraft(); Assert.IsTrue(draft.success,draft.message);
        var saved=TravelApp.TravelAppRuntime.Instance.Services.DraftTrips.Last(); Assert.AreEqual(connecting.Id,saved.outbound.itineraryId);
        nav.ResumeDraft(saved); Assert.AreEqual(connecting.Id,page.SelectedDeparture.Id);
        Assert.AreEqual(connecting.Baggage.Summary,page.SelectedDeparture.Baggage.Summary);
        Invoke(nav,"ContinueToPayment");
        var snapshot=Field<PaymentBooking>(nav,"paymentBooking"); Assert.IsNotNull(snapshot);
        Assert.AreEqual(connecting.Id,snapshot.Outbound.ItineraryId); Assert.AreEqual(2,snapshot.Outbound.Segments.Count);
        Assert.AreEqual(connecting.Layovers[0].AirportCode,snapshot.Outbound.Layovers[0].AirportCode);
        Assert.AreEqual(connecting.Baggage.Summary,snapshot.Outbound.Baggage.Summary);
        StringAssert.Contains(connecting.Baggage.Summary,CompletedDemoBooking.Itinerary(snapshot));
        foreach(var segment in connecting.Segments) StringAssert.Contains(segment.OriginCode+" → "+segment.DestinationCode,CompletedDemoBooking.Itinerary(snapshot));
        TravelApp.TravelAppRuntime.Instance.Services.RemoveDraft(saved.id);
    }
}
