using System;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace TravelPlanning.Tracking
{
    public sealed class TrackingWatchRow
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public int LastPriceCents { get; set; }
        public bool IsActive { get; set; }
    }
}
