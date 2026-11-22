using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

public sealed class DemoBookingPages : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29, 40, 59, 255);
    private static readonly Color Muted = new Color32(96, 113, 139, 255);
    private static readonly Color Blue = new Color32(47, 80, 247, 255);
    private static readonly Color Pale = new Color32(235, 240, 255, 255);
    public IReadOnlyList<CompletedDemoBooking> Bookings => TravelApp.TravelAppRuntime.Instance.CompletedFlights;
    private DateSelectionPage style;
    private TMP_Text confirmationText;
    private TMP_Text confirmationTitle;

    public void BuildConfirmation(DateSelectionPage dateStyle, Action viewTrips)
    {
        style = dateStyle;
        Background();
        var panel = style.Panel("Booking receipt", transform, 60, 62, 1416, 708);
        confirmationTitle = Text("Confirmation title", panel, 36, 28, 1340, 48, "",
            30, Ink, true, TextAlignmentOptions.Left);
        confirmationText = Text("Booking details", panel, 36, 96, 1340, 484, "",
            22, Ink, false, TextAlignmentOptions.TopLeft);
        confirmationText.textWrappingMode = TextWrappingModes.Normal;
        ItineraryScroll.Wrap(confirmationText);
        style.Control("View Booked Trips", panel, 36, 610, 320, 60,
            "View My Trips", Blue, Color.white, 22).onClick.AddListener(() => viewTrips());
    }

    public void BuildBookedTrips(DateSelectionPage dateStyle, Action<CompletedDemoBooking> open,
        Action<TripDraft> resume, Action newTrip)
    {
        Background();
        gameObject.AddComponent<SavedTripsPage>().Build(dateStyle, resume, newTrip, open);
    }

    public CompletedDemoBooking Complete(PaymentBooking booking)
    {
        var record = TravelApp.TravelAppRuntime.Instance.CompleteFlightPayment(booking);
        RefreshHistory();
        return record;
    }

    public void ShowConfirmation(CompletedDemoBooking record)
    {
        var booking = record.Booking;
        confirmationTitle.text = "Booking confirmed · " + record.Reference;
        confirmationText.text = $"{booking.Origin} → {booking.Destination}\n" +
            $"{booking.PassengerCount} traveler(s) · {booking.CabinClass}\n\n" +
            CompletedDemoBooking.Itinerary(booking) + "\n\n" +
            $"Total: ${booking.EstimatedTotal:N0} {booking.Currency} · Payment completed";
        ItineraryScroll.Refresh(confirmationText);
    }

    private void RefreshHistory() => GetComponent<SavedTripsPage>()?.Refresh();

    private void Background()
    {
        var image = GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();
        image.color = new Color32(245, 247, 250, 255);
        image.raycastTarget = false;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
