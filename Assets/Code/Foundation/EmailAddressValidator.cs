using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TravelApp
{
    public static class EmailAddressValidator
    {
        // Offline IANA root-zone list. No email addresses are transmitted for validation.
        private static readonly HashSet<string> TopLevelDomains = LoadTopLevelDomains();
        private static readonly Regex LocalPart = new Regex(@"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+$");
        private static readonly Regex DomainLabel = new Regex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$");

        public static bool IsValid(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 120)
                return false;
            int at = email.IndexOf('@');
            if (at < 1 || at > 64 || at != email.LastIndexOf('@'))
                return false;
            string local = email.Substring(0, at);
            if (local.StartsWith(".") || local.EndsWith(".") || local.Contains("..") || !LocalPart.IsMatch(local))
                return false;
            string[] labels = email.Substring(at + 1).Split('.');
            if (labels.Length < 2 || !TopLevelDomains.Contains(labels[labels.Length - 1]))
                return false;
            foreach (string label in labels)
                if (!DomainLabel.IsMatch(label))
                    return false;
            return true;
        }

        private static HashSet<string> LoadTopLevelDomains()
        {
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var asset = Resources.Load<TextAsset>("EmailTopLevelDomains");
            if (asset == null)
                return domains;
            foreach (string line in asset.text.Split('\n'))
            {
                string domain = line.Trim();
                if (domain.Length > 0 && !domain.StartsWith("#"))
                    domains.Add(domain);
            }
            return domains;
        }
    }
}
