using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Image))]
public sealed class DestinationAutocomplete : MonoBehaviour
{
    private const int MaxSuggestions = 8;
    private const float RowHeight = 64f;
    private const float PopupWidth = 640f;
    private int suggestionCount, highlighted = -1;
    private TMP_Text noResults, feedback;
    private string fieldName = "destination";
    private bool requireFeedback;
    private FormFieldFeedback fieldFeedback;
    private readonly TextMeshProUGUI[] airportNames = new TextMeshProUGUI[MaxSuggestions];
    private readonly TextMeshProUGUI[] airportCodes = new TextMeshProUGUI[MaxSuggestions];

    private string[] destinations;

    public string SelectedAirportCode { get; private set; }
    public string SelectedCityKey => TravelApp.FlightMap.TravelLocationCatalog.Find(SelectedAirportCode)?.BookingDestination;
    private Transform popupLayer;
    internal event Action SuggestionsOpened;
    private readonly TravelApp.FlightMap.FlightLocation[] suggestionAirports = new TravelApp.FlightMap.FlightLocation[MaxSuggestions];
    private readonly bool[] citySuggestions = new bool[MaxSuggestions];
    private static IEnumerable<TravelApp.FlightMap.FlightLocation> Airports => TravelApp.FlightMap.TravelLocationCatalog.Airports.Where(a=>a.BookingDestination!=null);

    public bool TrySelectAirportDestination(string airportCode)
    {
        var airport=Airports.FirstOrDefault(a=>string.Equals(a.code,airportCode,StringComparison.OrdinalIgnoreCase));
        if(airport==null) return false;
        bool alternate=Airports.First(a=>a.BookingDestination==airport.BookingDestination).code!=airport.code;
        SelectAirport(airport,alternate);
        return true;
    }

    private TMP_InputField input;
    private RectTransform results;
    private Outline selectedCardOutline;
    private string initialAirport;

    internal void ConfigureOrigin(string[] airports)
    {
        destinations = TravelApp.FlightMap.TravelLocationCatalog.BookingDestinations.ToArray();
        initialAirport = airports[0];
    }

    public string SelectedDestination { get; private set; }
    public bool CanContinue => SelectedDestination != null && SelectedAirportCode != null;
    public event Action<bool> ContinuationChanged;
    private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[MaxSuggestions];

    private void Awake() => EnsureInitialized();
    private void EnsureInitialized()
    {
        if(input!=null)return;
        if (initialAirport == null)
            destinations = TravelApp.FlightMap.TravelLocationCatalog.BookingDestinations.ToArray();
        var field = CreateRect("Destination input", transform, Vector2.zero, Vector2.one);
        // TMP creates its caret on enable, after the text component is assigned.
        field.gameObject.SetActive(false);
        var hitArea = field.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;

        var viewport = CreateRect("Text viewport", field, Vector2.zero, Vector2.one);
        // Keep scrolled input text inside the field padding.
        viewport.offsetMin = new Vector2(24, 17);
        viewport.offsetMax = new Vector2(-24, -7);
        viewport.gameObject.AddComponent<RectMask2D>();
        var value = CreateLabel("Text", viewport);
        var placeholder = CreateLabel("Placeholder", viewport);
        // Center the line using font metrics so different glyphs share a baseline.
        value.alignment = placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.text = initialAirport == null ? "Search destinations" : "Search origin airports";
        placeholder.color = new Color32(100, 106, 118, 255);

        input = field.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = hitArea;
        input.transition = Selectable.Transition.None;
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.customCaretColor = true;
        input.caretColor = new Color32(37, 99, 235, 255);
        input.caretBlinkRate = 0.85f;
        input.caretWidth = 3;
        input.restoreOriginalTextOnEscape = false;

        results = CreateRect("Destination suggestions", transform, Vector2.zero, Vector2.right);
        results.pivot = new Vector2(0.5f, 1);
        results.anchoredPosition = new Vector2(0, -4);
        for (int i = 0; i < labels.Length; i++)
        {
            int index=i;
            var row = CreateRect("Suggestion", results, Vector2.up, Vector2.one);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, RowHeight);
            row.anchoredPosition = new Vector2(0, -i * RowHeight);
            var background = row.gameObject.AddComponent<Image>();
            background.color = new Color32(242, 244, 248, 255);
            var button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;

            var label = CreateLabel("City", row);
            label.rectTransform.offsetMin = new Vector2(100, 29);
            label.rectTransform.offsetMax = new Vector2(-18, -4);
            label.fontSize=18;
            var detail=CreateLabel("Airport name",row);
            detail.rectTransform.offsetMin=new Vector2(100,4); detail.rectTransform.offsetMax=new Vector2(-18,-33);
            detail.fontSize=16; detail.color=new Color32(96,113,139,255);
            detail.overflowMode=TextOverflowModes.Ellipsis;
            airportNames[i]=detail;
            var code=CreateLabel("Airport code",row);
            code.rectTransform.anchorMin=code.rectTransform.anchorMax=Vector2.up;
            code.rectTransform.pivot=Vector2.up;code.rectTransform.anchoredPosition=new Vector2(14,0);code.rectTransform.sizeDelta=new Vector2(76,RowHeight);
            code.fontSize=19;code.fontStyle=FontStyles.Bold;code.color=new Color32(47,80,247,255);
            airportCodes[i]=code;
            button.transition=Selectable.Transition.None;
            var hover=row.gameObject.AddComponent<AirportSuggestionHover>();
            hover.Configure(()=>Highlight(index));
            labels[i] = label;
            button.onClick.AddListener(() => SelectAirport(suggestionAirports[index],!citySuggestions[index]));
        }

