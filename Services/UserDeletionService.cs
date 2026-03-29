using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookSharingApp.Services
{
    public class UserDeletionService : IUserDeletionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IShareService _shareService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<UserDeletionService> _logger;

        public UserDeletionService(
            ApplicationDbContext context,
            IShareService shareService,
            INotificationService notificationService,
            ILogger<UserDeletionService> logger)
        {
            _context = context;
            _shareService = shareService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task DeleteUserAsync(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user is null)
                throw new InvalidOperationException("User not found");

            _logger.LogInformation("Starting account deletion for user {UserId}", userId);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Steps 1-4: Hard deletes and chat scrub — no interdependencies, batched into one save
                var refreshTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId)
                    .ToListAsync();
                _context.RefreshTokens.RemoveRange(refreshTokens);
                _logger.LogDebug("Queued deletion of {Count} refresh tokens for user {UserId}", refreshTokens.Count, userId);

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();
                _context.Notifications.RemoveRange(notifications);
                _logger.LogDebug("Queued deletion of {Count} notifications for user {UserId}", notifications.Count, userId);

                var shareUserStates = await _context.ShareUserStates
                    .Where(sus => sus.UserId == userId)
                    .ToListAsync();
                _context.ShareUserStates.RemoveRange(shareUserStates);
                _logger.LogDebug("Queued deletion of {Count} share user states for user {UserId}", shareUserStates.Count, userId);

                var chatMessages = await _context.ChatMessages
                    .Where(m => m.SenderId == userId && !m.IsSystemMessage)
                    .ToListAsync();
                foreach (var message in chatMessages)
                    message.Content = "[deleted]";
                _logger.LogDebug("Queued scrub of {Count} chat messages for user {UserId}", chatMessages.Count, userId);

                await _context.SaveChangesAsync();

                // Step 5: Auto-decline requested shares where this user is the borrower, notify lenders
                var requestedBorrowerShares = await _context.Shares
                    .Include(s => s.UserBook)
                        .ThenInclude(ub => ub.Book)
                    .Where(s => s.Borrower == userId &&
                                s.Status == ShareStatus.Requested &&
                                !s.IsDisputed)
                    .ToListAsync();

                foreach (var share in requestedBorrowerShares)
                    share.Status = ShareStatus.Declined;

                await _context.SaveChangesAsync();
                _logger.LogDebug("Auto-declined {Count} requested shares for user {UserId}", requestedBorrowerShares.Count, userId);

                foreach (var share in requestedBorrowerShares)
                {
                    try
                    {
                        var bookTitle = share.UserBook?.Book?.Title ?? "a book";
                        await _notificationService.CreateShareNotificationAsync(
                            share.Id,
                            NotificationType.ShareStatusChanged,
                            $"The borrower's request for '{bookTitle}' was cancelled because they deleted their account",
                            userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to notify lender of declined share {ShareId} during account deletion for user {UserId}", share.Id, userId);
                    }
                }

                // Step 6: Handle lender-side share cleanup for each of the user's books, then soft-delete them
                // Note: HandleUserBookDeletionAsync manages shares (auto-decline requested, notify active borrowers)
                // but does NOT soft-delete the UserBook itself — that's done here
                var userBooks = await _context.UserBooks
                    .Where(ub => ub.UserId == userId && !ub.IsDeleted)
                    .ToListAsync();

                foreach (var userBook in userBooks)
                {
                    await _shareService.HandleUserBookDeletionAsync(userBook.Id, userId);
                    userBook.IsDeleted = true;
                    userBook.DeletedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                _logger.LogDebug("Soft-deleted {Count} user books for user {UserId}", userBooks.Count, userId);

                // Step 7: Leave all communities (delete community if last member)
                var communityMemberships = await _context.CommunityUsers
                    .Where(cu => cu.UserId == userId)
                    .ToListAsync();

                foreach (var membership in communityMemberships)
                {
                    var memberCount = await _context.CommunityUsers
                        .CountAsync(cu => cu.CommunityId == membership.CommunityId);

                    _context.CommunityUsers.Remove(membership);

                    if (memberCount == 1)
                    {
                        var community = await _context.Communities.FindAsync(membership.CommunityId);
                        if (community is not null)
                            _context.Communities.Remove(community);
                    }
                }
                await _context.SaveChangesAsync();
                _logger.LogDebug("Removed user {UserId} from {Count} communities", userId, communityMemberships.Count);

                // Step 8: Scrub PII from the aspnetusers row in-place (tombstone)
                user.Email = $"deleted-{userId}@deleted.invalid";
                user.NormalizedEmail = $"DELETED-{userId.ToUpper()}@DELETED.INVALID";
                user.UserName = $"deleted-{userId}";
                user.NormalizedUserName = $"DELETED-{userId.ToUpper()}";
                user.FirstName = "[Deleted";
                user.LastName = "User]";
                user.PhoneNumber = null;
                user.PasswordHash = null;
                user.SecurityStamp = Guid.NewGuid().ToString();
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Successfully deleted account for user {UserId}", userId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
