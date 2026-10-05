using System;
using System.Globalization;

namespace TravelPlanning.Trips
{
    internal static class TripValidation
    {
#region Validate name
        internal static string CleanName(string name)
        {
            if (name == null)
                throw new ArgumentException("Enter a trip name with 1 to 60 characters.", nameof(name));
            foreach (char character in name)
            {
                if (char.IsControl(character))
                    throw new ArgumentException("Trip names cannot contain control characters.", nameof(name));
            }

            string trimmed = name.Trim();
            if (trimmed.Length < 1 || trimmed.Length > 60)
                throw new ArgumentException("Enter a trip name with 1 to 60 characters.", nameof(name));
            return trimmed;
        }

#endregion
#region Validate dates
        internal static void ValidateCatalogDates(string first, string last)
        {
            if (!ParseDate(first, out var start) ||
                !ParseDate(last, out var end) ||
                end < start ||
                (end - start).TotalDays +
                1 > 366)
                throw new InvalidOperationException("The catalog date range is missing or invalid.");
        }

        internal static void ValidateDates(string startDate, string endDate, string first, string last)
        {
            if (!ParseDate(startDate, out var start) || !ParseDate(endDate, out var end))
                throw new ArgumentException("Enter both trip dates as YYYY-MM-DD.");
            if (end < start)
                throw new ArgumentException("The end date must be on or after the start date.");
            ParseDate(first, out var minimum);
            ParseDate(last, out var maximum);
            if (start < minimum || end > maximum || (end - start).TotalDays + 1 > 366)
                throw new ArgumentException("Choose trip dates from " + first + " through " + last + ".");
        }

        private static bool ParseDate(string value, out DateTime parsed)
        {
            return DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed);
        }

#endregion
        internal static string Timestamp()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }
    }
}
