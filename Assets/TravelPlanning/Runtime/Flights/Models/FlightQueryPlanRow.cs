using System;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace TravelPlanning.Flights
{
    public sealed class FlightQueryPlanRow
    {
        [Column("detail")]
        public string Detail { get; set; }
    }
}
