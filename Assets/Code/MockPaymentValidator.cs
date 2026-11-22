using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static class MockPaymentValidator
{
    public static string Validate(string name, string number, string expiration, string cvv, string postal, DateTime today)
    {
        number = number ?? "";
        expiration = expiration ?? "";
        cvv = cvv ?? "";
        postal = postal ?? "";
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2 || name.Length > 80 || !name.Any(char.IsLetter) || name.Any(char.IsControl))
            return "Enter a cardholder name.";
        string digits = number.Replace(" ", "").Replace("-", "");
        if (digits.Length < 13 || digits.Length > 19 || digits.Any(c => c < '0' || c > '9'))
            return "Enter a card number with 13–19 digits.";
        if (!Regex.IsMatch(expiration, @"^(0[1-9]|1[0-2])/[0-9]{2}$"))
            return "Use MM/YY for the expiration date.";
        int month = int.Parse(expiration.Substring(0, 2), CultureInfo.InvariantCulture);
        int year = 2000 + int.Parse(expiration.Substring(3, 2), CultureInfo.InvariantCulture);
        if (new DateTime(year, month, 1).AddMonths(1) <= today.Date)
            return "Use an expiration date in the current month or later.";
        if (!Regex.IsMatch(cvv, @"^[0-9]{3,4}$"))
            return "Enter a 3- or 4-digit security code.";
        if (!Regex.IsMatch(postal.Trim(), @"^[A-Za-z0-9][A-Za-z0-9 -]{1,10}[A-Za-z0-9]$"))
            return "Enter a ZIP/postal code with 3–12 letters or digits.";
        return null;
    }
}
