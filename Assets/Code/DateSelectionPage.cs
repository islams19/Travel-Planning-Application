using System;
using System.Globalization;
using System.Linq;
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
    private TMP_Text secondMonthLabel;
    private TMP_Text selectionHint;
    private TMP_Text fareNote;
    private GameObject cheaperLegend;
    private Func<DateTime,int?> estimateDateFare;
    private TMP_Text selectedDatesLabel;
    private UnityEngine.UI.Button confirmButton;
    public event Action<DateTime, DateTime?> DatesConfirmed;
    private bool oneWay;
    private bool editingReturn;
    private bool editingDeparture;
    public event Action DatesChanged;
    private DateTime displayedMonth;
    private DateTime? departureDate;
    private DateTime? returnDate;
    private readonly UnityEngine.UI.Button[] dayButtons = new UnityEngine.UI.Button[84];
    private readonly TMP_Text[] dayLabels = new TMP_Text[84];

    private readonly TMP_Text[] fareLabels = new TMP_Text[84];
    private readonly DateTime?[] cellDates = new DateTime?[84];
    private readonly int?[] estimatedFares = new int?[84];
    private static readonly Color LowerFare = new Color32(22,128,87,255);
    private static readonly Color DisabledDay = new Color32(186,193,206,255);
    // The calendar's notion of "today". Tests can pin it; the app uses the device clock.
    internal static Func<DateTime> Today = () => DateTime.Today;
    private UnityEngine.UI.Button previousMonth;
    private static DateTime CurrentMonth { get { var today=Today(); return new DateTime(today.Year,today.Month,1); } }

    public void Build()
    {
        rounded = CreateRoundedSprite();
        var background = gameObject.AddComponent<Image>();
        background.color = Background;
        background.raycastTarget = false;

        var calendar = Panel("Calendar", transform, 60, 62, 1416, 640);
        selectionHint=Text("Calendar instruction",calendar,28,18,520,38,"Choose departure date",26,Ink,true,TextAlignmentOptions.Left);
        destinationLabel=Text("Destination",calendar,580,20,804,34,"",20,Muted,false,TextAlignmentOptions.Right);
        destinationLabel.overflowMode=TextOverflowModes.Ellipsis;
        monthLabel=Text("Month",calendar,84,72,540,46,"",25,Ink,true);
        secondMonthLabel=Text("Following month",calendar,792,72,540,46,"",25,Ink,true);
        previousMonth=Control("Previous month",calendar,28,72,46,46,"<",Background,Muted,26);
        previousMonth.onClick.AddListener(()=>ChangeMonth(-1));
        Control("Next month",calendar,1340,72,46,46,">",Background,Muted,26).onClick.AddListener(()=>ChangeMonth(1));
        var divider=Shape("Month divider",calendar,706,76,1,508,Border);
        divider.GetComponent<Image>().sprite=null;
        string[] weekdays={"Mo","Tu","We","Th","Fr","Sa","Su"};
        for(int month=0;month<2;month++)
        {
            float left=month==0 ? 36 : 744;
            for(int column=0;column<7;column++)
                Text("Weekday "+month+" "+column,calendar,left+column*92,132,84,34,weekdays[column],17,Muted,true);
            for(int cell=0;cell<42;cell++)
            {
                int index=month*42+cell;
                var button=Control("Day cell "+(index+1),calendar,left+cell%7*92,180+cell/7*70,84,64,"",Color.white,Ink,21,3);
                dayButtons[index]=button;
                dayLabels[index]=button.GetComponentInChildren<TMP_Text>();
                dayLabels[index].rectTransform.anchoredPosition=new Vector2(0,-4);
                dayLabels[index].rectTransform.sizeDelta=new Vector2(84,27);
                fareLabels[index]=Text("Date estimate",button.transform,0,32,84,24,"",15,Muted,false);
                button.onClick.AddListener(()=>{ if(cellDates[index].HasValue) SelectDate(cellDates[index].Value); });
            }
        }
        fareNote=Text("Date fare note",calendar,28,605,1070,26,"",15,Muted,false,TextAlignmentOptions.Left);
        cheaperLegend=Place("Lower fare legend",calendar,1120,605,264,26).gameObject;
        Shape("Lower fare key",cheaperLegend.transform,0,10,7,7,LowerFare);
        Text("Legend",cheaperLegend.transform,18,0,240,26,"Lower estimates",15,LowerFare,true,TextAlignmentOptions.Left);

        var summary=Panel("Trip summary",transform,60,722,1416,144);
        Text("Selected trip",summary,264,20,600,28,"SELECTED TRIP",17,Muted,true,TextAlignmentOptions.Left);
        selectedDatesLabel=Text("Selected dates",summary,264,56,600,44,"",25,Ink,true,TextAlignmentOptions.Left);
        selectedDatesLabel.enableAutoSizing=true; selectedDatesLabel.fontSizeMin=18; selectedDatesLabel.fontSizeMax=25;
        confirmButton=Control("Confirm Dates",summary,1104,56,284,56,"Done",Blue,Color.white,23,1.4f);
        confirmButton.onClick.AddListener(ConfirmDates);
        var tripType=Control("Trip type",summary,24,20,210,40,"Round trip",PaleBlue,Blue,18,2);
        DropdownChevron.Attach(tripType);
        tripType.onClick.AddListener(()=>SetOneWay(!oneWay));

        displayedMonth = CurrentMonth;
        RefreshCalendar();
        RefreshSelectedDates();
    }

    private static int FirstDayOffset(DateTime month) => ((int)month.DayOfWeek+6)%7;
    private bool ChoosingReturn => !oneWay && !editingDeparture && departureDate.HasValue && (editingReturn || !returnDate.HasValue);

    private void ChangeMonth(int amount)
    {
        int serial=(displayedMonth.Year-1)*12+displayedMonth.Month-1;
        serial=Mathf.Clamp(serial+amount,0,9999*12-2);
        var month=new DateTime(serial/12+1,serial%12+1,1);
        displayedMonth=month<CurrentMonth ? CurrentMonth : month;
        RefreshCalendar();
    }

    private void SelectDate(DateTime date)
    {
        date=date.Date;
        if(date<Today().Date) return;
        if(ChoosingReturn)
        {
            if(date<=departureDate.Value) return;
            returnDate=date;
        }
        else
        {
            departureDate=date; returnDate=null; editingReturn=false; editingDeparture=false;
        }
        RefreshCalendar(); RefreshSelectedDates();
    }

    internal void ConfigureDateEstimates(Func<DateTime,int?> provider)
    { estimateDateFare=provider; RefreshCalendar(); }
    internal void RefreshDateEstimates() => RefreshCalendar();

    private void RefreshCalendar()
    {
        monthLabel.text=displayedMonth.ToString("MMMM yyyy",CultureInfo.InvariantCulture);
        secondMonthLabel.text=displayedMonth.AddMonths(1).ToString("MMMM yyyy",CultureInfo.InvariantCulture);
        selectionHint.text=ChoosingReturn ? "Choose return date" : "Choose departure date";
        DateTime today=Today().Date;
        bool canGoBack=displayedMonth>CurrentMonth;
        previousMonth.interactable=canGoBack;
        previousMonth.GetComponentInChildren<TMP_Text>().color=canGoBack ? Muted : DisabledDay;
        for(int i=0;i<84;i++)
        {
            DateTime month=displayedMonth.AddMonths(i/42);
            int day=i%42-FirstDayOffset(month)+1;
            bool visible=day>=1 && day<=DateTime.DaysInMonth(month.Year,month.Month);
            cellDates[i]=visible ? month.AddDays(day-1) : (DateTime?)null;
            estimatedFares[i]=visible ? estimateDateFare?.Invoke(cellDates[i].Value) : null;
            dayButtons[i].gameObject.SetActive(visible);
        }
        var prices=estimatedFares.Where((price,index)=>price.HasValue && cellDates[index].HasValue &&
            cellDates[index]>=today && (!ChoosingReturn || cellDates[index]>departureDate)).Select(price=>price.Value).OrderBy(price=>price).ToArray();
        int threshold=prices.Length>0 ? prices[(prices.Length-1)/4] : 0;
        bool hasPrices=estimatedFares.Any(price=>price.HasValue);
        fareNote.text=hasPrices ? "Preliminary one-way estimates per adult · USD. Not live or guaranteed; flight-result prices may differ." :
            "Choose From and To to see estimated date fares.";
        cheaperLegend.SetActive(hasPrices);
        for(int i=0;i<84;i++)
        {
            if(!cellDates[i].HasValue) continue;
            DateTime date=cellDates[i].Value;
            bool endpoint=date==departureDate || date==returnDate;
            bool inRange=departureDate.HasValue && returnDate.HasValue && date>departureDate && date<returnDate;
            bool selectable=date>=today && (!ChoosingReturn || date>departureDate);
            dayButtons[i].interactable=selectable;
            dayButtons[i].targetGraphic.color=endpoint ? Blue : inRange ? PaleBlue : Color.white;
            dayLabels[i].text=date.Day.ToString(CultureInfo.InvariantCulture);
            dayLabels[i].color=endpoint ? Color.white : inRange ? Blue : selectable ? Ink : DisabledDay;
            fareLabels[i].text=estimatedFares[i].HasValue && (selectable || endpoint) ? $"${estimatedFares[i].Value:N0}" : "";
            bool cheaper=selectable && estimatedFares[i].HasValue && estimatedFares[i]<=threshold;
            fareLabels[i].color=endpoint ? Color.white : inRange ? Blue : cheaper ? LowerFare : Muted;
            fareLabels[i].fontStyle=cheaper ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private void RefreshSelectedDates()
    {
        DatesChanged?.Invoke();
        confirmButton.interactable = departureDate.HasValue && (oneWay || returnDate.HasValue && returnDate>departureDate);
        confirmButton.targetGraphic.color=confirmButton.interactable ? Blue : Border;
        confirmButton.GetComponentInChildren<TMP_Text>().color=confirmButton.interactable ? Color.white : Muted;
        if (!departureDate.HasValue)
        {
            selectedDatesLabel.text = "Select a departure date";
            return;
        }

        const string format = "MMM d, yyyy";
        string departure = departureDate.Value.ToString(format, CultureInfo.InvariantCulture);
        selectedDatesLabel.text = returnDate.HasValue
            ? departure + " – " + returnDate.Value.ToString(format, CultureInfo.InvariantCulture)
            : oneWay ? departure + " · One way" : departure + " – Select return";
    }

    private void ConfirmDates()
    {
        if (departureDate.HasValue && (oneWay || returnDate.HasValue && returnDate.Value > departureDate.Value))
            DatesConfirmed?.Invoke(departureDate.Value, returnDate);
    }

    internal DateTime? Departure => departureDate;
    internal DateTime? Return => returnDate;
    internal bool IsOneWay => oneWay;
    internal void RestoreDates(DateTime? departure, DateTime? returning, bool isOneWay)
    {
        editingReturn=false; editingDeparture=false;
        departureDate = departure;
        oneWay = isOneWay;
        returnDate = !isOneWay && departure.HasValue && returning>departure ? returning : null;
        var month = departure ?? Today();
        displayedMonth = new DateTime(month.Year, month.Month, 1);
        if (displayedMonth < CurrentMonth) displayedMonth = CurrentMonth;
        transform.Find("Trip summary/Surface/Trip type").GetComponentInChildren<TMP_Text>().text = oneWay ? "One way" : "Round trip";
        RefreshCalendar();
        RefreshSelectedDates();
    }

    internal void SetOneWay(bool value) => RestoreDates(departureDate,returnDate,value);
    internal void BeginDateEdit(bool returnField)
    {
        editingReturn=returnField && departureDate.HasValue && !oneWay;
        editingDeparture=!returnField;
        var date=departureDate ?? Today();
        var first=new DateTime(date.Year,date.Month,1);
        if(first<CurrentMonth) first=CurrentMonth;
        if(returnField && returnDate.HasValue && returnDate.Value>=first.AddMonths(2))
            first=new DateTime(returnDate.Value.Year,returnDate.Value.Month,1).AddMonths(-1);
        displayedMonth=first;
        RefreshCalendar();
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

    internal RectTransform Panel(string objectName, Transform parent, float x, float y, float width, float height)
    {
        var edge = Shape(objectName, parent, x, y, width, height, Border);
        return Shape("Surface", edge, 1.5f, 1.5f, width - 3, height - 3, Color.white);
    }

    internal RectTransform Shape(string objectName, Transform parent, float x, float y, float width, float height,
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

    internal UnityEngine.UI.Button Control(string objectName, Transform parent, float x, float y, float width, float height,
        string caption, Color fill, Color color, float fontSize, float cornerScale = 1)
    {
        var rect = Shape(objectName, parent, x, y, width, height, fill, cornerScale);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        var captionLabel=Text("Label", rect, 0, 0, width, height, caption, fontSize, color, true);
        captionLabel.margin=new Vector4(12,0,12,0);
        return button;
    }

    internal static TMP_Text Text(string objectName, Transform parent, float x, float y, float width, float height,
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
