namespace BookSharingApp.Services
{
    public interface IUserDeletionService
    {
        /// <summary>
        /// Permanently deletes a user account by scrubbing all PII in-place (tombstone pattern).
        /// Retains anonymized relational data for the other parties to shares and chat threads.
        /// </summary>
        Task DeleteUserAsync(string userId);
    }
}
