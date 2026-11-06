using TMPro;
using UnityEngine;

public sealed class SidebarNavigation : MonoBehaviour
{
    [SerializeField] private GameObject[] pages;
    [SerializeField] private UnityEngine.UI.Button[] buttons;
    [SerializeField] private TMP_Text headerTitle;
    [SerializeField] private TMP_Text headerSubtitle;
    [SerializeField] private DateSelectionPage datePage;
    private DestinationAutocomplete destinationSearch;

    private static readonly string[] Titles =
    {
        "Search flight destinations", "Explore World", "Booked Trips", "Settings"
    };

    private static readonly string[] Subtitles =
    {
        "LET'S FIND A TRIP", "DISCOVER DESTINATIONS", "YOUR TRAVEL PLANS", "YOUR PREFERENCES"
    };

    private void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int pageIndex = i;
            buttons[i].onClick.AddListener(() => ShowPage(pageIndex));
        }

        ShowPage(0);
    }

    private void ShowPage(int index)
    {
        if (datePage != null)
            datePage.gameObject.SetActive(false);

        for (int i = 0; i < pages.Length; i++)
            pages[i].SetActive(i == index);

        headerTitle.text = Titles[index];
        headerSubtitle.text = Subtitles[index];
    }

    private void Start()
    {
        var searchPage = pages[0];
        destinationSearch = searchPage.GetComponentInChildren<DestinationAutocomplete>(true);
        if (destinationSearch == null || datePage == null)
            return;

        datePage.Build();

        var continueButton = datePage.CreateContinueButton(searchPage.transform);
        continueButton.interactable = destinationSearch.CanContinue;
        destinationSearch.ContinuationChanged += canContinue => continueButton.interactable = canContinue;
        continueButton.onClick.AddListener(ShowDateSelection);
    }

    private void ShowDateSelection()
    {
        if (!destinationSearch.CanContinue)
            return;

        string destination = destinationSearch.SelectedDestination;
        foreach (var page in pages)
            page.SetActive(false);

        headerTitle.text = "Select dates for your trip";
        headerSubtitle.text = destination.ToUpperInvariant();
        datePage.SetDestination(destination);
        datePage.gameObject.SetActive(true);
    }
}
