using System;
using System.Threading;
using System.Threading.Tasks;

namespace TravelPlanning.Accounts
{
    /// <summary>The UI entry point. Each account action has one job and shares the same session.</summary>
    public sealed class AccountService
    {
        private readonly AccountSession session = new AccountSession();
        private readonly RegisterAccountAction register;
        private readonly LoginAccountAction login;
        public string SignedInEmail => session.Email;
        public string SignedInUserId => session.UserId;

        public AccountService(AccountDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            register = new RegisterAccountAction(database);
            login = new LoginAccountAction(database, session);
        }

#region Register account
        public Task<AccountResult> RegisterAsync(
            string email,
            string password,
            string confirmation,
            CancellationToken cancellationToken = default)
        {
            return register.ExecuteAsync(email, password, confirmation, cancellationToken);
        }

#endregion
#region Log in
        public Task<AccountResult> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            return login.ExecuteAsync(email, password, cancellationToken);
        }

#endregion
#region Log out
        public void Logout()
        {
            session.Logout();
        }
#endregion
    }
}
