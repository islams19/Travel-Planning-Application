using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Accounts
{
    // Public for sqlite-net's simple row mapping. Never send this credential record to the UI.
    public sealed class AccountRecord
    {
        [Column("id")]
        public string Id { get; set; }

        [Column("email_normalized")]
        public string Email { get; set; }

        [Column("password_hash")]
        public string Hash { get; set; }

        [Column("password_salt")]
        public string Salt { get; set; }

        [Column("password_iterations")]
        public int Iterations { get; set; }

        [Column("password_algorithm")]
        public string Algorithm { get; set; }
    }
}
