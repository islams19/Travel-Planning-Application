using System;
using System.Threading;
using System.Threading.Tasks;
using SQLite;

namespace TravelPlanning.Data
{
    /// <summary>A shared parent that sends database work through TravelDatabase's worker-thread queue.</summary>
    public abstract class DatabaseServiceBase
    {
        private readonly TravelDatabase database;
        protected DatabaseServiceBase(TravelDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

#region Run database work
        protected Task<T> ExecuteAsync<T>(Func<SQLiteConnection, T> operation, CancellationToken cancellationToken)
        {
            return database.ExecuteAsync(operation, cancellationToken);
        }
#endregion
    }
}
