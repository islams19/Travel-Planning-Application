using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

public class BookingManager : MonoBehaviour
{
    public static BookingManager Instance;

    [Header("Database")]
    [SerializeField] private ProductDatabase productDatabase;

    [Header("Search")]
    [SerializeField] private TMP_InputField searchInput;

    [Header("Filters")]
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private TMP_InputField minPriceInput;
    [SerializeField] private TMP_InputField maxPriceInput;
    [SerializeField] private TMP_Dropdown minimumRatingDropdown;

    [Header("Date Filters")]
    [SerializeField] private TMP_InputField startDateInput;
    [SerializeField] private TMP_InputField endDateInput;

    [Header("Sorting")]
    [SerializeField] private TMP_Dropdown sortDropdown;

    [Header("Results")]
    [SerializeField] private Transform resultsContainer;
    [SerializeField] private GameObject productResultPrefab;


    private void Awake()
    {
        Instance = this;
    }


    private void Start()
    {
        Search();
    }


    public void Search()
    {
        List<Product> results =
            new List<Product>(productDatabase.products);

        results = ApplySearch(results);
        results = ApplyTypeFilter(results);
        results = ApplyPriceFilter(results);
        results = ApplyRatingFilter(results);
        results = ApplyDateFilter(results);
        results = ApplySorting(results);

        DisplayResults(results);
    }


    private List<Product> ApplySearch(List<Product> products)
    {
        string query = searchInput.text.Trim().ToLower();

        if (string.IsNullOrEmpty(query))
        {
            return products;
        }

        return products.Where(product =>
            product.productName.ToLower().Contains(query) ||
            product.description.ToLower().Contains(query) ||
            product.location.ToLower().Contains(query) ||
            product.productType.ToString().ToLower().Contains(query)
        ).ToList();
    }


    private List<Product> ApplyTypeFilter(List<Product> products)
    {
        // 0 = All
        // 1 = Hotel
        // 2 = Flight
        // 3 = Restaurant
        // 4 = Experience

        if (typeDropdown.value == 0)
        {
            return products;
        }

        ProductType selectedType =
            (ProductType)(typeDropdown.value - 1);

        return products.Where(product =>
            product.productType == selectedType
        ).ToList();
    }


    private List<Product> ApplyPriceFilter(List<Product> products)
    {
        float minPrice = 0;
        float maxPrice = float.MaxValue;

        if (!string.IsNullOrWhiteSpace(minPriceInput.text))
        {
            float.TryParse(minPriceInput.text, out minPrice);
        }

        if (!string.IsNullOrWhiteSpace(maxPriceInput.text))
        {
            float parsedMax;

            if (float.TryParse(maxPriceInput.text, out parsedMax))
            {
                maxPrice = parsedMax;
            }
        }

        return products.Where(product =>
            product.price >= minPrice &&
            product.price <= maxPrice
        ).ToList();
    }


    private List<Product> ApplyRatingFilter(List<Product> products)
    {
        if (minimumRatingDropdown.value == 0)
        {
            return products;
        }

        float minimumRating = minimumRatingDropdown.value;

        return products.Where(product =>
            product.rating >= minimumRating
        ).ToList();
    }


    private List<Product> ApplyDateFilter(List<Product> products)
    {
        DateTime? requestedStart =
            ParseDate(startDateInput.text);

        DateTime? requestedEnd =
            ParseDate(endDateInput.text);

        // No dates entered
        if (!requestedStart.HasValue &&
            !requestedEnd.HasValue)
        {
            return products;
        }

        return products.Where(product =>
        {
            DateTime productStart =
                product.startDate.ToDateTime();

            DateTime productEnd =
                product.endDate.ToDateTime();

            // Product has invalid dates
            if (productStart == DateTime.MinValue ||
                productEnd == DateTime.MinValue)
            {
                return false;
            }

            // User entered both start and end
            if (requestedStart.HasValue &&
                requestedEnd.HasValue)
            {
                return productStart <= requestedStart.Value &&
                       productEnd >= requestedEnd.Value;
            }

            // User only entered start date
            if (requestedStart.HasValue)
            {
                return productStart <= requestedStart.Value &&
                       productEnd >= requestedStart.Value;
            }

            // User only entered end date
            if (requestedEnd.HasValue)
            {
                return productStart <= requestedEnd.Value &&
                       productEnd >= requestedEnd.Value;
            }

            return true;

        }).ToList();
    }


    private DateTime? ParseDate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        DateTime date;

        bool validDate = DateTime.TryParseExact(
            input.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date
        );

        if (validDate)
        {
            return date;
        }

        Debug.LogWarning(
            "Invalid date: " + input +
            ". Please use YYYY-MM-DD."
        );

        return null;
    }


    private List<Product> ApplySorting(List<Product> products)
    {
        switch (sortDropdown.value)
        {
            // 0 = Price (Ascending)
            case 0:
                return products
                    .OrderBy(product => product.price)
                    .ToList();

            // 1 = Price (Descending)
            case 1:
                return products
                    .OrderByDescending(product => product.price)
                    .ToList();

            // 2 = Rating (Ascending)
            case 2:
                return products
                    .OrderBy(product => product.rating)
                    .ToList();

            // 3 = Rating (Descending)
            case 3:
                return products
                    .OrderByDescending(product => product.rating)
                    .ToList();

            // 4 = Date (Latest)
            case 4:
                return products
                    .OrderByDescending(product =>
                        product.startDate.ToDateTime())
                    .ToList();

            // 5 = Date (Closest)
            case 5:
                return products
                    .OrderBy(product =>
                        product.startDate.ToDateTime())
                    .ToList();

            default:
                return products;
        }
    }


    private void DisplayResults(List<Product> products)
    {
        // Delete old results
        foreach (Transform child in resultsContainer)
        {
            Destroy(child.gameObject);
        }

        // Create new results
        foreach (Product product in products)
        {
            GameObject result =
                Instantiate(
                    productResultPrefab,
                    resultsContainer
                );

            ProductCardUI card =
                result.GetComponent<ProductCardUI>();

            card.SetProduct(product);
        }
    }


    public void ResetFilters()
    {
        searchInput.text = "";

        typeDropdown.value = 0;

        minPriceInput.text = "";
        maxPriceInput.text = "";

        minimumRatingDropdown.value = 0;

        startDateInput.text = "";
        endDateInput.text = "";

        sortDropdown.value = 0;

        Search();
    }
}