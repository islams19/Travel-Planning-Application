using System.Threading;

namespace TravelPlanning.Accounts
{
    /// <summary>A lock protects the current identity from overlapping login and logout requests.</summary>
    internal sealed class AccountSession
    {
        private readonly object sessionLock = new object ();
        private long generation;
        private string email;
        private string userId;
        internal string Email
        {
            get
            {
                lock (sessionLock)
                {
                    return email;
                }
            }
        }

        internal string UserId
        {
            get
            {
                lock (sessionLock)
                {
                    return userId;
                }
            }
        }

#region Start login
        internal long BeginLogin()
        {
            lock (sessionLock)
            {
                email = null;
                userId = null;
                return ++generation;
            }
        }

#endregion
#region Finish login
        internal AccountResult CompleteLogin(
            long startedGeneration,
            AccountRecord account,
            bool matches,
            CancellationToken token)
        {
            lock (sessionLock)
            {
                token.ThrowIfCancellationRequested();
                // A logout or a newer login makes an older result ineligible to sign in.
                if (startedGeneration != generation || !matches)
                {
                    return AccountValidation.InvalidLogin();
                }

                email = account.Email;
                userId = account.Id;
                return new AccountResult(true, "You are logged in.", email, userId);
            }
        }

#endregion
#region Clear session
        internal void Logout()
        {
            lock (sessionLock)
            {
                ++generation;
                email = null;
                userId = null;
            }
        }
#endregion
    }
}
