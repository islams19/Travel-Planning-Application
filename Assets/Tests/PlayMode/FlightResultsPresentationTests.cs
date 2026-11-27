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

public class FlightResultsPresentationTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>(true).First(b=>b.name==name && b.gameObject.activeInHierarchy);
    static RectTransform Card(FlightResultsPage.Flight f)=>(RectTransform)f.GetType().GetProperty("CardView",Private).GetValue(f);
    static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);

    [UnityTest,Timeout(45000)] public IEnumerator HorizontalCardsLogosBaggageAndChevronInteractions()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();
        var pages=Field<GameObject[]>(nav,"pages"); var dates=Field<DateSelectionPage>(nav,"datePage");
        var form=pages[0].GetComponent<CompactFlightSearch>();
        var trigger=Button(form.transform,"Search cabin"); var chevron=trigger.GetComponent<DropdownChevron>();
        Assert.IsNotNull(chevron); Assert.AreEqual(0,chevron.BackgroundAlpha);
        var pointer=new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(trigger.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        float hover=chevron.BackgroundAlpha; Assert.Greater(hover,0);
        ExecuteEvents.Execute(trigger.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        Assert.Greater(chevron.BackgroundAlpha,hover);
        ExecuteEvents.Execute(trigger.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        trigger.onClick.Invoke(); Assert.Greater(chevron.BackgroundAlpha,hover);
        Button(form.transform,"Cabin Business").onClick.Invoke();
        ExecuteEvents.Execute(trigger.gameObject,pointer,ExecuteEvents.pointerExitHandler); Assert.AreEqual(0,chevron.BackgroundAlpha);
        Assert.IsFalse(trigger.transform.Find("Chevron").GetComponent<Graphic>().raycastTarget);
        var rect=(RectTransform)trigger.transform;
        Canvas.ForceUpdateCanvases();
        var screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        pointer.position=screen; var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsTrue(hits.Any(h=>h.gameObject==trigger.gameObject || h.gameObject.transform.IsChildOf(trigger.transform)));
        Button(pages[0].transform,"card-Tokyo").onClick.Invoke();
        Invoke(dates,"RestoreDates",(DateTime?)new DateTime(2027,5,14),(DateTime?)null,true);
        Invoke(nav,"ShowFlightResults",new DateTime(2027,5,14),(DateTime?)null);
        var page=Field<FlightResultsPage>(nav,"flightResults");
        Invoke(page,"SetBookingOptions",3,"Business");
        yield return null; Canvas.ForceUpdateCanvases();
        var all=Field<List<FlightResultsPage.Flight>>(page,"flights"); Assert.AreEqual(12,all.Count);
        var scroll=Field<ScrollRect>(page,"resultsScroll");
        var ordered=all.OrderBy(f=>Card(f).GetSiblingIndex()).ToArray();
        for(int i=0;i<ordered.Length;i++)
        {
            var flight=ordered[i];var card=Card(flight);
            Assert.AreEqual(scroll.viewport.rect.width,card.rect.width,2);
            Assert.AreEqual(0,card.anchoredPosition.x,.1f);
            if(i>0) Assert.Greater(Card(ordered[i-1]).anchoredPosition.y-card.anchoredPosition.y,card.rect.height);
            var texture=AirlineLogo.Load(flight.Code);
            if(texture!=null)
            {
                var logo=card.Find("Airline badge/Airline logo").GetComponent<RawImage>();Assert.AreSame(texture,logo.texture);
                Assert.AreEqual((float)logo.texture.width/logo.texture.height,logo.rectTransform.rect.width/logo.rectTransform.rect.height,.01f);
                Assert.LessOrEqual(logo.rectTransform.rect.width,logo.texture.width);Assert.LessOrEqual(logo.rectTransform.rect.height,logo.texture.height);
            }
            else Assert.AreEqual(flight.Code,card.Find("Airline badge").GetComponentInChildren<TMP_Text>().text);
            StringAssert.Contains(flight.Baggage.CheckedBagLabel,card.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Checked baggage").text);
            StringAssert.Contains("3 travelers",card.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Traveler total").text);
        }
        Assert.GreaterOrEqual(scroll.viewport.rect.height/198f,3,"At least three complete rows fit at 1920×1080");
        Button(page.transform,"Sort").onClick.Invoke(); Button(page.transform,"Earliest departure").onClick.Invoke();
        var sortLabel=Button(page.transform,"Sort").GetComponentInChildren<TMP_Text>(); sortLabel.ForceMeshUpdate();
        Assert.GreaterOrEqual(sortLabel.fontSize,16);
        Assert.LessOrEqual(sortLabel.textBounds.max.x,sortLabel.rectTransform.rect.xMax-24,"The longest sort caption must not overlap its chevron");
        foreach(var icon in page.GetComponentsInChildren<BaggageIcon>(true)) Assert.IsTrue(icon.maskable,"Decorative icons must respect the results viewport");
        var first=ordered[0]; var before=Card(first).position.y;
        ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-4)},ExecuteEvents.scrollHandler);
        yield return null; Assert.AreNotEqual(before,Card(first).position.y);
        Assert.IsNull(AirlineLogo.Load("UNKNOWN"));
        typeof(AirlineLogo).GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{dates,page.transform,0f,0f,100f,40f,"UNKNOWN"});
        var fallback=page.transform.Find("Airline badge"); Assert.AreEqual("UNKNOWN",fallback.GetComponentInChildren<TMP_Text>().text);
        Assert.IsNull(fallback.GetComponentInChildren<RawImage>()); Object.Destroy(fallback.gameObject);
        var connecting=all.First(f=>f.Stops==1); Button(Card(connecting),"Flight details").onClick.Invoke();
        StringAssert.Contains(connecting.Baggage.Summary,string.Join("\n",Field<RectTransform>(page,"detailsContent").GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
        StringAssert.Contains("not actual airline policies",string.Join("\n",Field<RectTransform>(page,"detailsContent").GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
        Button(page.transform,"Close flight details").onClick.Invoke();
        Button(Card(connecting),"Select").onClick.Invoke();
        var booking=nav.CurrentSeatBooking;
        Assert.AreSame(connecting.Baggage,booking.Flight.Baggage);
        var snapshot=(PaymentBooking)Activator.CreateInstance(typeof(PaymentBooking),Private,null,new object[]{booking,null},null);
        Assert.AreEqual(connecting.Baggage.Summary,snapshot.Outbound.Baggage.Summary);
        StringAssert.Contains(connecting.Baggage.Summary,CompletedDemoBooking.Itinerary(snapshot));
        var review=BookingItineraryText.Describe(connecting.Airline,connecting.Segments,connecting.Layovers,booking.SegmentSeats,"Business",booking.FarePerTraveler,3,connecting.Baggage);
        StringAssert.Contains(connecting.Baggage.Summary,review);
    }
}
