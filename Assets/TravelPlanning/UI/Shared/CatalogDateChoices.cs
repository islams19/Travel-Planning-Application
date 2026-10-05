using System;
using System.Collections.Generic;
using System.Globalization;

namespace TravelPlanning.UI.Shared
{
    /// <summary>Builds bounded date choices from the seeded catalog metadata.</summary>
    internal static class CatalogDateChoices
    {
        public static List<string> Create(string start, string end)
        {
            var first = DateTime.ParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var last = DateTime.ParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (last < first || (last - first).TotalDays > 366)
            {
                throw new InvalidOperationException("Catalog date range must fit within one year.");
            }

            var dates = new List<string>();
            for (var date = first; date <= last; date = date.AddDays(1))
            {
                dates.Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return dates;
        }
    }
}
