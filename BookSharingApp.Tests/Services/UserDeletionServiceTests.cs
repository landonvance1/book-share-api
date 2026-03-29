using BookSharingApp.Common;
using BookSharingApp.Models;
using BookSharingApp.Services;
using BookSharingApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.Tests.Services
{
    public class UserDeletionServiceTests
    {
        public abstract class UserDeletionServiceTestBase : IDisposable
        {
            protected readonly Mock<IShareService> ShareServiceMock;
            protected readonly Mock<INotificationService> NotificationServiceMock;
            protected readonly Mock<ILogger<UserDeletionService>> LoggerMock;

            protected UserDeletionServiceTestBase()
            {
                ShareServiceMock = new Mock<IShareService>();
                NotificationServiceMock = new Mock<INotificationService>();
                LoggerMock = new Mock<ILogger<UserDeletionService>>();

                ShareServiceMock
                    .Setup(s => s.HandleUserBookDeletionAsync(It.IsAny<int>(), It.IsAny<string>()))
                    .ReturnsAsync(false);
            }

            public void Dispose() { }
        }

        public class DeleteUserAsyncTests : UserDeletionServiceTestBase
        {
            // ── User not found ────────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_WithNonExistentUser_ThrowsInvalidOperationException()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);

                var act = () => service.DeleteUserAsync("nonexistent-user");

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("User not found");
            }

            // ── PII scrub ─────────────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_ScrubsUserPIIInPlace()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1", firstName: "John", lastName: "Doe", email: "john@example.com");
                user.PhoneNumber = "555-1234";
                user.PasswordHash = "some-hash";
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var scrubbed = await context.Users.FindAsync("user-1");
                scrubbed!.Email.Should().Be("deleted-user-1@deleted.invalid");
                scrubbed.NormalizedEmail.Should().Be("DELETED-USER-1@DELETED.INVALID");
                scrubbed.UserName.Should().Be("deleted-user-1");
                scrubbed.NormalizedUserName.Should().Be("DELETED-USER-1");
                scrubbed.FirstName.Should().Be("[Deleted");
                scrubbed.LastName.Should().Be("User]");
                scrubbed.PhoneNumber.Should().BeNull();
                scrubbed.PasswordHash.Should().BeNull();
            }

            [Fact]
            public async Task DeleteUserAsync_PermanentlyLocksAccount()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var scrubbed = await context.Users.FindAsync("user-1");
                scrubbed!.LockoutEnabled.Should().BeTrue();
                scrubbed.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
            }

            [Fact]
            public async Task DeleteUserAsync_RotatesSecurityStamp()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var originalStamp = "original-stamp";
                user.SecurityStamp = originalStamp;
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var scrubbed = await context.Users.FindAsync("user-1");
                scrubbed!.SecurityStamp.Should().NotBe(originalStamp);
                scrubbed.SecurityStamp.Should().NotBeNullOrEmpty();
            }

            // ── Refresh tokens ────────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_DeletesAllRefreshTokensForUser()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-user");
                context.Users.AddRange(user, otherUser);
                await context.SaveChangesAsync();

                context.RefreshTokens.AddRange(
                    new RefreshToken { Token = "token-1", UserId = "user-1", ExpiresAt = DateTime.UtcNow.AddDays(7) },
                    new RefreshToken { Token = "token-2", UserId = "user-1", ExpiresAt = DateTime.UtcNow.AddDays(7) },
                    new RefreshToken { Token = "token-3", UserId = "other-user", ExpiresAt = DateTime.UtcNow.AddDays(7) }
                );
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var remaining = await context.RefreshTokens.ToListAsync();
                remaining.Should().HaveCount(1);
                remaining.Single().UserId.Should().Be("other-user");
            }

            // ── Notifications ─────────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_DeletesNotificationsForUser()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-user");
                context.Users.AddRange(user, otherUser);
                await context.SaveChangesAsync();

                context.Notifications.AddRange(
                    TestDataBuilder.CreateNotification(userId: "user-1", createdByUserId: "other-user"),
                    TestDataBuilder.CreateNotification(userId: "user-1", createdByUserId: "other-user"),
                    TestDataBuilder.CreateNotification(userId: "other-user", createdByUserId: "user-1")
                );
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var remaining = await context.Notifications.ToListAsync();
                remaining.Should().HaveCount(1);
                remaining.Single().UserId.Should().Be("other-user");
            }

            // ── ShareUserStates ───────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_DeletesShareUserStatesForUser()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-user");
                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                context.Users.AddRange(user, otherUser, lender);
                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(userId: "lender-1", bookId: book.Id, user: lender, book: book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share1 = TestDataBuilder.CreateShare(userBookId: userBook.Id, borrower: "user-1", userBook: userBook, borrowerUser: user);
                context.Shares.Add(share1);
                await context.SaveChangesAsync();

                var share2 = TestDataBuilder.CreateShare(userBookId: userBook.Id, borrower: "other-user", userBook: userBook, borrowerUser: otherUser);
                context.Shares.Add(share2);
                await context.SaveChangesAsync();

                context.ShareUserStates.Add(
                    TestDataBuilder.CreateShareUserState(shareId: share1.Id, userId: "user-1", isArchived: true, share: share1, user: user));
                await context.SaveChangesAsync();
                context.ShareUserStates.Add(
                    TestDataBuilder.CreateShareUserState(shareId: share2.Id, userId: "other-user", isArchived: true, share: share2, user: otherUser));
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var remaining = await context.ShareUserStates.ToListAsync();
                remaining.Should().HaveCount(1);
                remaining.Single().UserId.Should().Be("other-user");
            }

            // ── Chat messages ─────────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_ScrubsNonSystemChatMessageContent()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-user");
                context.Users.AddRange(user, otherUser);
                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ChatMessages.AddRange(
                    TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: "user-1", content: "Hello there"),
                    TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: "user-1", content: "Another message"),
                    TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: "other-user", content: "Other user message")
                );
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var userMessages = await context.ChatMessages
                    .Where(m => m.SenderId == "user-1")
                    .ToListAsync();
                userMessages.Should().AllSatisfy(m => m.Content.Should().Be("[deleted]"));

                var otherMessage = await context.ChatMessages
                    .FirstAsync(m => m.SenderId == "other-user");
                otherMessage.Content.Should().Be("Other user message");
            }

            [Fact]
            public async Task DeleteUserAsync_DoesNotScrubSystemMessages()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var systemMessage = TestDataBuilder.CreateChatMessage(
                    threadId: thread.Id,
                    senderId: "user-1",
                    content: "Status changed to Ready",
                    isSystemMessage: true);
                context.ChatMessages.Add(systemMessage);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var msg = await context.ChatMessages.FindAsync(systemMessage.Id);
                msg!.Content.Should().Be("Status changed to Ready");
            }

            // ── Borrower-side share handling ──────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_WithRequestedBorrowerShare_DeclinesShare()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                context.Users.AddRange(user, lender);
                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(userId: "lender-1", bookId: book.Id, user: lender, book: book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: "user-1",
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: user);
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare!.Status.Should().Be(ShareStatus.Declined);
            }

            [Fact]
            public async Task DeleteUserAsync_WithRequestedBorrowerShare_NotifiesLender()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                context.Users.AddRange(user, lender);
                var book = TestDataBuilder.CreateBook(title: "Dune");
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(userId: "lender-1", bookId: book.Id, user: lender, book: book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: "user-1",
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: user);
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                NotificationServiceMock.Verify(n => n.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    It.Is<string>(msg => msg.Contains("Dune")),
                    "user-1"),
                    Times.Once);
            }

            [Theory]
            [InlineData(ShareStatus.Ready)]
            [InlineData(ShareStatus.PickedUp)]
            [InlineData(ShareStatus.Returned)]
            public async Task DeleteUserAsync_WithActiveShareAsBorrower_DoesNotDeclineShare(ShareStatus activeStatus)
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                context.Users.AddRange(user, lender);
                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(userId: "lender-1", bookId: book.Id, user: lender, book: book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: "user-1",
                    status: activeStatus,
                    userBook: userBook,
                    borrowerUser: user);
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var unchanged = await context.Shares.FindAsync(share.Id);
                unchanged!.Status.Should().Be(activeStatus);
            }

            [Fact]
            public async Task DeleteUserAsync_WithDisputedRequestedShare_DoesNotDeclineShare()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                context.Users.AddRange(user, lender);
                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(userId: "lender-1", bookId: book.Id, user: lender, book: book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: "user-1",
                    status: ShareStatus.Requested,
                    isDisputed: true,
                    userBook: userBook,
                    borrowerUser: user);
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var unchanged = await context.Shares.FindAsync(share.Id);
                unchanged!.Status.Should().Be(ShareStatus.Requested);
            }

            // ── Lender-side user book handling ────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_WithUserBooks_SoftDeletesAllUserBooks()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                var book1 = TestDataBuilder.CreateBook(title: "Book One");
                var book2 = TestDataBuilder.CreateBook(title: "Book Two");
                context.Books.AddRange(book1, book2);
                await context.SaveChangesAsync();

                var userBook1 = TestDataBuilder.CreateUserBook(userId: "user-1", bookId: book1.Id, user: user, book: book1);
                var userBook2 = TestDataBuilder.CreateUserBook(userId: "user-1", bookId: book2.Id, user: user, book: book2);
                context.UserBooks.AddRange(userBook1, userBook2);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var userBooks = await context.UserBooks.Where(ub => ub.UserId == "user-1").ToListAsync();
                userBooks.Should().AllSatisfy(ub =>
                {
                    ub.IsDeleted.Should().BeTrue();
                    ub.DeletedAt.Should().NotBeNull();
                });
            }

            [Fact]
            public async Task DeleteUserAsync_WithUserBooks_CallsHandleUserBookDeletionForEach()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                var book1 = TestDataBuilder.CreateBook();
                var book2 = TestDataBuilder.CreateBook();
                context.Books.AddRange(book1, book2);
                await context.SaveChangesAsync();

                var userBook1 = TestDataBuilder.CreateUserBook(userId: "user-1", bookId: book1.Id, user: user, book: book1);
                var userBook2 = TestDataBuilder.CreateUserBook(userId: "user-1", bookId: book2.Id, user: user, book: book2);
                context.UserBooks.AddRange(userBook1, userBook2);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                ShareServiceMock.Verify(s => s.HandleUserBookDeletionAsync(It.IsAny<int>(), "user-1"), Times.Exactly(2));
            }

            [Fact]
            public async Task DeleteUserAsync_WithAlreadyDeletedUserBook_SkipsIt()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var alreadyDeleted = TestDataBuilder.CreateUserBook(userId: "user-1", bookId: book.Id, user: user, book: book, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));
                context.UserBooks.Add(alreadyDeleted);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                ShareServiceMock.Verify(s => s.HandleUserBookDeletionAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            }

            // ── Community handling ────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_RemovesUserFromAllCommunities()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-user");
                context.Users.AddRange(user, otherUser);
                var community1 = TestDataBuilder.CreateCommunity(createdBy: "other-user");
                var community2 = TestDataBuilder.CreateCommunity(createdBy: "other-user");
                context.Communities.AddRange(community1, community2);
                await context.SaveChangesAsync();

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community1.Id, "user-1"),
                    TestDataBuilder.CreateCommunityUser(community1.Id, "other-user"),
                    TestDataBuilder.CreateCommunityUser(community2.Id, "user-1"),
                    TestDataBuilder.CreateCommunityUser(community2.Id, "other-user")
                );
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var userMemberships = await context.CommunityUsers
                    .Where(cu => cu.UserId == "user-1")
                    .ToListAsync();
                userMemberships.Should().BeEmpty();
            }

            [Fact]
            public async Task DeleteUserAsync_WhenLastMemberOfCommunity_DeletesCommunity()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                var community = TestDataBuilder.CreateCommunity(createdBy: "user-1");
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                context.CommunityUsers.Add(TestDataBuilder.CreateCommunityUser(community.Id, "user-1"));
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var deletedCommunity = await context.Communities.FindAsync(community.Id);
                deletedCommunity.Should().BeNull();
            }

            [Fact]
            public async Task DeleteUserAsync_WhenNotLastMemberOfCommunity_KeepsCommunity()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-user");
                context.Users.AddRange(user, otherUser);
                var community = TestDataBuilder.CreateCommunity(createdBy: "other-user");
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, "user-1"),
                    TestDataBuilder.CreateCommunityUser(community.Id, "other-user")
                );
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                await service.DeleteUserAsync("user-1");

                var surviving = await context.Communities.FindAsync(community.Id);
                surviving.Should().NotBeNull();

                var remainingMembers = await context.CommunityUsers
                    .Where(cu => cu.CommunityId == community.Id)
                    .ToListAsync();
                remainingMembers.Should().HaveCount(1);
                remainingMembers.Single().UserId.Should().Be("other-user");
            }

            // ── Edge cases ────────────────────────────────────────────────────

            [Fact]
            public async Task DeleteUserAsync_WithNoAssociatedData_CompletesSuccessfully()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var service = new UserDeletionService(context, ShareServiceMock.Object, NotificationServiceMock.Object, LoggerMock.Object);
                var act = () => service.DeleteUserAsync("user-1");

                await act.Should().NotThrowAsync();
            }
        }
    }
}
