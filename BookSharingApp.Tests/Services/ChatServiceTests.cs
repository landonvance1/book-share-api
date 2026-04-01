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
    public class ChatServiceTests
    {
        // Shared base class for all nested test classes
        public abstract class ChatServiceTestBase : IDisposable
        {
            protected readonly Mock<ILogger<ChatService>> LoggerMock;
            protected readonly Mock<INotificationService> NotificationServiceMock;

            protected ChatServiceTestBase()
            {
                LoggerMock = new Mock<ILogger<ChatService>>();
                NotificationServiceMock = new Mock<INotificationService>();
            }

            public void Dispose()
            {
                // Cleanup if needed
            }
        }

        public class CreateShareChatAsyncTests : ChatServiceTestBase
        {
            [Fact]
            public async Task CreateShareChatAsync_WithNewShare_CreatesThreadAndShareChatThread()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await chatService.CreateShareChatAsync(share.Id);

                // Assert
                var shareChatThread = await context.ShareChatThreads
                    .Include(sct => sct.Thread)
                    .FirstOrDefaultAsync(sct => sct.ShareId == share.Id);

                shareChatThread.Should().NotBeNull();
                shareChatThread!.ShareId.Should().Be(share.Id);
                shareChatThread.Thread.Should().NotBeNull();
                shareChatThread.Thread.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
                shareChatThread.Thread.LastActivity.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            }

            [Fact]
            public async Task CreateShareChatAsync_WhenChatAlreadyExists_DoesNotCreateDuplicate()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Create existing chat thread
                var existingThread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(existingThread);
                await context.SaveChangesAsync();

                var existingShareChatThread = TestDataBuilder.CreateShareChatThread(
                    threadId: existingThread.Id,
                    shareId: share.Id
                );
                context.ShareChatThreads.Add(existingShareChatThread);
                await context.SaveChangesAsync();

                var initialThreadCount = await context.ChatThreads.CountAsync();

                // Act
                await chatService.CreateShareChatAsync(share.Id);

                // Assert
                var finalThreadCount = await context.ChatThreads.CountAsync();
                finalThreadCount.Should().Be(initialThreadCount, "no new thread should be created for duplicate");

                var shareChatThreadCount = await context.ShareChatThreads
                    .CountAsync(sct => sct.ShareId == share.Id);
                shareChatThreadCount.Should().Be(1, "only one ShareChatThread should exist per share");
            }

            [Fact]
            public async Task CreateShareChatAsync_WithNewShare_CreatesThreadWithCorrectRelationship()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await chatService.CreateShareChatAsync(share.Id);

                // Assert
                var shareChatThread = await context.ShareChatThreads
                    .Include(sct => sct.Thread)
                    .Include(sct => sct.Share)
                    .FirstOrDefaultAsync(sct => sct.ShareId == share.Id);

                shareChatThread.Should().NotBeNull();
                shareChatThread!.ThreadId.Should().BeGreaterThan(0);
                shareChatThread.ShareId.Should().Be(share.Id);
                shareChatThread.Thread.Should().NotBeNull();
                shareChatThread.Share.Should().NotBeNull();
                shareChatThread.Share.Id.Should().Be(share.Id);
            }
        }

        public class SendMessageAsyncTests : ChatServiceTestBase
        {
            [Fact]
            public async Task SendMessageAsync_WithValidContent_CreatesMessage()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Act
                var result = await chatService.SendMessageAsync(share.Id, borrower.Id, "Hello!");

                // Assert
                result.Should().NotBeNull();
                result.Content.Should().Be("Hello!");
                result.SenderId.Should().Be(borrower.Id);
                result.ThreadId.Should().Be(thread.Id);
                result.IsSystemMessage.Should().BeFalse();

                var messageInDb = await context.ChatMessages.FindAsync(result.Id);
                messageInDb.Should().NotBeNull();
                messageInDb!.Content.Should().Be("Hello!");
            }

            [Fact]
            public async Task SendMessageAsync_WithNullOrEmptyContent_ThrowsArgumentException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Act & Assert - null content
                var actNull = async () => await chatService.SendMessageAsync(share.Id, borrower.Id, null!);
                await actNull.Should().ThrowAsync<ArgumentException>()
                    .WithMessage("Message content is required and must be less than 2000 characters");

                // Act & Assert - empty content
                var actEmpty = async () => await chatService.SendMessageAsync(share.Id, borrower.Id, "");
                await actEmpty.Should().ThrowAsync<ArgumentException>()
                    .WithMessage("Message content is required and must be less than 2000 characters");

                // Act & Assert - whitespace content
                var actWhitespace = async () => await chatService.SendMessageAsync(share.Id, borrower.Id, "   ");
                await actWhitespace.Should().ThrowAsync<ArgumentException>()
                    .WithMessage("Message content is required and must be less than 2000 characters");
            }

            [Fact]
            public async Task SendMessageAsync_WithContentOver2000Characters_ThrowsArgumentException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                var longContent = new string('a', 2001);

                // Act
                var act = async () => await chatService.SendMessageAsync(share.Id, borrower.Id, longContent);

                // Assert
                await act.Should().ThrowAsync<ArgumentException>()
                    .WithMessage("Message content is required and must be less than 2000 characters");
            }

            [Fact]
            public async Task SendMessageAsync_WhenShareNotFound_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var nonExistentShareId = 999;

                // Act
                var act = async () => await chatService.SendMessageAsync(nonExistentShareId, "user-1", "Hello!");

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Access denied to this share chat");
            }

            [Fact]
            public async Task SendMessageAsync_WhenUserNotAuthorized_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var unauthorizedUser = TestDataBuilder.CreateUser(id: "unauthorized-user");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower, unauthorizedUser);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await chatService.SendMessageAsync(share.Id, unauthorizedUser.Id, "Hello!");

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Access denied to this share chat");
            }

            [Fact]
            public async Task SendMessageAsync_TrimsContentBeforeSaving()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Act
                var result = await chatService.SendMessageAsync(share.Id, borrower.Id, "  Hello!  ");

                // Assert
                result.Content.Should().Be("Hello!", "content should be trimmed");

                var messageInDb = await context.ChatMessages.FindAsync(result.Id);
                messageInDb!.Content.Should().Be("Hello!");
            }

            [Fact]
            public async Task SendMessageAsync_UpdatesThreadLastActivity()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var initialTime = DateTime.UtcNow.AddHours(-1);
                var thread = TestDataBuilder.CreateChatThread(lastActivity: initialTime);
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Act
                await chatService.SendMessageAsync(share.Id, borrower.Id, "Hello!");

                // Assert
                var updatedThread = await context.ChatThreads.FindAsync(thread.Id);
                updatedThread.Should().NotBeNull();
                updatedThread!.LastActivity.Should().NotBeNull();
                updatedThread.LastActivity.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
                updatedThread.LastActivity.Should().BeAfter(initialTime);
            }

            [Fact]
            public async Task SendMessageAsync_CreatesNotificationForOtherParty()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "John", lastName: "Doe");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = new UserBook
                {
                    UserId = lender.Id,
                    BookId = book.Id
                };
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = borrower.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Act
                await chatService.SendMessageAsync(share.Id, borrower.Id, "Hello!");

                // Assert
                NotificationServiceMock.Verify(
                    ns => ns.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.ShareMessageReceived,
                        It.Is<string>(msg => msg.Contains("John Doe")),
                        borrower.Id
                    ),
                    Times.Once
                );
            }
        }

        public class GetMessageCountAsyncTests : ChatServiceTestBase
        {
            [Fact]
            public async Task GetMessageCountAsync_WithExistingChatThread_ReturnsAccurateCount()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var user = TestDataBuilder.CreateUser();
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = new UserBook { UserId = user.Id, BookId = book.Id, };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = "default-borrower",
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Create 15 messages
                for (int i = 0; i < 15; i++)
                {
                    var message = TestDataBuilder.CreateChatMessage(
                        threadId: thread.Id,
                        senderId: user.Id,
                        content: $"Message {i}"
                    );
                    context.ChatMessages.Add(message);
                }
                await context.SaveChangesAsync();

                // Act
                var count = await chatService.GetMessageCountAsync(share.Id);

                // Assert
                count.Should().Be(15);
            }

            [Fact]
            public async Task GetMessageCountAsync_WhenChatThreadNotFound_ReturnsZero()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var nonExistentShareId = 999;

                // Act
                var count = await chatService.GetMessageCountAsync(nonExistentShareId);

                // Assert
                count.Should().Be(0);
            }
        }

        public class ReportMessageAsyncTests : ChatServiceTestBase
        {
            [Fact]
            public async Task ReportMessageAsync_WithValidRequest_SavesReportWithSnapshots()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                var message = TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: lender.Id, content: "offensive content");
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // Act
                await chatService.ReportMessageAsync(share.Id, message.Id, borrower.Id, ReportCategory.Harassment, "this is harassment");

                // Assert
                var report = await context.ChatMessageReports.SingleAsync();
                report.MessageId.Should().Be(message.Id);
                report.ReporterId.Should().Be(borrower.Id);
                report.ReportedUserId.Should().Be(lender.Id);
                report.ReportedContent.Should().Be("offensive content");
                report.Category.Should().Be(ReportCategory.Harassment);
                report.Notes.Should().Be("this is harassment");
                report.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            }

            [Fact]
            public async Task ReportMessageAsync_WithNullNotes_SavesReportWithNullNotes()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                var message = TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: lender.Id);
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // Act
                await chatService.ReportMessageAsync(share.Id, message.Id, borrower.Id, ReportCategory.Spam, null);

                // Assert
                var report = await context.ChatMessageReports.SingleAsync();
                report.Notes.Should().BeNull();
            }

            [Fact]
            public async Task ReportMessageAsync_WhenShareNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                // Act
                var act = async () => await chatService.ReportMessageAsync(999, 1, "reporter-1", ReportCategory.Spam, null);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share not found");
            }

            [Fact]
            public async Task ReportMessageAsync_WhenReporterNotPartOfShare_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var outsider = TestDataBuilder.CreateUser(id: "outsider-1");
                context.Users.AddRange(lender, borrower, outsider);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                var message = TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: lender.Id);
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await chatService.ReportMessageAsync(share.Id, message.Id, outsider.Id, ReportCategory.Spam, null);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Access denied to this share chat");
            }

            [Fact]
            public async Task ReportMessageAsync_WhenMessageNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                // Act — message id 999 doesn't exist
                var act = async () => await chatService.ReportMessageAsync(share.Id, 999, borrower.Id, ReportCategory.Spam, null);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Message not found");
            }

            [Fact]
            public async Task ReportMessageAsync_WhenMessageBelongsToDifferentShare_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                // Message in a different thread
                var otherThread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(otherThread);
                await context.SaveChangesAsync();

                var messageInOtherThread = TestDataBuilder.CreateChatMessage(threadId: otherThread.Id, senderId: lender.Id);
                context.ChatMessages.Add(messageInOtherThread);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await chatService.ReportMessageAsync(share.Id, messageInOtherThread.Id, borrower.Id, ReportCategory.Spam, null);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Message not found");
            }

            [Fact]
            public async Task ReportMessageAsync_WhenReportingOwnMessage_ThrowsArgumentException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                // borrower sent this message
                var message = TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: borrower.Id);
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // Act — borrower tries to report their own message
                var act = async () => await chatService.ReportMessageAsync(share.Id, message.Id, borrower.Id, ReportCategory.Spam, null);

                // Assert
                await act.Should().ThrowAsync<ArgumentException>()
                    .WithMessage("Cannot report your own message");
            }

            [Fact]
            public async Task ReportMessageAsync_WhenDuplicateReport_ThrowsDuplicateReportException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                var message = TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: lender.Id);
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // First report succeeds
                await chatService.ReportMessageAsync(share.Id, message.Id, borrower.Id, ReportCategory.Spam, null);

                // Act — second report from the same user on the same message
                var act = async () => await chatService.ReportMessageAsync(share.Id, message.Id, borrower.Id, ReportCategory.Harassment, null);

                // Assert
                await act.Should().ThrowAsync<DuplicateReportException>();
            }

            [Fact]
            public async Task ReportMessageAsync_SnapshotsContentEvenIfMessageIsLaterDeleted()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                var userBook = new UserBook { UserId = lender.Id, BookId = book.Id };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share { UserBookId = userBook.Id, Borrower = borrower.Id, Status = ShareStatus.Requested };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                context.ShareChatThreads.Add(TestDataBuilder.CreateShareChatThread(thread.Id, share.Id));
                await context.SaveChangesAsync();

                var originalContent = "bad content";
                var message = TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: lender.Id, content: originalContent);
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // Act
                await chatService.ReportMessageAsync(share.Id, message.Id, borrower.Id, ReportCategory.InappropriateContent, null);

                // Simulate account deletion wiping the message content
                message.Content = "[deleted]";
                await context.SaveChangesAsync();

                // Assert — snapshot preserved the original content
                var report = await context.ChatMessageReports.SingleAsync();
                report.ReportedContent.Should().Be(originalContent);
            }
        }

        public class GetMessageThreadAsyncTests : ChatServiceTestBase
        {
            [Fact]
            public async Task GetMessageThreadAsync_WithValidShare_ReturnsMessagesOrderedByDateDescending()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var user1 = TestDataBuilder.CreateUser(id: "user-1");
                var user2 = TestDataBuilder.CreateUser(id: "user-2");
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = new UserBook { UserId = user1.Id, BookId = book.Id, };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = user2.Id,
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                var message1 = TestDataBuilder.CreateChatMessage(
                    threadId: thread.Id,
                    senderId: user1.Id,
                    content: "First message",
                    sentAt: DateTime.UtcNow.AddMinutes(-10)
                );
                var message2 = TestDataBuilder.CreateChatMessage(
                    threadId: thread.Id,
                    senderId: user2.Id,
                    content: "Second message",
                    sentAt: DateTime.UtcNow.AddMinutes(-5)
                );
                var message3 = TestDataBuilder.CreateChatMessage(
                    threadId: thread.Id,
                    senderId: user1.Id,
                    content: "Third message",
                    sentAt: DateTime.UtcNow
                );

                context.ChatMessages.AddRange(message1, message2, message3);
                await context.SaveChangesAsync();

                // Act
                var messages = await chatService.GetMessageThreadAsync(share.Id, page: 1, pageSize: 10);

                // Assert
                messages.Should().HaveCount(3);
                messages[0].Content.Should().Be("Third message", "messages should be ordered by SentAt descending");
                messages[1].Content.Should().Be("Second message");
                messages[2].Content.Should().Be("First message");
            }

            [Fact]
            public async Task GetMessageThreadAsync_WithPageLessThanOne_DefaultsToPageOne()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var user = TestDataBuilder.CreateUser();
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = new UserBook { UserId = user.Id, BookId = book.Id, };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = "default-borrower",
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                var message = TestDataBuilder.CreateChatMessage(threadId: thread.Id, senderId: user.Id);
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // Act
                var messages = await chatService.GetMessageThreadAsync(share.Id, page: 0, pageSize: 10);

                // Assert
                messages.Should().HaveCount(1, "page defaults to 1 when less than 1");
            }

            [Fact]
            public async Task GetMessageThreadAsync_WithInvalidPageSize_DefaultsToFifty()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var user = TestDataBuilder.CreateUser();
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = new UserBook { UserId = user.Id, BookId = book.Id, };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = "default-borrower",
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                // Create 60 messages
                for (int i = 0; i < 60; i++)
                {
                    var message = TestDataBuilder.CreateChatMessage(
                        threadId: thread.Id,
                        senderId: user.Id,
                        content: $"Message {i}",
                        sentAt: DateTime.UtcNow.AddMinutes(-i)
                    );
                    context.ChatMessages.Add(message);
                }
                await context.SaveChangesAsync();

                // Act - pageSize 101 should default to 50
                var messagesWithLargePageSize = await chatService.GetMessageThreadAsync(share.Id, page: 1, pageSize: 101);

                // Act - pageSize 0 should default to 50
                var messagesWithZeroPageSize = await chatService.GetMessageThreadAsync(share.Id, page: 1, pageSize: 0);

                // Assert
                messagesWithLargePageSize.Should().HaveCount(50, "pageSize > 100 should default to 50");
                messagesWithZeroPageSize.Should().HaveCount(50, "pageSize < 1 should default to 50");
            }

            [Fact]
            public async Task GetMessageThreadAsync_WhenShareChatThreadNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var nonExistentShareId = 999;

                // Act
                var act = async () => await chatService.GetMessageThreadAsync(nonExistentShareId, page: 1, pageSize: 10);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share chat thread not found");
            }

            [Fact]
            public async Task GetMessageThreadAsync_IncludesSenderInformation()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var chatService = new ChatService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1", firstName: "John", lastName: "Doe");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var book = TestDataBuilder.CreateBook();
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = new UserBook { UserId = user.Id, BookId = book.Id, };
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = new Share
                {
                    UserBookId = userBook.Id,
                    Borrower = "default-borrower",
                    Status = ShareStatus.Requested
                };
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var thread = TestDataBuilder.CreateChatThread();
                context.ChatThreads.Add(thread);
                await context.SaveChangesAsync();

                var shareChatThread = TestDataBuilder.CreateShareChatThread(threadId: thread.Id, shareId: share.Id);
                context.ShareChatThreads.Add(shareChatThread);
                await context.SaveChangesAsync();

                var message = TestDataBuilder.CreateChatMessage(
                    threadId: thread.Id,
                    senderId: user.Id,
                    content: "Test message"
                );
                context.ChatMessages.Add(message);
                await context.SaveChangesAsync();

                // Act
                var messages = await chatService.GetMessageThreadAsync(share.Id, page: 1, pageSize: 10);

                // Assert
                messages.Should().HaveCount(1);
                messages[0].Sender.Should().NotBeNull();
                messages[0].Sender.FirstName.Should().Be("John");
                messages[0].Sender.LastName.Should().Be("Doe");
            }
        }
    }
}
