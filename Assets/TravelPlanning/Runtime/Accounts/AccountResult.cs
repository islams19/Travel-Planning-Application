namespace TravelPlanning.Accounts
{
    /// <summary>A receipt from registration/login: did it work, and what should the screen say?</summary>
    public sealed class AccountResult
    {
        public bool Success { get; }
        public string Message { get; }
        public string Email { get; }

        internal AccountResult(bool success, string message, string email = null)
        {
            Success = success;
            Message = message;
            Email = email;
        }
    }
}
