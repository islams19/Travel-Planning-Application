using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class DestinationAutocomplete : MonoBehaviour
{
    private const int MaxSuggestions = 5;
    private const float RowHeight = 48f;

    [SerializeField] private string[] destinations =
    {
        "Amsterdam, Netherlands",
        "Athens, Greece",
        "Auckland, New Zealand",
        "Bali, Indonesia",
        "Bangkok, Thailand",
        "Barcelona, Spain",
        "Beijing, China",
        "Berlin, Germany",
        "Bogotá, Colombia",
        "Boston, Massachusetts",
        "Buenos Aires, Argentina",
        "Cairo, Egypt",
        "Cancún, Mexico",
        "Cape Town, South Africa",
        "Chicago, Illinois",
        "Copenhagen, Denmark",
        "Delhi, India",
        "Doha, Qatar",
        "Dubai, United Arab Emirates",
        "Dublin, Ireland",
        "Hong Kong",
        "Istanbul, Türkiye",
        "Jakarta, Indonesia",
        "Johannesburg, South Africa",
        "Kuala Lumpur, Malaysia",
        "Lagos, Nigeria",
        "Lima, Peru",
        "Lisbon, Portugal",
        "London, United Kingdom",
        "Los Angeles, California",
        "Madrid, Spain",
        "Manila, Philippines",
        "Marrakech, Morocco",
        "Melbourne, Australia",
        "Mexico City, Mexico",
        "Miami, Florida",
        "Milan, Italy",
        "Montreal, Canada",
        "Mumbai, India",
        "Nairobi, Kenya",
        "New York, New York",
        "Osaka, Japan",
        "Paris, France",
        "Prague, Czechia",
        "Rio de Janeiro, Brazil",
        "Rome, Italy",
        "San Francisco, California",
        "Santiago, Chile",
        "Santorini, Greece",
        "São Paulo, Brazil",
        "Seoul, South Korea",
        "Shanghai, China",
        "Singapore",
        "Stockholm, Sweden",
        "Sydney, Australia",
        "Taipei, Taiwan",
        "Tokyo, Japan",
        "Toronto, Canada",
        "Vancouver, Canada",
        "Vienna, Austria",
        "Warsaw, Poland",
        "Zurich, Switzerland"
    };

    private TMP_InputField input;
    private RectTransform results;
    private Outline selectedCardOutline;

    public string SelectedDestination { get; private set; }
    public bool CanContinue => SelectedDestination != null;
    public event Action<bool> ContinuationChanged;
    private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[MaxSuggestions];

    private void Awake()
    {
        var field = CreateRect("Destination input", transform, Vector2.zero, Vector2.one);
        // TMP creates its caret on enable, after the text component is assigned.
        field.gameObject.SetActive(false);
        var hitArea = field.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;

        var viewport = CreateRect("Text viewport", field, Vector2.zero, Vector2.one);
        // Keep scrolled input text out of the search icon area.
        viewport.offsetMin = new Vector2(85, 17);
        viewport.offsetMax = new Vector2(-24, -7);
        viewport.gameObject.AddComponent<RectMask2D>();
        var value = CreateLabel("Text", viewport);
        var placeholder = CreateLabel("Placeholder", viewport);
        // Center the line using font metrics so different glyphs share a baseline.
        value.alignment = placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.text = "Search destinations";
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

        results = CreateRect("Destination suggestions", transform, Vector2.zero, Vector2.right);
        results.pivot = new Vector2(0.5f, 1);
        results.anchoredPosition = new Vector2(0, -4);
        for (int i = 0; i < labels.Length; i++)
        {
            var row = CreateRect("Suggestion", results, Vector2.up, Vector2.one);
            row.pivot = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(0, RowHeight);
            row.anchoredPosition = new Vector2(0, -i * RowHeight);
            var background = row.gameObject.AddComponent<Image>();
            background.color = new Color32(242, 244, 248, 255);
            var button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;

            var label = CreateLabel("City", row);
            label.rectTransform.offsetMin = new Vector2(20, 0);
            label.rectTransform.offsetMax = new Vector2(-20, 0);
            labels[i] = label;
            button.onClick.AddListener(() => SelectDestination(label.text));
        }

        results.gameObject.SetActive(false);
        input.onValueChanged.AddListener(OnSearchTextChanged);
        input.onSelect.AddListener(UpdateSuggestions);
        field.gameObject.SetActive(true);
        BindDestinationCards();
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
            string destination = Array.Find(destinations, entry => entry != null &&
                CultureInfo.InvariantCulture.CompareInfo.Compare(
                    entry.Split(',')[0], city, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0);
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

    private void SelectDestination(string destination, Outline cardOutline = null)
    {
        input.DeactivateInputField();
        input.SetTextWithoutNotify(destination);
        if (cardOutline != null || !string.Equals(SelectedDestination, destination, StringComparison.Ordinal))
            SetCardOutline(cardOutline);

        SelectedDestination = destination;
        input.textComponent.rectTransform.anchoredPosition = Vector2.zero;
        results.gameObject.SetActive(false);
        ContinuationChanged?.Invoke(CanContinue);
    }

    private void OnSearchTextChanged(string value)
    {
        if (SelectedDestination != null && !string.Equals(value, SelectedDestination, StringComparison.Ordinal))
        {
            SelectedDestination = null;
            SetCardOutline(null);
            ContinuationChanged?.Invoke(CanContinue);
        }

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

    private void UpdateSuggestions(string value)
    {
        string prefix = value.Trim();
        int count = 0;
        if (prefix.Length > 0)
        {
            foreach (string destination in destinations)
            {
                if (string.IsNullOrWhiteSpace(destination) ||
                    !MatchesDestination(destination, prefix))
                    continue;

                labels[count++].text = destination;
                if (count == labels.Length)
                    break;
            }
        }

        for (int i = 0; i < labels.Length; i++)
            labels[i].transform.parent.gameObject.SetActive(i < count);

        results.sizeDelta = new Vector2(0, count * RowHeight);
        if (count > 0)
            transform.SetAsLastSibling();

        results.gameObject.SetActive(count > 0);
    }

    private static bool MatchesDestination(string destination, string prefix)
    {
        if (destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        int separator = destination.LastIndexOf(',');
        if (separator < 0)
            return false;

        for (int index = separator + 1; index < destination.Length; index++)
        {
            if (char.IsWhiteSpace(destination[index]) || !char.IsWhiteSpace(destination[index - 1]) && index != separator + 1)
                continue;

            if (destination.AsSpan(index).StartsWith(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
