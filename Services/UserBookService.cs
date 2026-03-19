using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookSharingApp.Services
{
    public class UserBookService : IUserBookService
    {
        private readonly ApplicationDbContext _context;
        private readonly IShareService _shareService;
        private readonly ILogger<UserBookService> _logger;

        public UserBookService(
            ApplicationDbContext context,
            IShareService shareService,
            ILogger<UserBookService> logger)
        {
            _context = context;
            _shareService = shareService;
            _logger = logger;
        }

        public async Task<List<UserBook>> GetUserBooksAsync(string userId)
        {
            return await _context.UserBooks
                .Include(ub => ub.Book)
                .Where(ub => ub.UserId == userId && !ub.IsDeleted)
                .ToListAsync();
        }

        public async Task<List<GroupedSearchBookResult>> SearchAccessibleBooksAsync(string userId, string? search)
        {
            var rawResults = await _context.Database
                .SqlQueryRaw<SearchBookResult>(
                    "SELECT * FROM search_accessible_books({0}, {1})",
                    userId,
                    search ?? string.Empty)
                .ToListAsync();

            return rawResults
                .GroupBy(r => new { r.BookId, r.Title, r.Author })
                .Select(g => new GroupedSearchBookResult
                {
                    BookId = g.Key.BookId,
                    Title = g.Key.Title,
                    Author = g.Key.Author,
                    Owners = g.Select(r => new BookOwnerInfo
                    {
                        UserBookId = r.UserBookId,
                        OwnerUserId = r.OwnerUserId,
                        OwnerFirstName = r.OwnerFirstName,
                        Status = r.Status,
                        CommunityId = r.CommunityId,
                        CommunityName = r.CommunityName
                    }).ToList()
                })
                .ToList();
        }

        public async Task<UserBook> AddUserBookAsync(int bookId, string userId)
        {
            // Check if book exists
            var bookExists = await _context.Books.AnyAsync(b => b.Id == bookId);
            if (!bookExists)
                throw new InvalidOperationException("Book not found");

            // Check if user already has this book (including soft-deleted)
            var existingUserBook = await _context.UserBooks
                .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BookId == bookId);

            if (existingUserBook != null)
            {
                if (existingUserBook.IsDeleted)
                {
                    // Re-activate the soft-deleted book
                    existingUserBook.IsDeleted = false;
                    existingUserBook.DeletedAt = null;
                    existingUserBook.Status = UserBookStatus.Available;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Re-activated soft-deleted UserBook {UserBookId} for user {UserId}",
                        existingUserBook.Id, userId);

                    return existingUserBook;
                }
                throw new InvalidOperationException("User already has this book");
            }

            var userBook = new UserBook
            {
                UserId = userId,
                BookId = bookId,
                Status = UserBookStatus.Available
            };

            _context.UserBooks.Add(userBook);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created UserBook {UserBookId} for user {UserId} with book {BookId}",
                userBook.Id, userId, bookId);

            return userBook;
        }

        public async Task<UserBook> UpdateUserBookStatusAsync(int userBookId, UserBookStatus newStatus, string userId)
        {
            if (!Enum.IsDefined(typeof(UserBookStatus), newStatus))
                throw new ArgumentException("Invalid status value");

            var userBook = await _context.UserBooks.FindAsync(userBookId);

            if (userBook is null)
                throw new InvalidOperationException("UserBook not found");

            if (userBook.UserId != userId)
                throw new UnauthorizedAccessException("You do not own this book");

            if (userBook.IsDeleted)
                throw new InvalidOperationException("Cannot update status of a deleted book");

            userBook.Status = newStatus;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated UserBook {UserBookId} status to {Status} by user {UserId}",
                userBookId, newStatus, userId);

            return userBook;
        }

        public async Task<DeleteUserBookResult> DeleteUserBookAsync(int userBookId, string userId, bool confirmed)
        {
            var userBook = await _context.UserBooks
                .Include(ub => ub.Book)
                .FirstOrDefaultAsync(ub => ub.Id == userBookId);

            if (userBook is null)
                throw new InvalidOperationException("UserBook not found");

            if (userBook.UserId != userId)
                throw new UnauthorizedAccessException("You do not own this book");

            if (userBook.IsDeleted)
                throw new InvalidOperationException("Book has already been removed from your library");

            // Check for active shares (Ready, PickedUp, or Returned)
            var hasActiveShare = await _context.Shares
                .AnyAsync(s => s.UserBookId == userBookId &&
                               !s.IsDisputed &&
                               (s.Status == ShareStatus.Ready ||
                                s.Status == ShareStatus.PickedUp ||
                                s.Status == ShareStatus.Returned));

            if (hasActiveShare && !confirmed)
            {
                var bookTitle = userBook.Book?.Title ?? "This book";
                return DeleteUserBookResult.NeedsConfirmation(bookTitle);
            }

            // Handle existing shares (auto-decline requested, notify active, archive for lender)
            await _shareService.HandleUserBookDeletionAsync(userBookId, userId);

            // Soft delete the UserBook
            userBook.IsDeleted = true;
            userBook.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Soft-deleted UserBook {UserBookId} by user {UserId}",
                userBookId, userId);

            return DeleteUserBookResult.Deleted();
        }
    }
}
