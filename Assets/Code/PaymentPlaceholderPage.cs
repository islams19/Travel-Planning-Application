using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

// Retains the existing handoff component so callers continue using the same booking snapshot.
public sealed class PaymentPlaceholderPage : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29, 40, 59, 255);
    private static readonly Color Muted = new Color32(96, 113, 139, 255);
    private static readonly Color Blue = new Color32(47, 80, 247, 255);
    private static readonly Color Pale = new Color32(235, 240, 255, 255);
    public PaymentBooking Booking { get; private set; }
    private PaymentBooking submittedBooking;
    private TMP_Text itinerary;
    private TMP_Text total;
    private TMP_Text error;
    private TMP_InputField nameInput;
    private TMP_InputField numberInput;
    private TMP_InputField expirationInput;
    private TMP_InputField cvvInput;
    private TMP_InputField postalInput;
    private UnityEngine.UI.Button pay;
    private Action<PaymentBooking> completed;

    public void Build(DateSelectionPage style, Action back, Action<PaymentBooking> onCompleted)
    {
        completed = onCompleted;
        var background = gameObject.AddComponent<Image>();
        background.color = new Color32(245, 247, 250, 255);
        background.raycastTarget = false;
        style.Control("Back to Trip Review", transform, 60, 26, 244, 44,
            "‹  Back to trip review", Pale, Blue, 19, 2).onClick.AddListener(() => back());
        Text("Payment step", transform, 1040, 26, 436, 44, "Flight  /  Seats  /  Payment",
            20, Blue, true, TextAlignmentOptions.Right);

        var form = style.Panel("Payment details", transform, 60, 94, 830, 704);
        Text("Title", form, 34, 24, 760, 44, "Payment details", 29, Ink, true, TextAlignmentOptions.Left);
        Icon(form, "Checkout lock", 602, 34, 24, PaymentIcon.Symbol.Lock, Blue);
        Text("Secure checkout", form, 635, 29, 160, 34,
            "Secure checkout", 18, Blue, true, TextAlignmentOptions.Left);
        style.Shape("Card method", form, 34, 128, 760, 57, Pale, 1.8f);
        Icon(form, "Card method icon", 52, 145, 26, PaymentIcon.Symbol.Card, Blue);
        Text("Card method label", form, 92, 133, 670, 44, "Credit or debit card",
            22, Blue, true, TextAlignmentOptions.Left);

        nameInput = Field(style, form, "Cardholder name", "Alex Example", 34, 212, 760, 80);
        numberInput = Field(style, form, "Card number", "0000 0000 0000 0000", 34, 322, 760, 23);
        Icon(numberInput.transform, "Card number icon", 16, 16, 26, PaymentIcon.Symbol.Card, Muted);
        numberInput.textViewport.offsetMin = new Vector2(54, 0);
        numberInput.onValueChanged.AddListener(value => FormatCardNumber());
        expirationInput = Field(style, form, "Expiration date", "MM/YY", 34, 432, 368, 5);
        cvvInput = Field(style, form, "Security code", "123", 426, 432, 368, 4);
        cvvInput.contentType = TMP_InputField.ContentType.Password;
        cvvInput.characterValidation = TMP_InputField.CharacterValidation.Integer;
        expirationInput.onValueChanged.AddListener(value => FormatExpiration());
        expirationInput.onSelect.AddListener(_ =>
        {
            if (!expirationInput.text.Any(char.IsDigit))
                expirationInput.caretPosition = 0;
        });
        expirationInput.SetTextWithoutNotify("  /");
        postalInput = Field(style, form, "ZIP / postal code", "10001", 34, 542, 760, 12);
        Text("Handling notice", form, 34, 647, 760, 30, "Card details are discarded after submission.",
            18, Muted, false, TextAlignmentOptions.Left);
        Icon(transform, "Protection shield", 166, 825, 25, PaymentIcon.Symbol.Shield, Muted);
        Text("Protection notice", transform, 204, 819, 670, 40,
            "Your payment information is encrypted and protected.", 18, Muted, false, TextAlignmentOptions.Left);

        var summary = style.Panel("Final itinerary", transform, 930, 94, 546, 704);
        Text("Summary title", summary, 30, 24, 480, 43, "Your trip", 28, Ink, true, TextAlignmentOptions.Left);
        itinerary = Text("Itinerary", summary, 30, 80, 480, 402, "", 20, Ink, false, TextAlignmentOptions.TopLeft);
        itinerary.textWrappingMode = TextWrappingModes.Normal;
        itinerary.enableAutoSizing = true;
        itinerary.fontSizeMin = 16;
        itinerary.fontSizeMax = 20;
        ItineraryScroll.Wrap(itinerary);
        total = Text("Total", summary, 30, 490, 480, 76, "", 24, Ink, true, TextAlignmentOptions.TopLeft);
        pay = style.Control("Submit mock payment", summary, 30, 582, 480, 60,
            "Complete booking", Blue, Color.white, 23);
        pay.onClick.AddListener(Submit);
        gameObject.AddComponent<KeyboardFocusOrder>().Configure(nameInput,numberInput,expirationInput,cvvInput,postalInput,pay);
        error = Text("Validation message", summary, 30, 650, 480, 44, "", 17,
            new Color32(168, 49, 54, 255), false, TextAlignmentOptions.TopLeft);
        error.textWrappingMode = TextWrappingModes.Normal;
        Icon(transform, "Cancellation check", 952, 825, 26, PaymentIcon.Symbol.Check,
            new Color32(25, 163, 112, 255));
        Text("Cancellation policy", transform, 994, 816, 480, 35,
            "Free cancellation within 24 hours", 20, Ink, true, TextAlignmentOptions.Left);
        Text("Cancellation disclaimer", transform, 994, 853, 480, 40,
            "Subject to the airline’s fare rules and eligibility.", 17, Muted, false, TextAlignmentOptions.Left);
    }

    public void ShowBooking(PaymentBooking booking)
    {
        Booking = booking;
        itinerary.text = $"{booking.PassengerCount} traveler(s) · {booking.CabinClass}\n\n" +
            CompletedDemoBooking.Itinerary(booking);
        ItineraryScroll.Refresh(itinerary);
        total.text = $"Estimated total\n${booking.EstimatedTotal:N0} {booking.Currency}";
        error.text = "";
        pay.interactable = !ReferenceEquals(submittedBooking, booking);
    }

    private void Submit()
    {
        if (Booking == null || ReferenceEquals(submittedBooking, Booking))
            return;
        error.text = MockPaymentValidator.Validate(nameInput.text, numberInput.text,
            expirationInput.text, cvvInput.text, postalInput.text, DateTime.Today) ?? "";
        if (error.text.Length > 0)
            return;
        var booking = Booking;
        submittedBooking = booking;
        pay.interactable = false;
        ClearFields();
        completed(booking);
    }

    private void ClearFields()
    {
        foreach (var field in new[] { nameInput, numberInput, expirationInput, cvvInput, postalInput })
            field.SetTextWithoutNotify("");
        expirationInput.SetTextWithoutNotify("  /");
    }

    private void FormatCardNumber()
    {
        int digitsBeforeCaret = numberInput.text.Take(numberInput.stringPosition).Count(char.IsDigit);
        string digits = new string(numberInput.text.Where(c => c >= '0' && c <= '9').Take(19).ToArray());
        string formatted = string.Join(" ", Enumerable.Range(0, (digits.Length + 3) / 4)
            .Select(group => digits.Substring(group * 4, Math.Min(4, digits.Length - group * 4))));
        numberInput.SetTextWithoutNotify(formatted);
        int caret = digitsBeforeCaret + Math.Max(0, (digitsBeforeCaret - 1) / 4);
        numberInput.stringPosition = Math.Min(caret, formatted.Length);
    }

    private void FormatExpiration()
    {
        int digitsBeforeCaret = expirationInput.text.Take(expirationInput.stringPosition).Count(char.IsDigit);
        string digits = new string(expirationInput.text.Where(c => c >= '0' && c <= '9').Take(4).ToArray());
        string month = digits.Substring(0, Math.Min(2, digits.Length)).PadRight(2);
        expirationInput.SetTextWithoutNotify(month + "/" + (digits.Length > 2 ? digits.Substring(2) : ""));
        expirationInput.stringPosition = digitsBeforeCaret < 2 ? digitsBeforeCaret : Math.Min(5, digitsBeforeCaret + 1);
    }

    private static void Icon(Transform parent, string name, float x, float y, float size,
        PaymentIcon.Symbol symbol, Color color)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(PaymentIcon)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(size, size);
        var icon = rect.GetComponent<PaymentIcon>();
        icon.Kind = symbol;
        icon.color = color;
        icon.raycastTarget = false;
    }

    private void OnDisable()
    {
        if (nameInput != null)
            ClearFields();
    }

    private static TMP_InputField Field(DateSelectionPage style, Transform parent, string caption,
        string placeholder, float x, float y, float width, int maxLength)
    {
        Text(caption + " label", parent, x, y, width, 30, caption, 20, Ink, true, TextAlignmentOptions.Left);
        var rect = style.Shape(caption, parent, x, y + 38, width, 58,
            new Color32(241, 244, 249, 255), 3);
        rect.gameObject.SetActive(false);
        var viewport = new GameObject("Text viewport", typeof(RectTransform), typeof(RectMask2D));
        var view = (RectTransform)viewport.transform;
        view.SetParent(rect, false);
        view.anchorMin = Vector2.zero;
        view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(16, 0);
        view.offsetMax = new Vector2(-16, 0);
        var text = Text("Text", view, 0, 0, 0, 0, "", 22, Ink, false, TextAlignmentOptions.Left);
        var hint = Text("Placeholder", view, 0, 0, 0, 0, placeholder, 22, Muted, false, TextAlignmentOptions.Left);
        foreach (var label in new[] { text, hint })
        {
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        }
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        var image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        input.targetGraphic = image;
        input.textViewport = view;
        input.textComponent = text;
        input.placeholder = hint;
        input.characterLimit = maxLength;
        input.customCaretColor = true;
        input.caretColor = Blue;
        rect.gameObject.SetActive(true);
        return input;
    }
}
