using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Accounts
{
    /// <summary>Reads and writes accounts in the already initialized travel SQLite database.</summary>
    public sealed class AccountDatabase : DatabaseServiceBase
    {
        public AccountDatabase(TravelDatabase database) : base(database)
        {
        }

#region Find Async
        internal Task<AccountRecord> FindAsync(string email, CancellationToken token)
        {
            return base.ExecuteAsync(
                connection => connection.FindWithQuery<AccountRecord>(
                "SELECT id, email_normalized, password_hash, password_salt, password_iterations, " +
                "password_algorithm FROM users WHERE email_normalized = ?",
                email),
                token);
        }
#endregion

#region Insert Async
        internal Task<bool> InsertAsync(AccountRecord account, CancellationToken token)
        {
            return base.ExecuteAsync(connection =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    connection.Execute(
                        "INSERT INTO users (id, email_normalized, password_hash, password_salt, password_iterations, " +
                        "password_algorithm, created_utc) VALUES (?, ?, ?, ?, ?, ?, ?)",
                        account.Id,
                        account.Email,
                        account.Hash,
                        account.Salt,
                        account.Iterations,
                        account.Algorithm,
                        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                    return true;
                }
                catch (SQLiteException error)when (error.Result == SQLite3.Result.Constraint)
                {
                    // Only a duplicate email is a normal form error; other database problems propagate.
                    if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users WHERE email_normalized = ?", account.Email) > 0)
                        return false;
                    throw;
                }
            }, token);
        }
#endregion
    }
}
