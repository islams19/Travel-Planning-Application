using System;
using System.Threading;
using System.Threading.Tasks;

namespace TravelPlanning.Accounts
{
    internal sealed class RegisterAccountAction
    {
        private readonly AccountDatabase database;
        internal RegisterAccountAction(AccountDatabase database)
        {
            this.database = database;
        }

#region Register account
        internal async Task<AccountResult> ExecuteAsync(
            string email,
            string password,
            string confirmation,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string normalized = AccountValidation.NormalizeEmail(email);
            if (normalized == null)
            {
                return new AccountResult(false, "Enter a valid email address, such as name@example.com.");
            }

            if (!AccountValidation.ValidPassword(password))
            {
                return new AccountResult(false, "Use a password with 10 to 128 characters.");
            }

            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            {
                return new AccountResult(false, "The passwords do not match.");
            }

            // PBKDF2 stays on a worker, separate from the serialized database queue.
            var account = await Task.Run((
                ) => PasswordHasher.Create(normalized, password), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!await database.InsertAsync(account, token).ConfigureAwait(false))
            {
                return new AccountResult(false, "An account with this email already exists. Please log in.");
            }

            return new AccountResult(true, "Account created. You can now log in.", normalized, account.Id);
        }
#endregion
    }
}
