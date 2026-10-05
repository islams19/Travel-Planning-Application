using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Trips
{
    /// <summary>One saved-trip operation, executed through the shared database parent.</summary>
    internal sealed class CreateTripAction : UserDatabaseActionBase
    {
        private readonly TripRepository repository;
        internal CreateTripAction(
            TravelDatabase database,
            string userId,
            TripRepository repository) : base(database, userId, "Sign in before opening saved trips.")
        {
            this.repository = repository;
        }

#region Run action
        internal Task<TripSummary> ExecuteAsync(string name, string startDate, string endDate, CancellationToken token)
        {
            string cleanName = TripValidation.CleanName(name);
            return base.ExecuteAsync(connection =>
            {
                TripSummary created = null;
                connection.RunInTransaction(() =>
                {
                    RequireUser(connection);
                    repository.CatalogDates(connection, out string first, out string last);
                    TripValidation.ValidateDates(startDate, endDate, first, last);
                    token.ThrowIfCancellationRequested();
                    string now = TripValidation.Timestamp();
                    created = new TripSummary
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = cleanName,
                        StartDate = startDate,
                        EndDate = endDate,
                        CreatedUtc = now,
                        UpdatedUtc = now,
                        ItemCount = 0
                    };
                    repository.Insert(connection, created);
                    token.ThrowIfCancellationRequested();
                });
                // Once committed, return success even if cancellation arrives immediately afterward.
                return created;
            }, token);
        }
#endregion
    }
}
