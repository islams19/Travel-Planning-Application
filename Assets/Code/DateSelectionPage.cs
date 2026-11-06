using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DateSelectionPage : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29, 40, 59, 255);
    private static readonly Color Muted = new Color32(96, 113, 139, 255);
    private static readonly Color Blue = new Color32(47, 80, 247, 255);
    private static readonly Color PaleBlue = new Color32(235, 240, 255, 255);
    private static readonly Color Background = new Color32(245, 247, 250, 255);
    private static readonly Color Border = new Color32(220, 228, 240, 255);

    private Sprite rounded;
    private Texture2D roundedTexture;
    private TMP_Text destinationLabel;
    private TMP_Text monthLabel;
    private TMP_Text selectedDatesLabel;
    private DateTime displayedMonth;
    private DateTime? departureDate;
    private DateTime? returnDate;
    private readonly UnityEngine.UI.Button[] dayButtons = new UnityEngine.UI.Button[42];
    private readonly TMP_Text[] dayLabels = new TMP_Text[42];

    public void Build()
    {
        rounded = CreateRoundedSprite();
        var background = gameObject.AddComponent<Image>();
        background.color = Background;
        background.raycastTarget = false;

        var calendar = Panel("Calendar", transform, 60, 62, 856, 602);
        monthLabel = Text("Month", calendar, 112, 47, 632, 60, "", 28, Ink, true);
        Control("Previous month", calendar, 48, 48, 60, 60, "<", Background, Muted, 30)
            .onClick.AddListener(() => ChangeMonth(-1));
        Control("Next month", calendar, 748, 48, 60, 60, ">", Background, Muted, 30)
            .onClick.AddListener(() => ChangeMonth(1));

        string[] weekdays = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
        for (int i = 0; i < weekdays.Length; i++)
            Text(weekdays[i], calendar, 48 + i * 110, 132, 98, 49, weekdays[i], 18, Muted, true);

        for (int i = 0; i < dayButtons.Length; i++)
        {
            int cell = i;
            var button = Control("Day cell " + (i + 1), calendar, 48 + i % 7 * 110, 205,
                98, 60, "", Color.white, Ink, 22, 3);
            dayButtons[i] = button;
            dayLabels[i] = button.GetComponentInChildren<TMP_Text>();
            button.onClick.AddListener(() => SelectDate(displayedMonth.AddDays(cell - FirstDayOffset)));
        }

        var summary = Panel("Trip summary", transform, 964, 62, 512, 471);
        var marker = Shape("Destination marker", summary, 48, 48, 60, 60, PaleBlue);
        Text("Marker", marker, 0, 0, 60, 60, "•", 28, Blue, true);
        destinationLabel = Text("Destination", summary, 132, 48, 290, 34, "", 24, Ink, true, TextAlignmentOptions.Left);
        Text("Destination status", summary, 132, 81, 290, 30, "Destination selected", 20, Muted, false, TextAlignmentOptions.Left);
        var indicator = Place("Selected indicator", summary, 430, 62, 30, 30)
            .gameObject.AddComponent<RawImage>();
        indicator.texture = Resources.Load<Texture2D>("DateSelectionCheck");
        indicator.raycastTarget = false;
        var divider = Shape("Divider", summary, 48, 155, 416, 2, Border);
        divider.GetComponent<Image>().sprite = null;
        divider.GetComponent<Image>().type = Image.Type.Simple;

        Text("Selected trip", summary, 48, 195, 416, 40, "SELECTED TRIP", 18, Muted, true, TextAlignmentOptions.Left);
        selectedDatesLabel = Text("Selected dates", summary, 48, 247, 416, 45, "", 30, Ink, true, TextAlignmentOptions.Left);
        selectedDatesLabel.enableAutoSizing = true;
        selectedDatesLabel.fontSizeMin = 18;
        selectedDatesLabel.fontSizeMax = 30;
        Control("Confirm Dates", summary, 48, 339, 416, 84, "Confirm Dates", Blue, Color.white, 24, 0.8f);

        DateTime today = DateTime.Today;
        displayedMonth = new DateTime(today.Year, today.Month, 1);
        RefreshCalendar();
        RefreshSelectedDates();
    }

    private int FirstDayOffset => ((int)displayedMonth.DayOfWeek + 6) % 7;

    private void ChangeMonth(int amount)
    {
        displayedMonth = displayedMonth.AddMonths(amount);
        RefreshCalendar();
    }

    private void SelectDate(DateTime date)
    {
        if (!departureDate.HasValue || returnDate.HasValue || date <= departureDate.Value)
        {
            departureDate = date;
            returnDate = null;
        }
        else
        {
            returnDate = date;
        }

        RefreshCalendar();
        RefreshSelectedDates();
    }

    private void RefreshCalendar()
    {
        monthLabel.text = displayedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        int offset = FirstDayOffset;
        int days = DateTime.DaysInMonth(displayedMonth.Year, displayedMonth.Month);
        int rowSpacing = offset + days > 35 ? 66 : 72;
        for (int i = 0; i < dayButtons.Length; i++)
        {
            var button = dayButtons[i];
            int day = i - offset + 1;
            bool visible = day >= 1 && day <= days;
            button.gameObject.SetActive(visible);
            if (!visible)
                continue;

            DateTime date = displayedMonth.AddDays(day - 1);
            bool endpoint = date == departureDate || date == returnDate;
            bool inRange = departureDate.HasValue && returnDate.HasValue &&
                date > departureDate.Value && date < returnDate.Value;
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -205 - i / 7 * rowSpacing);
            button.targetGraphic.color = endpoint ? Blue : inRange ? PaleBlue : Color.white;
            dayLabels[i].text = day.ToString(CultureInfo.InvariantCulture);
            dayLabels[i].color = endpoint ? Color.white : inRange ? Blue : Ink;
        }
    }

    private void RefreshSelectedDates()
    {
        if (!departureDate.HasValue)
        {
            selectedDatesLabel.text = "Select a departure date";
            return;
        }

        const string format = "MMM d, yyyy";
        string departure = departureDate.Value.ToString(format, CultureInfo.InvariantCulture);
        selectedDatesLabel.text = returnDate.HasValue
            ? departure + " – " + returnDate.Value.ToString(format, CultureInfo.InvariantCulture)
            : departure + " – Select return";
    }

    public void SetDestination(string destination)
    {
        destinationLabel.text = destination;
    }

    public UnityEngine.UI.Button CreateContinueButton(Transform searchPage)
    {
        var button = Control("Continue to dates", searchPage, 0, 202, 164, 46,
            "Continue", Blue, Color.white, 20, 1.6f);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-28, -202);
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.disabledColor = new Color32(170, 180, 209, 255);
        button.colors = colors;
        return button;
    }

    private RectTransform Panel(string objectName, Transform parent, float x, float y, float width, float height)
    {
        var edge = Shape(objectName, parent, x, y, width, height, Border);
        return Shape("Surface", edge, 1.5f, 1.5f, width - 3, height - 3, Color.white);
    }

    private RectTransform Shape(string objectName, Transform parent, float x, float y, float width, float height,
        Color color, float cornerScale = 1)
    {
        var rect = Place(objectName, parent, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = cornerScale;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private UnityEngine.UI.Button Control(string objectName, Transform parent, float x, float y, float width, float height,
        string caption, Color fill, Color color, float fontSize, float cornerScale = 1)
    {
        var rect = Shape(objectName, parent, x, y, width, height, fill, cornerScale);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        Text("Label", rect, 0, 0, width, height, caption, fontSize, color, true);
        return button;
    }

    private static TMP_Text Text(string objectName, Transform parent, float x, float y, float width, float height,
        string value, float size, Color color, bool bold, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = Place(objectName, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = alignment;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Place(string objectName, Transform parent, float x, float y, float width, float height)
    {
        var rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private Sprite CreateRoundedSprite()
    {
        const int size = 68;
        const int radius = 33;
        roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        roundedTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(0, Mathf.Abs(x + 0.5f - size / 2f) - 1);
            float dy = Mathf.Max(0, Mathf.Abs(y + 0.5f - size / 2f) - 1);
            byte alpha = (byte)(255 * Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)));
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        roundedTexture.SetPixels32(pixels);
        roundedTexture.Apply(false, true);
        return Sprite.Create(roundedTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private void OnDestroy()
    {
        Destroy(rounded);
        Destroy(roundedTexture);
    }
}
