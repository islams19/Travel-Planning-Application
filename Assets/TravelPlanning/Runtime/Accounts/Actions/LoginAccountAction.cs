using System.Threading;
using System.Threading.Tasks;

namespace TravelPlanning.Accounts
{
    internal sealed class LoginAccountAction
    {
        private readonly AccountDatabase database;
        private readonly AccountSession session;
        internal LoginAccountAction(AccountDatabase database, AccountSession session)
        {
            this.database = database;
            this.session = session;
        }

#region Log in
        internal async Task<AccountResult> ExecuteAsync(string email, string password, CancellationToken token)
        {
            long generation = session.BeginLogin();
            token.ThrowIfCancellationRequested();
            string normalized = AccountValidation.NormalizeEmail(email);
            if (normalized == null || !AccountValidation.ValidPassword(password))
            {
                return AccountValidation.InvalidLogin();
            }

            var account = await database.FindAsync(normalized, token).ConfigureAwait(false);
            // Missing users still execute PasswordHasher's dummy PBKDF2 calculation.
            bool matches = await Task.Run(() => PasswordHasher.Verify(account, password), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return session.CompleteLogin(generation, account, matches, token);
        }
#endregion
    }
}
