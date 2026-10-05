using System;
using System.Net.Mail;

namespace TravelPlanning.Accounts
{
    /// <summary>Shared form rules preserve password spaces and normalize only the email.</summary>
    internal static class AccountValidation
    {
#region Valid Password
        internal static bool ValidPassword(string password)
        {
            return !string.IsNullOrWhiteSpace(password) && password.Length >= 10 && password.Length <= 128;
        }
#endregion

#region Invalid Login
        internal static AccountResult InvalidLogin()
        {
            return new AccountResult(false, "Email or password is incorrect.");
        }
#endregion

#region Normalize Email
        internal static string NormalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('\r') >= 0 || email.IndexOf('\n') >= 0)
            {
                return null;
            }

            string value = email.Trim().ToLowerInvariant();
            if (value.Length > 254)
            {
                return null;
            }

            try
            {
                var parsed = new MailAddress(value);
                return parsed.Address == value && parsed.Host.Contains(".") ? value : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
#endregion
    }
}
