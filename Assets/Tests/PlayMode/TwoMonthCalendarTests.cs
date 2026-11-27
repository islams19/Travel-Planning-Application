using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UIButton=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

public class TwoMonthCalendarTests
{
    sealed class MemoryStore : ISaveStore { public SaveData Load()=>new SaveData(); public void Save(SaveData data) { } }
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>(true).Single(b=>b.name==name && b.gameObject.activeInHierarchy);
    static IEnumerator Click(UIButton button)
    {
        yield return null; Canvas.ForceUpdateCanvases(); Assert.IsTrue(button.interactable,button.name);
        var rect=(RectTransform)button.transform; var canvas=button.GetComponentInParent<Canvas>();
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsTrue(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),button.name+" must receive pointer clicks");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Click(Transform root,string name)=>Click(Button(root,name));
    static int Cell(DateSelectionPage page,DateTime date)=>Array.FindIndex(Field<DateTime?[]>(page,"cellDates"),d=>d==date);
    static UIButton Day(DateSelectionPage page,DateTime date)=>Field<UIButton[]>(page,"dayButtons")[Cell(page,date)];
    static string Price(DateSelectionPage page,DateTime date)=>Field<TMP_Text[]>(page,"fareLabels")[Cell(page,date)].text;
    static int Value(string price)=>int.Parse(price.TrimStart('$'),NumberStyles.AllowThousands,CultureInfo.InvariantCulture);
    static DateTime? Departure(DateSelectionPage page)=>Field<DateTime?>(page,"departureDate");
    static DateTime? Return(DateSelectionPage page)=>Field<DateTime?>(page,"returnDate");
    AppServices original; TravelCatalog catalog;
    static readonly System.Reflection.FieldInfo CalendarToday=typeof(DateSelectionPage).GetField("Today",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
    static void PinToday(DateTime day)=>CalendarToday.SetValue(null,(Func<DateTime>)(()=>day));
    [TearDown] public void Cleanup()
    {
        CalendarToday.SetValue(null,(Func<DateTime>)(()=>DateTime.Today));
        if(original!=null) typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);
        if(catalog!=null) Object.DestroyImmediate(catalog);
    }
    [UnityTest,Timeout(45000)] public IEnumerator TwoMonthsPricesCrossMonthRangeNavigationEditingAndDraftsUseSharedState()
    {
        // Pin "today" to the start of a month so the test's relative dates are never in the past.
        PinToday(new DateTime(2026,11,1));
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        var runtime=TravelAppRuntime.Instance; original=runtime.Services; catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,new AppServices(catalog,new MemoryStore()));
        var nav=Object.FindFirstObjectByType<SidebarNavigation>(); var search=Field<GameObject[]>(nav,"pages")[0];
        var dates=Field<DateSelectionPage>(nav,"datePage"); var results=Field<FlightResultsPage>(nav,"flightResults");
        var baseFares=Field<List<FlightResultsPage.Flight>>(results,"flights").Select(f=>f.Price).ToArray();
        yield return Click(search.transform,"card-Tokyo"); yield return Click(search.transform,"Departure date");
        var first=Field<DateTime>(dates,"displayedMonth");
        Assert.AreEqual(first.ToString("MMMM yyyy",CultureInfo.InvariantCulture),Field<TMP_Text>(dates,"monthLabel").text);
        Assert.AreEqual(first.AddMonths(1).ToString("MMMM yyyy",CultureInfo.InvariantCulture),Field<TMP_Text>(dates,"secondMonthLabel").text);
        Assert.AreEqual(84,Field<UIButton[]>(dates,"dayButtons").Length);
        var visible=Field<DateTime?[]>(dates,"cellDates").Where(d=>d.HasValue).Select(d=>d.Value).ToArray();
        Assert.AreEqual(DateTime.DaysInMonth(first.Year,first.Month)+DateTime.DaysInMonth(first.AddMonths(1).Year,first.AddMonths(1).Month),visible.Length);
        Assert.IsTrue(visible.All(d=>Price(dates,d).StartsWith("$")));
        var colors=Field<TMP_Text[]>(dates,"fareLabels").Where(t=>t.gameObject.activeInHierarchy && t.text.StartsWith("$")).Select(t=>t.color).ToArray();
        Assert.IsTrue(colors.Contains((Color)new Color32(22,128,87,255))); Assert.Greater(colors.Distinct().Count(),1);
        StringAssert.Contains("Not live or guaranteed",Field<TMP_Text>(dates,"fareNote").text);
        var shared=first.AddMonths(1).AddDays(4); string oldPrice=Price(dates,shared);
        yield return Click(dates.transform,"Next month"); Assert.AreEqual(first.AddMonths(1),Field<DateTime>(dates,"displayedMonth"));
        Assert.AreEqual(oldPrice,Price(dates,shared)); yield return Click(dates.transform,"Previous month");
        var departing=first.AddDays(DateTime.DaysInMonth(first.Year,first.Month)-3); var returning=first.AddMonths(1).AddDays(3);
        yield return Click(Day(dates,departing)); Assert.AreEqual(departing,Departure(dates)); Assert.IsNull(Return(dates));
        Assert.IsFalse(Day(dates,departing.AddDays(-1)).interactable);
        Day(dates,departing.AddDays(-1)).onClick.Invoke(); Assert.AreEqual(departing,Departure(dates)); Assert.IsNull(Return(dates));
        Assert.IsFalse(Button(dates.transform,"Confirm Dates").interactable);
        yield return Click(Day(dates,returning)); Assert.AreEqual(returning,Return(dates));
        Assert.AreEqual((Color)new Color32(47,80,247,255),Day(dates,departing).targetGraphic.color);
        Assert.AreEqual((Color)new Color32(47,80,247,255),Day(dates,returning).targetGraphic.color);
        Assert.AreEqual((Color)new Color32(235,240,255,255),Day(dates,first.AddMonths(1)).targetGraphic.color);
        yield return Click(dates.transform,"Confirm Dates"); Assert.IsFalse(dates.gameObject.activeInHierarchy);
        StringAssert.Contains(departing.ToString("ddd, MMM d",CultureInfo.InvariantCulture),Button(search.transform,"Departure date").GetComponentInChildren<TMP_Text>().text);
        StringAssert.Contains(returning.ToString("ddd, MMM d",CultureInfo.InvariantCulture),Button(search.transform,"Return date").GetComponentInChildren<TMP_Text>().text);
        CollectionAssert.AreEqual(baseFares,Field<List<FlightResultsPage.Flight>>(results,"flights").Select(f=>f.Price));
        Assert.IsNull(results.SelectedDeparture,"Calendar estimates must not select or price an itinerary");
        yield return Click(search.transform,"Return date");
        Assert.AreEqual(first,Field<DateTime>(dates,"displayedMonth"),"A consecutive-month selection should remain visible together");
        yield return Click(Day(dates,returning.AddDays(2))); Assert.AreEqual(departing,Departure(dates));
        yield return Click(dates.transform,"Confirm Dates"); returning=returning.AddDays(2);
        Assert.IsTrue(nav.SaveActiveDraft().success); var draft=runtime.Services.DraftTrips.Single(); nav.ResumeDraft(draft);
        Assert.AreEqual(departing,Departure(dates)); Assert.AreEqual(returning,Return(dates));
        yield return Click(search.transform,"Search cabin"); yield return Click(search.transform,"Cabin Business");
        yield return Click(search.transform,"Departure date");
        int expected=DateFareEstimates.EconomyFare(departing,"JFK","HND").Value;
        int adjusted=(int)results.GetType().GetMethod("AdjustFare",Private).Invoke(results,new object[]{expected});
        Assert.AreEqual(adjusted,Value(Price(dates,departing)));
        // An explicit departure edit starts a new range; it can move earlier than the old departure.
        yield return Click(Day(dates,departing.AddDays(-2))); Assert.AreEqual(departing.AddDays(-2),Departure(dates)); Assert.IsNull(Return(dates));
        yield return Click(Day(dates,returning)); yield return Click(dates.transform,"Confirm Dates");
        // December/January navigation preserves both labels and the selected range.
        Invoke(dates,"RestoreDates",(DateTime?)new DateTime(2026,12,28),(DateTime?)new DateTime(2027,1,5),false);
        yield return Click(search.transform,"Return date");
        Assert.AreEqual("December 2026",Field<TMP_Text>(dates,"monthLabel").text); Assert.AreEqual("January 2027",Field<TMP_Text>(dates,"secondMonthLabel").text);
        yield return Click(dates.transform,"Next month"); Assert.AreEqual("February 2027",Field<TMP_Text>(dates,"secondMonthLabel").text);
        yield return Click(dates.transform,"Previous month"); yield return Click(dates.transform,"Confirm Dates");
        yield return Click(search.transform,"Search trip type"); yield return Click(search.transform,"One way");
        yield return Click(search.transform,"Departure date");
        yield return Click(Day(dates,new DateTime(2026,12,26))); Assert.IsNull(Return(dates)); Assert.IsTrue(Button(dates.transform,"Confirm Dates").interactable);
        yield return Click(dates.transform,"Confirm Dates"); yield return Click(search.transform,"Search Flights");
        Assert.IsTrue(results.gameObject.activeInHierarchy); Assert.IsNull(Field<DateTime?>(nav,"returnDate"));
        CollectionAssert.AreEqual(MockFlightGenerator.Generate(TravelApp.FlightMap.TravelLocationCatalog.Find("JFK"),TravelApp.FlightMap.TravelLocationCatalog.Find("HND"),Departure(dates).Value).Select(f=>f.Price),Field<List<FlightResultsPage.Flight>>(results,"flights").Select(f=>f.Price));
        Assert.AreEqual(1,Object.FindObjectsByType<DateSelectionPage>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        runtime.Services.RemoveDraft(draft.id);
    }
    [UnityTest,Timeout(30000)] public IEnumerator PastDatesAreGreyedOutAndCannotBeChosen()
    {
        PinToday(new DateTime(2026,11,26));
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        var runtime=TravelAppRuntime.Instance; original=runtime.Services; catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,new AppServices(catalog,new MemoryStore()));
        var nav=Object.FindFirstObjectByType<SidebarNavigation>(); var search=Field<GameObject[]>(nav,"pages")[0];
        var dates=Field<DateSelectionPage>(nav,"datePage");
        yield return Click(search.transform,"card-Tokyo"); yield return Click(search.transform,"Departure date");
        Assert.AreEqual(new DateTime(2026,11,1),Field<DateTime>(dates,"displayedMonth"));
        Assert.IsFalse(Button(dates.transform,"Previous month").interactable,"The calendar must not page back before the current month");
        var yesterday=new DateTime(2026,11,25);
        Assert.IsFalse(Day(dates,yesterday).interactable); Assert.AreEqual("",Price(dates,yesterday));
        Day(dates,yesterday).onClick.Invoke(); Assert.IsNull(Departure(dates));
        Assert.IsTrue(Day(dates,new DateTime(2026,11,26)).interactable,"Today stays bookable");
        yield return Click(Day(dates,new DateTime(2026,11,26))); Assert.AreEqual(new DateTime(2026,11,26),Departure(dates));
        yield return Click(dates.transform,"Next month"); Assert.IsTrue(Button(dates.transform,"Previous month").interactable);
        // A restored trip whose dates have passed asks for new dates instead of searching.
        Invoke(dates,"RestoreDates",(DateTime?)new DateTime(2026,11,8),(DateTime?)new DateTime(2026,11,15),false);
        Field<CompactFlightSearch>(nav,"compactSearch").CloseCalendar();
        yield return Click(search.transform,"Search Flights");
        Assert.IsFalse(Field<FlightResultsPage>(nav,"flightResults").gameObject.activeInHierarchy);
        Assert.IsTrue(Field<CompactFlightSearch>(nav,"compactSearch").IsCalendarOpen);
        StringAssert.Contains("passed",Field<TMP_Text>(Field<CompactFlightSearch>(nav,"compactSearch"),"message").text);
    }
    [UnityTest,Timeout(30000)] public IEnumerator DateValidationMessageClearsOnceDatesAreChosen()
    {
        PinToday(new DateTime(2026,11,1));
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        var runtime=TravelAppRuntime.Instance; original=runtime.Services; catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,new AppServices(catalog,new MemoryStore()));
        var nav=Object.FindFirstObjectByType<SidebarNavigation>(); var search=Field<GameObject[]>(nav,"pages")[0];
        var dates=Field<DateSelectionPage>(nav,"datePage"); var compact=Field<CompactFlightSearch>(nav,"compactSearch");
        yield return Click(search.transform,"card-Tokyo"); yield return Click(search.transform,"Search Flights");
        Assert.AreEqual("Choose the required travel dates.",Field<TMP_Text>(compact,"message").text);
        yield return Click(Day(dates,new DateTime(2026,12,10))); yield return Click(Day(dates,new DateTime(2026,12,20)));
        yield return Click(dates.transform,"Confirm Dates");
        Assert.AreEqual("",Field<TMP_Text>(compact,"message").text,"A fixed date error must not linger");
    }
}
