using System;
using SQLite;

namespace TravelPlanning.Data
{
    /// <summary>Adds an immutable account identity to an action; SQL still checks ownership explicitly.</summary>
    internal abstract class UserDatabaseActionBase : DatabaseServiceBase
    {
        protected string UserId { get; }

        protected UserDatabaseActionBase(TravelDatabase database, string userId, string signInMessage) : base(database)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(signInMessage, nameof(userId));
            }

            UserId = userId;
        }

#region Validate account
        protected void RequireUser(SQLiteConnection connection)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users WHERE id=?", UserId) != 1)
            {
                throw new InvalidOperationException("Your account is not available. Sign in again.");
            }
        }
#endregion
    }
}
