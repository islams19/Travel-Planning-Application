using System;
using System.Collections.Generic;
using System.Text;

namespace TravelPlanning.Reviews
{
    /// <summary>Checks a Google Maps search reference before the UI offers to open it. Never opens a browser.</summary>
    public static class GoogleMapsReference
    {
#region Try Get Url
        public static bool TryGetUrl(string raw, out string canonicalUrl)
        {
            canonicalUrl = null;
            if (string.IsNullOrWhiteSpace(raw) ||
                raw != raw.Trim() ||
                HasForbiddenCharacters(raw) ||
                raw.IndexOf('#') >= 0)
                return false;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                (uri.Host != "google.com" &&
                uri.Host != "www.google.com") ||
                uri.Port != 443 ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/maps/search/")
                return false;
            // Also reject an explicitly empty user-info section (https://@google.com/...).
            int authorityStart = raw.IndexOf("://", StringComparison.Ordinal);
            if (authorityStart < 0)
                return false;
            int authorityEnd = raw.IndexOf('/', authorityStart + 3);
            if (authorityEnd < 0 ||
                raw.Substring(authorityStart + 3, authorityEnd - authorityStart - 3).IndexOf('@') >= 0)
                return false;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            // Parse the original text: Uri may normalize malformed escapes or dot segments.
            int queryStart = raw.IndexOf('?', authorityEnd);
            if (queryStart < 0 || raw.Substring(authorityEnd, queryStart - authorityEnd) != "/maps/search/")
                return false;
            string query = raw.Substring(queryStart + 1);
            if (query.Length == 0)
                return false;
            foreach (string part in query.Split('&'))
            {
                int equals = part.IndexOf('=');
                if (equals <= 0 ||
                    !TryDecode(part.Substring(0, equals), out var key) ||
                    !TryDecode(part.Substring(equals + 1), out var value) ||
                    (key != "api" &&
                    key != "query") ||
                    values.ContainsKey(key))
                    return false;
                values.Add(key, value);
            }

            if (values.Count != 2 ||
                !values.TryGetValue("api", out var api) ||
                api != "1" ||
                !values.TryGetValue("query", out var place) ||
                string.IsNullOrWhiteSpace(place) ||
                HasForbiddenCharacters(place))
                return false;
            try
            {
                canonicalUrl = "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(place);
                return true;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }
#endregion

#region Has Forbidden Characters
        private static bool HasForbiddenCharacters(string value)
        {
            foreach (char character in value)
                if (char.IsControl(character) || character == '\\')
                    return true;
            return false;
        }
#endregion

#region Try Decode
        // Decode + as a space and reject malformed percent escapes or malformed UTF-8 bytes.
        private static bool TryDecode(string value, out string decoded)
        {
            decoded = null;
            var output = new StringBuilder();
            var utf8 = new UTF8Encoding(false, true);
            try
            {
                for (int index = 0; index < value.Length; index++)
                {
                    if (value[index] != '%')
                    {
                        output.Append(value[index] == '+' ? ' ' : value[index]);
                        continue;
                    }

                    var bytes = new List<byte>();
                    while (index < value.Length && value[index] == '%')
                    {
                        if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2]))
                            return false;
                        bytes.Add(Convert.ToByte(value.Substring(index + 1, 2), 16));
                        index += 3;
                    }

                    output.Append(utf8.GetString(bytes.ToArray()));
                    index--;
                }

                decoded = output.ToString();
                utf8.GetByteCount(decoded); // Reject an unpaired Unicode surrogate as well.
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }
#endregion

#region Is Hex
        private static bool IsHex(char value) => (value >= '0' &&
            value <= '9') ||
            (value >= 'a' &&
            value <= 'f') ||
            (value >= 'A' &&
            value <= 'F');
#endregion
    }
}
