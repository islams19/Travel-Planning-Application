using System;
using TMPro;
using TravelApp.FlightMap;
using UnityEngine;
using static DateSelectionPage;

// Explicit, one-way handoff. Explore's own location state stays on its selector.
public sealed class ExploreBookingBridge : MonoBehaviour
{
    private FlightLocationSelector selector;
    private Func<string, TravelApp.Result> search;
    private UnityEngine.UI.Button button;
    private TMP_Text message;

    public void Build(DateSelectionPage style, FlightLocationSelector locationSelector,
        Func<string, TravelApp.Result> searchDestination)
    {
        selector = locationSelector;
        foreach(var dropdown in selector.GetComponentsInChildren<TMP_Dropdown>(true)) DropdownChevron.Attach(dropdown);
        search = searchDestination;
        PolishPanel();
        button = style.Control("Search Explore destination", selector.transform, 24, 646, 312, 68,
            "Search flights to\nthis destination", new Color32(47,80,247,255), Color.white, 18, 2);
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.disabledColor = new Color32(164,175,205,255);
        button.colors = colors;
        message = Text("Explore booking message", selector.transform, 24, 730, 312, 78,
            "", 17, new Color32(150,55,65,255), false, TextAlignmentOptions.TopLeft);
        message.textWrappingMode = TextWrappingModes.Normal;
        button.onClick.AddListener(Search);
        selector.SelectionChanged += SelectionChanged;
        Refresh();
    }

    private void PolishPanel()
    {
        void Place(string name,float y,float height)
        {
            var child=selector.transform.Find(name) as RectTransform;if(child==null)return;
            child.anchorMin=child.anchorMax=child.pivot=Vector2.up;child.anchoredPosition=new Vector2(24,-y);child.sizeDelta=new Vector2(312,height);
        }
        Place("Text_Choose locations",24,38);
        var title=selector.transform.Find("Text_Choose locations")?.GetComponent<TMP_Text>();
        if(title!=null){title.text="Plan a route";title.fontSize=26;title.fontStyle=FontStyles.Bold;}
        Place("Text_Departure search",88,26);Place("DepartureSearch",124,48);Place("DepartureDropdown",184,50);
        Place("Text_Destination search",260,26);Place("DestinationSearch",296,48);Place("DestinationDropdown",356,50);
        foreach(var text in selector.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.name=="Text_Departure search")text.text="From";
            if(text.name=="Text_Destination search")text.text="To";
            if(text.name=="DepartureInfo"||text.name=="DestinationInfo")text.gameObject.SetActive(false);
            if(text.name=="DistanceInfo"||text.name=="DurationInfo") {text.fontSize=19;text.alignment=TextAlignmentOptions.TopLeft;text.textWrappingMode=TextWrappingModes.Normal;}
        }
        Place("DistanceInfo",448,74);Place("DurationInfo",544,74);
        foreach(var input in selector.GetComponentsInChildren<TMP_InputField>(true))
        {
            if(input.placeholder is TMP_Text placeholder)placeholder.text="Search city or airport";
            input.textComponent.alignment=TextAlignmentOptions.Left;
            if(input.placeholder is TMP_Text hint)hint.alignment=TextAlignmentOptions.Left;
        }
    }

    private void OnEnable() { if (button != null) Refresh(); }
    private void SelectionChanged(FlightLocation departure, FlightLocation destination) => Refresh();
    private void Refresh()
    {
        button.interactable = selector.Destination != null;
        message.text = button.interactable ? "" : "Choose a destination airport to search flights.";
    }
    private void Search()
    {
        if (selector.Destination == null || !button.interactable) return;
        var result = search(selector.Destination.code);
        if (!result.success) message.text = result.message;
    }
    private void OnDestroy()
    {
        if (selector != null) selector.SelectionChanged -= SelectionChanged;
    }
}
