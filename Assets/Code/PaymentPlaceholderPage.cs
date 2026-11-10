using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

public sealed class PaymentPlaceholderPage : MonoBehaviour
{
    public PaymentBooking Booking { get; private set; }
    private TMP_Text details;

    public void Build(DateSelectionPage style, Action back)
    {
        var background = gameObject.AddComponent<Image>();
        background.color = new Color32(245, 247, 250, 255);
        background.raycastTarget = false;

        var panel = style.Panel("Payment handoff", transform, 60, 62, 1000, 292);
        var ink = new Color32(29, 40, 59, 255);
        var blue = new Color32(47, 80, 247, 255);
        Text("Placeholder title", panel, 30, 28, 930, 42, "Temporary payment placeholder",
            29, ink, true, TextAlignmentOptions.Left);
        Text("Handoff instructions", panel, 30, 85, 930, 36,
            "Payment integration handoff · Booking details are ready for the payment team.",
            20, ink, false, TextAlignmentOptions.Left);
        details = Text("Booking details", panel, 30, 135, 930, 62, "",
            20, ink, false, TextAlignmentOptions.TopLeft);
        style.Control("Back to Trip Review", panel, 30, 214, 244, 48,
            "Back to Trip Review", blue, Color.white, 19, 2).onClick.AddListener(() => back());
    }

    public void ShowBooking(PaymentBooking booking)
    {
        Booking = booking;
        details.text = $"{booking.Origin} → {booking.Destination}\n" +
            $"Estimated sample total: ${booking.EstimatedTotal:N0} {booking.Currency}";
    }
}
