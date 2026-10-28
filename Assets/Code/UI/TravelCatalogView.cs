using System.Collections;
using TMPro;
using UnityEngine;
namespace TravelApp
{
    public sealed class TravelCatalogView : MonoBehaviour
    {
        public TMP_InputField searchInput, minPriceInput, maxPriceInput, startDateInput, endDateInput;
        public TMP_Dropdown typeDropdown, ratingDropdown, sortDropdown;
        public TMP_Text resultCount;
        public TravelItemList results;
        TravelScreenUI screen;
        Coroutine pending;
        public void Bind(TravelScreenUI owner)
        {
            screen = owner;
            foreach (var input in Inputs()) input.onValueChanged.AddListener(ChangedText);
            foreach (var drop in Dropdowns()) drop.onValueChanged.AddListener(ChangedDropdown);
        }
        TMP_InputField[] Inputs() => new[] { searchInput, minPriceInput, maxPriceInput, startDateInput, endDateInput };
        TMP_Dropdown[] Dropdowns() => new[] { typeDropdown, ratingDropdown, sortDropdown };
        void ChangedText(string _) { Queue(); }
        void ChangedDropdown(int _) { Queue(); }
        void Queue()
        {
            if (!isActiveAndEnabled) return;
            if (pending != null) StopCoroutine(pending);
            pending = StartCoroutine(Delayed());
        }
        IEnumerator Delayed() { yield return new WaitForSecondsRealtime(.2f); pending = null; Search(); }
        public void Search()
        {
            if (screen == null) return;
            if (!ProductQuery.TryCreate(searchInput.text, typeDropdown.value, minPriceInput.text, maxPriceInput.text, ratingDropdown.value, startDateInput.text, endDateInput.text, sortDropdown.value, out var query, out var error))
            { screen.Report(Result.Fail(error)); return; }
            var products = ProductSearch.Find(screen.Services.Catalog.products, query);
            resultCount.text = products.Count + (products.Count == 1 ? " experience to explore" : " experiences to explore");
            results.Render(products.Count, (card, index) =>
            {
                var p = products[index];
                card.Set(p.productType.ToString().ToUpperInvariant() + "  /  " + p.location, p.productName, p.description, "$" + p.price.ToString("0.00") + "  / booking", p.productImage,
                    "Explore & book", () => screen.ShowProduct(p, startDateInput.text, endDateInput.text));
            });
        }
        public void Hotels() { typeDropdown.SetValueWithoutNotify(1); screen.Browse(); }
        public void Flights() { typeDropdown.SetValueWithoutNotify(2); screen.Browse(); }
        public void Dining() { typeDropdown.SetValueWithoutNotify(3); screen.Browse(); }
        public void Experiences() { typeDropdown.SetValueWithoutNotify(4); screen.Browse(); }
        public void ResetFilters()
        {
            foreach (var input in Inputs()) input.SetTextWithoutNotify("");
            foreach (var drop in Dropdowns()) drop.SetValueWithoutNotify(0);
            if (pending != null) { StopCoroutine(pending); pending = null; }
            Search();
        }
        void OnDisable() { if (pending != null) { StopCoroutine(pending); pending = null; } }
        void OnDestroy()
        {
            foreach (var input in Inputs()) if (input != null) input.onValueChanged.RemoveListener(ChangedText);
            foreach (var drop in Dropdowns()) if (drop != null) drop.onValueChanged.RemoveListener(ChangedDropdown);
        }
    }
}
