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
        button = style.Control("Search Explore destination", selector.transform, 20, 474, 320, 68,
            "Search flights to\nthis destination", new Color32(47,80,247,255), Color.white, 18, 2);
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.disabledColor = new Color32(164,175,205,255);
        button.colors = colors;
        message = Text("Explore booking message", selector.transform, 20, 556, 320, 78,
            "", 17, new Color32(150,55,65,255), false, TextAlignmentOptions.TopLeft);
        message.textWrappingMode = TextWrappingModes.Normal;
        button.onClick.AddListener(Search);
        selector.SelectionChanged += SelectionChanged;
        Refresh();
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
