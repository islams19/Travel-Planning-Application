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
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public sealed class ManualReviewRefinementTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static UIButton Button(Transform parent,string name)=>parent.GetComponentsInChildren<UIButton>(true).First(b=>b.name==name);
    static void Pointer(UIButton button)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Assert.IsNotEmpty(hits,button.name);
        Assert.AreEqual(button.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),button.name+" blocked by "+hits[0].gameObject.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    sealed class Store:ISaveStore{public SaveData Load()=>new SaveData();public void Save(SaveData data){}}
    AppServices original;TravelCatalog catalog;
    IEnumerator Setup()
    {
        yield return null;var runtime=TravelAppRuntime.Instance;original=runtime.Services;catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        var services=new AppServices(catalog,new Store());services.Accounts.Register("ManualReview","review@example.com","test-pass");typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,services);
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {if(TravelAppRuntime.Instance!=null&&original!=null)typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);if(catalog!=null)Object.Destroy(catalog);yield return null;}
    [UnityTest,Timeout(45000)] public IEnumerator ProfileChoicesPersistWithoutChangingCurrentTrip()
    {
        yield return Setup();var nav=Object.FindAnyObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");
        var origin=Field<DestinationAutocomplete>(nav,"originSearch");string before=origin.SelectedAirportCode;
        var profile=Object.FindAnyObjectByType<SharedProfileMenu>();profile.Toggle();yield return null;
        var overlay=profile.GetComponentInParent<Canvas>().transform.Find("Shared profile overlay");Pointer(Button(overlay,"Profile"));yield return null;
        var page=Object.FindAnyObjectByType<ProfilePage>();Assert.IsNotNull(page);Assert.IsFalse(Field<UIButton[]>(nav,"buttons")[3].gameObject.activeInHierarchy);
        Field<TMP_InputField>(page,"nameInput").text="Review Traveler";Field<TMP_InputField>(page,"emailInput").text="traveler@example.com";
        Field<DestinationAutocomplete>(page,"home").TrySelectAirportDestination("EWR");
        foreach(var choice in new[]{new[]{"Preferred cabin class","Choose Business"},new[]{"Preferred seat type","Choose Window"},new[]{"Distance units","Choose Miles"}})
        {Pointer(Button(page.transform,choice[0]));yield return null;Pointer(Button(page.transform,choice[1]));yield return null;}
        Pointer(Button(page.transform,"Save profile"));yield return null;
        var accounts=TravelAppRuntime.Instance.Services.Accounts;Assert.AreEqual("Review Traveler",accounts.DisplayName);Assert.AreEqual("EWR",accounts.Preferences.homeAirportCode);Assert.AreEqual("Business",accounts.Preferences.cabinClass);Assert.AreEqual("Window",accounts.Preferences.seatType);
        Assert.AreEqual(before,origin.SelectedAirportCode);Assert.AreEqual("CUN",Field<DestinationAutocomplete>(nav,"destinationSearch").SelectedAirportCode);
        nav.StartNewTrip();yield return null;var dialog=Object.FindAnyObjectByType<AppDialog>();
        Pointer(Button(dialog.transform,"Discard and start new trip"));yield return null;
        Assert.AreEqual("EWR",origin.SelectedAirportCode);Assert.AreEqual("Business",Field<FlightResultsPage>(nav,"flightResults").CabinClass);
        nav.OpenProfile();yield return null;Assert.AreEqual("Review Traveler",Field<TMP_InputField>(page,"nameInput").text);
    }
    [UnityTest,Timeout(45000)] public IEnumerator DetailsBottomAndSelectedButtonsWorkForNonstopAndConnections()
    {
        yield return Setup();var nav=Object.FindAnyObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");
        Field<DateSelectionPage>(nav,"datePage").GetType().GetMethod("RestoreDates",Private).Invoke(Field<DateSelectionPage>(nav,"datePage"),new object[]{(DateTime?)new DateTime(2027,5,14),(DateTime?)null,true});
        Pointer(Button(GameObject.Find("searchflights_page").transform,"Search Flights"));yield return null;
        var results=Field<FlightResultsPage>(nav,"flightResults");var flights=Field<List<FlightResultsPage.Flight>>(results,"flights");
        var selected=flights.First(f=>f.Stops==0);
        foreach(var flight in new[]{selected,flights.First(f=>f.Stops==2)})
        {
            var card=(RectTransform)flight.GetType().GetProperty("CardView",Private).GetValue(flight);
            Button(card,"Flight details").onClick.Invoke();yield return null;var scroll=Field<ScrollRect>(results,"detailsScroll");scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
            var note=scroll.content.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Illustrative details");var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,note.rectTransform);
            Assert.GreaterOrEqual(bounds.min.y,scroll.viewport.rect.yMin+30,"Bottom disclaimer must scroll fully into view");
            Button(Field<RectTransform>(results,"detailsOverlay"),"Close flight details").onClick.Invoke();
        }
        var chosen=(RectTransform)selected.GetType().GetProperty("CardView",Private).GetValue(selected);Button(chosen,"Select").onClick.Invoke();yield return null;
        Button(chosen,"Select").onClick.Invoke();yield return null;
        Button(Field<SeatSelectionPage>(nav,"seatPage").transform,"Back to flights").onClick.Invoke();yield return null;yield return null;
        Assert.IsTrue(results.gameObject.activeInHierarchy);
        foreach(var flight in flights)
        {
            var card=(RectTransform)flight.GetType().GetProperty("CardView",Private).GetValue(flight);var button=Button(card,"Select");
            Assert.AreEqual(flight==selected?"Selected":"Select",button.GetComponentInChildren<TMP_Text>().text);Assert.IsTrue(button.interactable);
            Assert.AreEqual(flight==selected?new Color32(47,80,247,255):new Color32(232,236,243,255),(Color32)button.targetGraphic.color);
        }
    }
    [UnityTest,Timeout(30000)] public IEnumerator TravelerInputCaretsUseLineMetricsAndPreserveTabFocus()
    {
        yield return Setup();var nav=Object.FindAnyObjectByType<SidebarNavigation>();var style=Field<DateSelectionPage>(nav,"datePage");
        var root=new GameObject("Caret verification",typeof(RectTransform));root.transform.SetParent(GameObject.Find("searchflights_page").transform,false);
        var form=root.AddComponent<TravelerDetailsForm>();form.Bind(style,PassengerParty.Adults(1),_=>{});yield return null;
        foreach(var input in root.GetComponentsInChildren<TMP_InputField>())
        {
            input.text="Alex";input.caretBlinkRate=0;KeyboardFocusNavigation.Instance.Focus(input);yield return null;yield return null;input.MoveTextEnd(false);input.ForceLabelUpdate();yield return null;Canvas.ForceUpdateCanvases();
            Assert.AreEqual(VerticalAlignmentOptions.Middle,input.textComponent.verticalAlignment,input.name);
            var vertices=(UIVertex[])typeof(TMP_InputField).GetField("m_CursorVerts",Private).GetValue(input);Assert.IsNotNull(vertices,input.name);
            var info=input.textComponent.textInfo.characterInfo[input.text.Length-1];Assert.AreEqual(info.descender,vertices[0].position.y,1f,input.name+" caret baseline");
        }
        KeyboardFocusNavigation.Instance.Focus(root.GetComponentsInChildren<TMP_InputField>().First());KeyboardFocusNavigation.Instance.Advance();Assert.IsNotNull(EventSystem.current.currentSelectedGameObject);
        Object.Destroy(root);
    }
}
