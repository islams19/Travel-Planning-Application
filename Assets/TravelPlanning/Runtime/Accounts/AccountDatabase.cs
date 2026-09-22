using System;
using System.IO;
using LiteDB;

namespace TravelPlanning.Accounts
{
    /// <summary>The filing cabinet: opens the local file and reads/writes account records.</summary>
    public sealed class AccountDatabase : IDisposable
    {
        private readonly LiteDatabase database;
        private readonly ILiteCollection<BsonDocument> accounts;

        public AccountDatabase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A database file path is required.", nameof(filePath));
            string fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            database = new LiteDatabase(new ConnectionString { Filename = fullPath });
            try
            {
                if (database.UserVersion > 2)
                    throw new InvalidDataException("This account database belongs to a newer application version.");
                accounts = database.GetCollection<BsonDocument>("accounts");
                // The database itself refuses duplicates, including simultaneous insert attempts.
                accounts.EnsureIndex("email", true);
                database.UserVersion = 2;
            }
            catch
            {
                database.Dispose();
                throw;
            }
        }

        internal bool Insert(string email, string password)
        {
            try
            {
                accounts.Insert(new BsonDocument
                {
                    ["_id"] = Guid.NewGuid(),
                    ["email"] = email,
                    ["password"] = password
                });
                return true;
            }
            catch (LiteException error) when (error.ErrorCode == LiteException.INDEX_DUPLICATE_KEY)
            {
                return false;
            }
        }

        internal BsonDocument Find(string email)
        {
            // Query values are passed as data, not joined into a database command.
            return accounts.FindOne(Query.EQ("email", email));
        }

        public void Dispose()
        {
            database.Dispose();
        }
    }
}
