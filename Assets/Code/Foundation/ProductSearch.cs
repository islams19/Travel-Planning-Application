using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace TravelApp
{
    public sealed class ProductQuery
    {
        public string text = "";
        public ProductType? type;
        public float minPrice, maxPrice = float.MaxValue, minRating;
        public DateTime? start, end;
        public int sort;
        public static bool Date(string value, out DateTime date) => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        public static bool TryCreate(string text, int type, string min, string max, int rating, string start, string end, int sort, out ProductQuery query, out string error)
        {
            query = new ProductQuery { text = text ?? "", type = type == 0 ? (ProductType?)null : (ProductType)(type - 1), minRating = rating, sort = sort };
            error = "";
            if (!string.IsNullOrWhiteSpace(min) && !Price(min, out query.minPrice) ||
                !string.IsNullOrWhiteSpace(max) && !Price(max, out query.maxPrice)) error = "Enter valid non-negative prices (for example 125.50).";
            else if (query.minPrice > query.maxPrice) error = "Minimum price must not exceed maximum price.";
            else if (!string.IsNullOrWhiteSpace(start)) { if (Date(start.Trim(), out var d)) query.start = d; else error = "Start date must be a valid YYYY-MM-DD date."; }
            if (!string.IsNullOrWhiteSpace(end)) { if (Date(end.Trim(), out var d)) query.end = d; else error = "End date must be a valid YYYY-MM-DD date."; }
            if (query.start.HasValue && query.end.HasValue && query.start > query.end) error = "End date must be on or after start date.";
            return error.Length == 0;
        }
        static bool Price(string text, out float price) => float.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out price) && !float.IsNaN(price) && !float.IsInfinity(price) && price >= 0;
    }
    public static class ProductSearch
    {
        public static bool Covers(Product p, DateTime start, DateTime end) => p != null && start <= end && p.startDate.ToDateTime() != DateTime.MinValue && p.endDate.ToDateTime() != DateTime.MinValue && p.startDate.ToDateTime() <= start && p.endDate.ToDateTime() >= end;
        public static List<Product> Find(IEnumerable<Product> products, ProductQuery q)
        {
            string text = (q.text ?? "").Trim();
            var result = products.Where(p => p != null && p.available && (!q.type.HasValue || p.productType == q.type.Value) &&
                p.price >= q.minPrice && p.price <= q.maxPrice && p.rating >= q.minRating &&
                (text.Length == 0 || Match(p.productName, text) || Match(p.description, text) || Match(p.location, text) || Match(p.productType.ToString(), text)) &&
                (!q.start.HasValue && !q.end.HasValue || Covers(p, q.start ?? q.end.Value, q.end ?? q.start.Value)));
            switch(q.sort)
            {
                case 1: result = result.OrderByDescending(p => p.price); break;
                case 2: result = result.OrderBy(p => p.rating); break;
                case 3: result = result.OrderByDescending(p => p.rating); break;
                case 4: result = result.OrderByDescending(p => p.startDate.ToDateTime()); break;
                case 5: result = result.OrderBy(p => p.startDate.ToDateTime()); break;
                default: result = result.OrderBy(p => p.price); break;
            }
            return result.ToList();
        }
        static bool Match(string value, string text) => value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
