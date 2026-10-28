using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using TravelApp;
public class BookingManager : MonoBehaviour
{
    public static BookingManager Instance;
    [Header("Database")] [SerializeField] private ProductDatabase productDatabase;
    [Header("Search")] [SerializeField] private TMP_InputField searchInput;
    [Header("Filters")] [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private TMP_InputField minPriceInput, maxPriceInput;
    [SerializeField] private TMP_Dropdown minimumRatingDropdown;
    [Header("Date Filters")] [SerializeField] private TMP_InputField startDateInput, endDateInput;
    [Header("Sorting")] [SerializeField] private TMP_Dropdown sortDropdown;
    [Header("Results")] [SerializeField] private Transform resultsContainer;
    [SerializeField] private GameObject productResultPrefab;
    readonly List<ProductCardUI> cards = new List<ProductCardUI>();
    Coroutine pending;
    void Awake() { Instance = this; }
    void Start()
    {
        foreach (var input in Inputs()) if (input != null) input.onValueChanged.AddListener(ChangedText);
        foreach (var drop in Dropdowns()) if (drop != null) drop.onValueChanged.AddListener(ChangedDropdown);
        Search();
    }
    IEnumerable<TMP_InputField> Inputs() => new[] { searchInput, minPriceInput, maxPriceInput, startDateInput, endDateInput };
    IEnumerable<TMP_Dropdown> Dropdowns() => new[] { typeDropdown, minimumRatingDropdown, sortDropdown };
    void ChangedText(string _) { Queue(); }
    void ChangedDropdown(int _) { Queue(); }
    void Queue() { if (pending != null) StopCoroutine(pending); pending = StartCoroutine(DelayedSearch()); }
    IEnumerator DelayedSearch() { yield return new WaitForSecondsRealtime(.2f); pending = null; Search(); }
    static string Text(TMP_InputField input) => input != null ? input.text : "";
    static int Value(TMP_Dropdown drop) => drop != null ? drop.value : 0;
    public void Search()
    {
        if (!ProductQuery.TryCreate(Text(searchInput), Value(typeDropdown), Text(minPriceInput), Text(maxPriceInput), Value(minimumRatingDropdown), Text(startDateInput), Text(endDateInput), Value(sortDropdown), out var query, out var error))
        { TravelAppRuntime.Instance?.Tell(error); return; }
        var catalog = TravelAppRuntime.Instance?.Services?.Catalog;
        var products = catalog != null ? catalog.products : productDatabase != null ? productDatabase.products : new List<Product>();
        var found = ProductSearch.Find(products, query);
        if (resultsContainer == null || productResultPrefab == null) { TravelAppRuntime.Instance?.Tell("Results UI is not configured. Browse is also available from the Home screen."); return; }
        for (int i = 0; i < found.Count; i++)
        {
            if (i >= cards.Count)
            {
                var go = Instantiate(productResultPrefab, resultsContainer);
                var card = go.GetComponent<ProductCardUI>();
                if (card == null) { Destroy(go); Debug.LogError("Product result prefab requires ProductCardUI."); return; }
                cards.Add(card);
            }
            cards[i].gameObject.SetActive(true); cards[i].SetProduct(found[i]);
        }
        for (int i = found.Count; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
        TravelAppRuntime.Instance?.Tell(found.Count == 0 ? "No products match these filters." : found.Count + " products found.");
    }
    public void ResetFilters()
    {
        foreach (var input in Inputs()) if (input != null) input.SetTextWithoutNotify("");
        foreach (var drop in Dropdowns()) if (drop != null) drop.SetValueWithoutNotify(0);
        Search();
    }
    public void Select(Product product)
    {
        var screen = TravelScreenUI.Current;
        if (screen != null) screen.ShowProduct(product, Text(startDateInput), Text(endDateInput));
    }
    void OnDestroy()
    {
        foreach (var input in Inputs()) if (input != null) input.onValueChanged.RemoveListener(ChangedText);
        foreach (var drop in Dropdowns()) if (drop != null) drop.onValueChanged.RemoveListener(ChangedDropdown);
        if (Instance == this) Instance = null;
    }
}