        noResults=CreateLabel("No airports found",results);
        noResults.rectTransform.offsetMin=new Vector2(20,0);noResults.rectTransform.offsetMax=new Vector2(-20,0);
        noResults.fontSize=19;noResults.text="No airports found. Try a city, airport name or code.";
        noResults.gameObject.SetActive(false);
        results.gameObject.AddComponent<Image>().color=Color.white;
        var edge=results.gameObject.AddComponent<Outline>(); edge.effectColor=new Color32(220,228,240,255); edge.effectDistance=new Vector2(1,-1);
        results.gameObject.SetActive(false);
        input.onValueChanged.AddListener(OnSearchTextChanged);
        input.onSelect.AddListener(UpdateSuggestions);
        input.onSubmit.AddListener(_=>AcceptHighlighted());
        input.onDeselect.AddListener(_=>RefreshFeedback());
        field.gameObject.SetActive(true);
        if (initialAirport == null)
            BindDestinationCards();
        else
            SelectDestination(initialAirport);
    }

    private void BindDestinationCards()
    {
        var scroll = transform.parent.GetComponentInChildren<ScrollRect>(true);
        if (scroll == null || scroll.content == null)
            return;

        foreach (Transform card in scroll.content)
        {
            var image = card.GetComponent<Image>();
            if (image == null || image.sprite == null || !image.sprite.name.StartsWith("card-", StringComparison.Ordinal))
                continue;

            string city = image.sprite.name.Substring("card-".Length);
            DestinationPhotos.Register(city == "Istabul" ? "Istanbul" : city, image.sprite);
            string Key(string value)=>new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            string cardKey=Key(city); if(cardKey=="istabul") cardKey="istanbul";
            string destination=destinations.FirstOrDefault(entry=>
                CultureInfo.InvariantCulture.CompareInfo.Compare(Key(entry.Split(',')[0]),cardKey,CompareOptions.IgnoreNonSpace)==0);
            if (destination == null)
                continue;

            var button = card.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
            }
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = input.caretColor;
            outline.effectDistance = new Vector2(2, 2);
            outline.enabled = false;
            button.onClick.AddListener(() => SelectDestination(destination, outline));
        }
    }

    internal string RawText => input.text;
    internal void RestoreSelection(string selected, string rawText, string airportCode = null)
    {
        var airport=Resolve(selected,airportCode);
        if(airport!=null)
            SelectAirport(airport,selected.EndsWith("("+airport.code+")",StringComparison.Ordinal));
        else
        {
            SelectedDestination=null; SelectedAirportCode=null;
            input.SetTextWithoutNotify(rawText ?? ""); SetCardOutline(null);
            results.gameObject.SetActive(false); RefreshFeedback(); ContinuationChanged?.Invoke(false);
        }
    }

    private static TravelApp.FlightMap.FlightLocation Resolve(string label,string code=null)
    {
        if(string.IsNullOrEmpty(label)) return null;
        return Airports.FirstOrDefault(a=>(string.IsNullOrEmpty(code) || a.code==code) &&
            (label==a.BookingDestination || label==a.BookingDestination+" ("+a.code+")"));
    }

    private void SelectAirport(TravelApp.FlightMap.FlightLocation airport,bool explicitAirport,Outline cardOutline=null)
    {
        if(airport==null) return;
        string label=airport.BookingDestination+((explicitAirport || initialAirport!=null) ? " ("+airport.code+")" : "");
        SelectedAirportCode=airport.code;
        ApplySelection(label,cardOutline);
    }

    private void SelectDestination(string destination, Outline cardOutline = null)
    {
        var airport=Resolve(destination);
        if(airport!=null) SelectAirport(airport,false,cardOutline);
    }

    private void ApplySelection(string destination, Outline cardOutline)
    {
        input.DeactivateInputField();
        input.SetTextWithoutNotify(destination);
        if (cardOutline != null || !string.Equals(SelectedDestination, destination, StringComparison.Ordinal))
            SetCardOutline(cardOutline);

        SelectedDestination = destination;
        input.textComponent.rectTransform.anchoredPosition = Vector2.zero;
        HideSuggestions();
        requireFeedback=false;RefreshFeedback();
        ContinuationChanged?.Invoke(CanContinue);
    }

    private void OnSearchTextChanged(string value)
    {
        if (SelectedDestination != null && !string.Equals(value, SelectedDestination, StringComparison.Ordinal))
        {
            SelectedDestination = null; SelectedAirportCode = null;
            SetCardOutline(null);
            ContinuationChanged?.Invoke(CanContinue);
        }

        RefreshFeedback();
        UpdateSuggestions(value);
    }

    private void SetCardOutline(Outline outline)
    {
        if (selectedCardOutline != null)
            selectedCardOutline.enabled = false;

        selectedCardOutline = outline;
        if (selectedCardOutline != null)
            selectedCardOutline.enabled = true;
    }

    private void OnDisable()
    {
        if (results != null)
            results.gameObject.SetActive(false);
    }

    internal void ConfigureCompact(Transform layer,string placeholderText)
    {
        EnsureInitialized();
        popupLayer=layer;
        input.textViewport.offsetMin=new Vector2(16,0); input.textViewport.offsetMax=new Vector2(-16,0);
        input.textComponent.fontSize=20; ((TMP_Text)input.placeholder).fontSize=20;
        input.textComponent.alignment=((TMP_Text)input.placeholder).alignment=TextAlignmentOptions.Left;
        ((TMP_Text)input.placeholder).text=placeholderText;
        fieldName=placeholderText.StartsWith("From",StringComparison.Ordinal)?"departure":placeholderText.StartsWith("Home",StringComparison.Ordinal)?"home":"destination";
        results.anchorMin=results.anchorMax=Vector2.zero;results.pivot=Vector2.up;
        results.anchoredPosition=new Vector2(0,-6);results.sizeDelta=new Vector2(PopupWidth,0);
        foreach(var label in labels) {label.fontSize=18; label.overflowMode=TextOverflowModes.Ellipsis;}
        fieldFeedback=FormFieldFeedback.Attach(input);
        feedback=fieldFeedback.Message;
        RefreshFeedback();
    }

    internal void HideSuggestions() { if(results!=null) results.gameObject.SetActive(false); highlighted=-1; }
    internal void FocusForCorrection()=>FormFieldFeedback.Focus(input);
    internal void ShowValidationError(string text) {requireFeedback=true;fieldFeedback.SetError(text);}
    internal bool ValidateAirport()
    {
        requireFeedback=true;RefreshFeedback();return CanContinue;
    }
    private void RefreshFeedback()
    {
        if(feedback==null)return;
        bool invalid=!CanContinue && (requireFeedback || !string.IsNullOrWhiteSpace(input.text));
        fieldFeedback.SetError(invalid?"Select a "+fieldName+" airport from the list.":null);
        fieldFeedback.SetBorder(invalid && (requireFeedback || !input.isFocused));
        feedback.color=requireFeedback || !input.isFocused ? new Color32(168,49,54,255):new Color32(96,113,139,255);
    }
    private void Update()
    {
        var keyboard=Keyboard.current;
        if(keyboard==null || input==null || EventSystem.current==null)return;
        if(keyboard.escapeKey.wasPressedThisFrame && results.gameObject.activeSelf) {HideSuggestions();return;}
        if(EventSystem.current.currentSelectedGameObject!=input.gameObject && !input.isFocused)return;
        if(keyboard.tabKey.wasPressedThisFrame) {HideSuggestions();return;}
        bool down=keyboard.downArrowKey.wasPressedThisFrame, up=keyboard.upArrowKey.wasPressedThisFrame;
        if(down || up)
        {
            if(!results.gameObject.activeSelf) UpdateSuggestions(input.text);
            if(suggestionCount>0) Highlight(highlighted<0?(down?0:suggestionCount-1):(highlighted+(down?1:-1)+suggestionCount)%suggestionCount);
        }
    }
    private void AcceptHighlighted()
    {
        if(results==null || !results.gameObject.activeSelf || suggestionCount==0)return;
        int index=highlighted<0?0:highlighted;
        SelectAirport(suggestionAirports[index],!citySuggestions[index]);
    }
    private void Highlight(int index)
    {
        highlighted=index;
        for(int i=0;i<suggestionCount;i++) labels[i].transform.parent.GetComponent<Image>().color=
            i==index?new Color32(218,228,255,255):citySuggestions[i]?new Color32(240,244,255,255):Color.white;
    }

    internal void DismissSuggestionsOutside(Vector2 point,Camera camera)
    {
        if(results!=null && results.gameObject.activeSelf &&
            !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,point,camera) &&
            !RectTransformUtility.RectangleContainsScreenPoint(results,point,camera)) HideSuggestions();
    }

    private void UpdateSuggestions(string value)
    {
        SuggestionsOpened?.Invoke();
        int count=0;
        var groups=Airports.GroupBy(a=>a.BookingDestination).Where(group=>group.Any(a=>MatchesAirport(a,value)))
            .OrderByDescending(group=>group.Any(a=>string.Equals(a.code,value.Trim(),StringComparison.OrdinalIgnoreCase)))
            .ThenBy(group=>group.Key,StringComparer.InvariantCultureIgnoreCase);
        foreach(var group in groups)
        {
            if(count>=labels.Length) break;
            BindSuggestion(count++,group.First(),true);
            foreach(var airport in group)
            {
                if(count>=labels.Length) break;
                BindSuggestion(count++,airport,false);
            }
        }
        for(int i=0;i<labels.Length;i++) labels[i].transform.parent.gameObject.SetActive(i<count);
        suggestionCount=count;highlighted=-1;
        noResults.gameObject.SetActive(count==0);
        results.sizeDelta=new Vector2(PopupWidth,Math.Max(1,count)*RowHeight);
        results.anchorMin=results.anchorMax=Vector2.zero;results.pivot=Vector2.up;
        PositionPopup();
        popupLayer?.SetAsLastSibling(); transform.SetAsLastSibling();
        results.gameObject.SetActive(true);
        for(int i=0;i<count;i++)
            if(!citySuggestions[i] && string.Equals(suggestionAirports[i].code,value.Trim(),StringComparison.OrdinalIgnoreCase)) { Highlight(i);break; }
    }

    private void BindSuggestion(int index,TravelApp.FlightMap.FlightLocation airport,bool city)
    {
        suggestionAirports[index]=airport; citySuggestions[index]=city;
        var label=labels[index];
        label.text=city ? airport.city+", "+airport.country : airport.airport;
        airportCodes[index].text=city?"CITY":airport.code;
        airportNames[index].text=city ? "Uses "+airport.code+" — "+airport.airport : airport.city+", "+airport.country;
        if(city) { label.rectTransform.offsetMin=new Vector2(100,29); label.rectTransform.offsetMax=new Vector2(-18,-4); }
        labels[index].transform.parent.name=city?"City "+airport.code:"Airport "+airport.code;
        label.fontStyle=city ? FontStyles.Bold : FontStyles.Normal;
        label.transform.parent.GetComponent<Image>().color=city ? new Color32(235,240,255,255) : Color.white;
    }

    private void PositionPopup()
    {
        results.anchoredPosition=new Vector2(0,feedback!=null && !string.IsNullOrEmpty(feedback.text)?-42:-6);
        var canvas=GetComponentInParent<Canvas>();
        if(canvas==null)return;
        var canvasRect=(RectTransform)canvas.transform;
        var corners=new Vector3[4];results.GetWorldCorners(corners);
        float right=canvasRect.InverseTransformPoint(corners[2]).x;
        float excess=right-(canvasRect.rect.xMax-20);
        if(excess>0)results.anchoredPosition+=new Vector2(-excess,0);
    }

    private static bool MatchesAirport(TravelApp.FlightMap.FlightLocation airport,string query)
    {
        string text=airport.BookingDestination+" "+airport.city+" "+airport.country+" "+airport.code+" "+airport.airport;
        var words=query.Replace("("," ").Replace(")"," ").Replace(","," ").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        return words.All(word=>CultureInfo.InvariantCulture.CompareInfo.IndexOf(text,word,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0);
    }

    private static TextMeshProUGUI CreateLabel(string labelName, Transform parent)
    {
        var rect = CreateRect(labelName, parent, Vector2.zero, Vector2.one);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 28;
        label.color = new Color32(18, 24, 39, 255);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}

// Hover updates the same highlighted option used by the keyboard; it owns no destination state.
public sealed class AirportSuggestionHover : MonoBehaviour, IPointerEnterHandler
{
    private Action entered;
    public void Configure(Action action) => entered=action;
    public void OnPointerEnter(PointerEventData data) => entered?.Invoke();
}
