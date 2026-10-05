using System;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Build identity read by the isolated PowerShell validation harnesses.</summary>
    [Serializable]
    internal sealed class ReleaseValidationSettings
    {
        public string companyName;
        public string productName;
        public bool developmentBuild;
    }
}
