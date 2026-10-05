namespace TravelPlanning.Data
{
    /// <summary>Plain data returned by the worker. It contains no Unity objects.</summary>
    public sealed class SqliteProofResult
    {
        public string DatabasePath { get; set; }
        public string Message { get; set; }
        public string CreatedUtc { get; set; }
        public string SqliteVersion { get; set; }
        public int Visits { get; set; }
        public bool WasAlreadySaved { get; set; }
        public int WorkerThreadId { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }
}
