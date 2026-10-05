namespace TravelPlanning.Accounts
{
    /// <summary>A safe result for the UI, containing no password or stored credentials.</summary>
    public sealed class AccountResult
    {
        public bool Success { get; }
        public string Message { get; }
        public string Email { get; }
        public string UserId { get; }

        internal AccountResult(bool success, string message, string email = null, string userId = null)
        {
            Success = success;
            Message = message;
            Email = email;
            UserId = userId;
        }
    }
}
