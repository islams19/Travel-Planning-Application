using TMPro;
using UnityEngine;

public sealed class SidebarNavigation : MonoBehaviour
{
    [SerializeField] private GameObject[] pages;
    [SerializeField] private UnityEngine.UI.Button[] buttons;
    [SerializeField] private TMP_Text headerTitle;
    [SerializeField] private TMP_Text headerSubtitle;

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
        for (int i = 0; i < pages.Length; i++)
            pages[i].SetActive(i == index);

        headerTitle.text = Titles[index];
        headerSubtitle.text = Subtitles[index];
    }
}
