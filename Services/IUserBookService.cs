using BookSharingApp.Models;

namespace BookSharingApp.Services
{
    public interface IUserBookService
    {
        // Reads
        Task<List<UserBook>> GetUserBooksAsync(string userId);
        Task<List<GroupedSearchBookResult>> SearchAccessibleBooksAsync(string userId, string? search);

        // Writes
        Task<UserBook> AddUserBookAsync(int bookId, string userId);

        /// <summary>
        /// Soft deletes a UserBook from a user's library.
        /// Returns a result indicating whether confirmation is needed (active shares exist).
        /// </summary>
        Task<DeleteUserBookResult> DeleteUserBookAsync(int userBookId, string userId, bool confirmed);
    }

    public class DeleteUserBookResult
    {
        public bool Success { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string? Message { get; set; }

        public static DeleteUserBookResult Deleted() => new() { Success = true };

        public static DeleteUserBookResult NeedsConfirmation(string bookTitle) => new()
        {
            Success = false,
            RequiresConfirmation = true,
            Message = $"'{bookTitle}' has an active share. The borrower will be notified that you have removed this book."
        };
    }
}
