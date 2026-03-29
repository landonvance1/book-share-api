using BookSharingApp.Common;
using BookSharingApp.Models;

namespace BookSharingApp.Tests.Helpers
{
    public class TestDataBuilder
    {
        private static int _userIdCounter = 1;
        private static int _bookIdCounter = 1;
        private static int _userBookIdCounter = 1;
        private static int _communityIdCounter = 1;
        private static int _shareIdCounter = 1;
        private static int _shareUserStateIdCounter = 1;
        private static int _notificationIdCounter = 1;
        private static int _chatThreadIdCounter = 1;
        private static int _chatMessageIdCounter = 1;

        public static User CreateUser(
            string? id = null,
            string? firstName = null,
            string? lastName = null,
            string? email = null)
        {
            var userId = id ?? $"user-{_userIdCounter++}";
            return new User
            {
                Id = userId,
                FirstName = firstName ?? "Test",
                LastName = lastName ?? "User",
                Email = email ?? $"{userId}@test.com",
                UserName = email ?? $"{userId}@test.com",
                NormalizedUserName = (email ?? $"{userId}@test.com").ToUpper(),
                NormalizedEmail = (email ?? $"{userId}@test.com").ToUpper(),
                EmailConfirmed = true
            };
        }

        public static Book CreateBook(
            int? id = null,
            string? title = null,
            string? author = null)
        {
            var bookId = id ?? _bookIdCounter++;
            return new Book
            {
                Id = bookId,
                Title = title ?? $"Test Book {bookId}",
                Author = author ?? $"Test Author {bookId}"
            };
        }

        public static UserBook CreateUserBook(
            int? id = null,
            string? userId = null,
            int? bookId = null,
            bool isDeleted = false,
            DateTime? deletedAt = null,
            User? user = null,
            Book? book = null)
        {
            return new UserBook
            {
                Id = id ?? _userBookIdCounter++,
                UserId = userId ?? user?.Id ?? "default-user",
                BookId = bookId ?? book?.Id ?? 1,
                IsDeleted = isDeleted,
                DeletedAt = deletedAt,
                User = user ?? new User { Id = userId ?? "default-user" },
                Book = book ?? new Book { Id = bookId ?? 1, Title = "Default Book", Author = "Default Author" }
            };
        }

        public static Community CreateCommunity(
            int? id = null,
            string? name = null,
            string? createdBy = null,
            bool active = true)
        {
            var communityId = id ?? _communityIdCounter++;
            return new Community
            {
                Id = communityId,
                Name = name ?? $"Test Community {communityId}",
                CreatedBy = createdBy ?? "default-user",
                Active = active
            };
        }

        public static CommunityUser CreateCommunityUser(
            int communityId,
            string userId,
            bool isModerator = false)
        {
            return new CommunityUser
            {
                CommunityId = communityId,
                UserId = userId,
                IsModerator = isModerator
            };
        }

        public static Share CreateShare(
            int? id = null,
            int? userBookId = null,
            string? borrower = null,
            ShareStatus status = ShareStatus.Requested,
            DateTime? returnDate = null,
            bool isDisputed = false,
            string? disputedBy = null,
            UserBook? userBook = null,
            User? borrowerUser = null)
        {
            return new Share
            {
                Id = id ?? _shareIdCounter++,
                UserBookId = userBookId ?? userBook?.Id ?? 1,
                Borrower = borrower ?? borrowerUser?.Id ?? "default-borrower",
                Status = status,
                ReturnDate = returnDate,
                IsDisputed = isDisputed,
                DisputedBy = disputedBy,
                UserBook = userBook ?? new UserBook
                {
                    Id = userBookId ?? 1,
                    UserId = "default-lender",
                    BookId = 1,
                    User = new User { Id = "default-lender" },
                    Book = new Book { Id = 1, Title = "Default Book", Author = "Default Author" }
                },
                BorrowerUser = borrowerUser ?? new User { Id = borrower ?? "default-borrower" }
            };
        }

        public static ShareUserState CreateShareUserState(
            int? id = null,
            int shareId = 1,
            string? userId = null,
            bool isArchived = false,
            DateTime? archivedAt = null,
            Share? share = null,
            User? user = null)
        {
            return new ShareUserState
            {
                Id = id ?? _shareUserStateIdCounter++,
                ShareId = shareId,
                UserId = userId ?? "default-user",
                IsArchived = isArchived,
                ArchivedAt = archivedAt,
                Share = share ?? new Share { Id = shareId },
                User = user ?? new User { Id = userId ?? "default-user" }
            };
        }

        public static Notification CreateNotification(
            int? id = null,
            string? userId = null,
            string? notificationType = null,
            string? message = null,
            int? shareId = null,
            string? createdByUserId = null,
            DateTime? createdAt = null,
            DateTime? readAt = null)
        {
            var finalUserId = userId ?? "default-user";
            var finalCreatedByUserId = createdByUserId ?? "default-creator";

            return new Notification
            {
                Id = id ?? _notificationIdCounter++,
                UserId = finalUserId,
                NotificationType = notificationType ?? NotificationType.ShareStatusChanged,
                Message = message ?? "Test notification message",
                ShareId = shareId,
                CreatedByUserId = finalCreatedByUserId,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                ReadAt = readAt
            };
        }

        public static ChatThread CreateChatThread(
            int? id = null,
            DateTime? createdAt = null,
            DateTime? lastActivity = null)
        {
            return new ChatThread
            {
                Id = id ?? _chatThreadIdCounter++,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                LastActivity = lastActivity
            };
        }

        public static ShareChatThread CreateShareChatThread(
            int threadId,
            int shareId)
        {
            return new ShareChatThread
            {
                ThreadId = threadId,
                ShareId = shareId
            };
        }

        public static ChatMessage CreateChatMessage(
            int? id = null,
            int? threadId = null,
            string? senderId = null,
            string? content = null,
            DateTime? sentAt = null,
            bool isSystemMessage = false)
        {
            var finalSenderId = senderId ?? "default-sender";
            var finalThreadId = threadId ?? 1;

            return new ChatMessage
            {
                Id = id ?? _chatMessageIdCounter++,
                ThreadId = finalThreadId,
                SenderId = finalSenderId,
                Content = content ?? "Test message",
                SentAt = sentAt ?? DateTime.UtcNow,
                IsSystemMessage = isSystemMessage
            };
        }

        public static void ResetCounters()
        {
            _userIdCounter = 1;
            _bookIdCounter = 1;
            _userBookIdCounter = 1;
            _communityIdCounter = 1;
            _shareIdCounter = 1;
            _shareUserStateIdCounter = 1;
            _notificationIdCounter = 1;
            _chatThreadIdCounter = 1;
            _chatMessageIdCounter = 1;
        }
    }
}
