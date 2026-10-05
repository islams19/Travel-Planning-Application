namespace TravelPlanning.Data
{
    /// <summary>Startup information returned to the diagnostic UI, not a Unity component.</summary>
    public sealed class TravelDatabaseStatus
    {
        public string DatabasePath { get; set; }
        public bool CopiedSeed { get; set; }
        public int SchemaVersion { get; set; }
        public string CatalogVersion { get; set; }
        public int Destinations { get; set; }
        public int Flights { get; set; }
        public int Hotels { get; set; }
        public int Restaurants { get; set; }
        public int Experiences { get; set; }
        public int Hotspots { get; set; }
        public int Reviews { get; set; }
        public int Users { get; set; }
        public int Trips { get; set; }
        public int WorkerThreadId { get; set; }
    }
}
