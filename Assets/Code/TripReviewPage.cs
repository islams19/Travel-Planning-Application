using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

public sealed class TripReviewPage : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29, 40, 59, 255);
    private static readonly Color Muted = new Color32(96, 113, 139, 255);
    private static readonly Color Blue = new Color32(47, 80, 247, 255);
    private static readonly Color PaleBlue = new Color32(235, 240, 255, 255);
    private RectTransform returnPanel;
    private TMP_Text departureDetails;
    private TMP_Text returnDetails;
    private TMP_Text total;
    private TMP_Text itinerary;

    public void Build(DateSelectionPage style, Action editRoute, Action editDates, Action editDeparture,
        Action editDepartureSeats, Action editReturn, Action editReturnSeats, Func<bool> continueToPayment)
    {
        var background = gameObject.AddComponent<Image>();
        background.color = new Color32(245, 247, 250, 255);
        background.raycastTarget = false;

        var trip = style.Panel("Itinerary", transform, 60, 44, 1416, 130);
        Text("Itinerary title", trip, 30, 17, 900, 37, "Itinerary", 26, Ink, true, TextAlignmentOptions.Left);
        itinerary = Text("Route and dates", trip, 30, 61, 1040, 55, "", 21, Muted, false, TextAlignmentOptions.TopLeft);
        EditButton(style, trip, 1090, "Edit route", editRoute);
        EditButton(style, trip, 1236, "Edit dates", editDates);

        var departure = style.Panel("Departure", transform, 60, 200, 910, 226);
        departureDetails = AddDetails(departure, "Departure flight");
        EditButton(style, departure, 592, "Edit flight", editDeparture);
        EditButton(style, departure, 738, "Edit seats", editDepartureSeats);
        returnPanel = style.Panel("Return", transform, 60, 452, 910, 226);
        returnDetails = AddDetails(returnPanel, "Return flight");
        EditButton(style, returnPanel, 592, "Edit flight", editReturn);
        EditButton(style, returnPanel, 738, "Edit seats", editReturnSeats);

        var summary = style.Panel("Trip total", transform, 1000, 200, 476, 320);
        Text("Title", summary, 30, 25, 410, 42, "Trip summary", 26, Ink, true, TextAlignmentOptions.Left);
        total = Text("Estimated total", summary, 30, 80, 410, 110, "", 22, Ink, false, TextAlignmentOptions.TopLeft);
        var error = Text("Booking validation", summary, 30, 252, 410, 51, "",
            17, new Color32(168, 49, 54, 255), false, TextAlignmentOptions.TopLeft);
        error.textWrappingMode = TextWrappingModes.Normal;
        style.Control("Continue to Payment", summary, 30, 196, 410, 48,
            "Continue to Payment", Blue, Color.white, 21, 2).onClick.AddListener(() =>
        {
            error.text = continueToPayment() ? "" : "Complete your flight and seat selections before continuing.";
        });
    }

    public void ShowBooking(SeatSelectionPage.Booking departure, SeatSelectionPage.Booking inbound)
    {
        itinerary.text = $"{departure.Origin} → {departure.Destination}\n" +
            $"Depart {FormatDate(departure.DepartureDate)}" +
            (inbound == null ? "  ·  One way" : $"  ·  Return {FormatDate(inbound.DepartureDate)}");
        departureDetails.text = Describe(departure);
        returnPanel.parent.gameObject.SetActive(inbound != null);
        if (inbound != null)
            returnDetails.text = Describe(inbound);

        int fare = departure.FarePerTraveler + (inbound?.FarePerTraveler ?? 0);
        string travelers = departure.PassengerCount == 1 ? "1 traveler" : $"{departure.PassengerCount} travelers";
        total.text = $"{travelers}  ·  {departure.Cabin}\n" +
            $"${fare:N0} per traveler\nEstimated total: ${fare * departure.PassengerCount:N0} USD\nSample fares and flights";
    }

    private static void EditButton(DateSelectionPage style, Transform panel, float x, string caption, Action action)
    {
        style.Control(caption, panel, x, 23, 132, 40, caption, PaleBlue, Blue, 18, 2)
            .onClick.AddListener(() => action());
    }

    private static TMP_Text AddDetails(Transform panel, string title)
    {
        Text("Leg title", panel, 30, 22, 840, 43, title, 27, Ink, true, TextAlignmentOptions.Left);
        var details = Text("Details", panel, 30, 76, 840, 126, "", 21, Muted, false, TextAlignmentOptions.TopLeft);
        details.textWrappingMode = TextWrappingModes.Normal;
        return details;
    }

    private static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private static string Describe(SeatSelectionPage.Booking leg)
    {
        return $"{leg.Origin} → {leg.Destination}  ·  {FormatDate(leg.DepartureDate)}\n" +
            $"{leg.Flight.Airline}  ·  {leg.Flight.Departure} – {leg.Flight.Arrival}\n" +
            $"{leg.Cabin}  ·  Seats: {string.Join(", ", leg.Seats)}  ·  ${leg.FarePerTraveler:N0} per traveler";
    }
}
