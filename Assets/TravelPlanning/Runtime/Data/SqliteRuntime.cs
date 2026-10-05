namespace TravelPlanning.Data
{
    /// <summary>Initializes the Windows SQLite provider once for all database services.</summary>
    public static class SqliteRuntime
    {
        private static readonly object sync = new object ();
        private static bool initialized;
#region Initialize
        public static void Initialize()
        {
            lock (sync)
            {
                if (initialized)
                    return;
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
                initialized = true;
            }
        }
#endregion
    }
}
