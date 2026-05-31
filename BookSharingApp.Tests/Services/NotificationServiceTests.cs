using BookSharingApp.Common;
using BookSharingApp.Services;
using BookSharingApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.Tests.Services
{
    public class NotificationServiceTests
    {
        // Shared base class for all nested test classes
        public abstract class NotificationServiceTestBase : IDisposable
        {
            protected readonly Mock<ILogger<NotificationService>> LoggerMock;

            protected NotificationServiceTestBase()
            {
                LoggerMock = new Mock<ILogger<NotificationService>>();
            }

            public void Dispose()
            {
                // Cleanup if needed
            }
        }

        public class GetUnreadNotificationsAsyncTests : NotificationServiceTestBase
        {
            [Fact]
            public async Task WithUnreadNotifications_ReturnsOnlyUnreadNotifications()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var creator = TestDataBuilder.CreateUser(id: "creator-1");
                context.Users.AddRange(user, creator);
                await context.SaveChangesAsync();

                var unreadNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    readAt: null
                );

                // Link navigation properties to tracked entities
                unreadNotification.User = user;
                unreadNotification.CreatedByUser = creator;

                context.Notifications.Add(unreadNotification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(user.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].Id.Should().Be(unreadNotification.Id);
            }

            [Fact]
            public async Task WithReadAndUnreadNotifications_ExcludesReadNotifications()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var creator = TestDataBuilder.CreateUser(id: "creator-1");
                context.Users.AddRange(user, creator);
                await context.SaveChangesAsync();

                var unreadNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    readAt: null
                );
                unreadNotification.User = user;
                unreadNotification.CreatedByUser = creator;

                var readNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    readAt: DateTime.UtcNow.AddHours(-1)
                );
                readNotification.User = user;
                readNotification.CreatedByUser = creator;

                context.Notifications.AddRange(unreadNotification, readNotification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(user.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].Id.Should().Be(unreadNotification.Id);
                result.Should().NotContain(n => n.Id == readNotification.Id);
            }

            [Fact]
            public async Task WithNoUnreadNotifications_ReturnsEmptyList()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var readNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    readAt: DateTime.UtcNow
                );
                context.Notifications.Add(readNotification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(user.Id);

                // Assert
                result.Should().BeEmpty();
            }

            [Fact]
            public async Task WithMultipleUsers_ReturnsOnlySpecifiedUserNotifications()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user1 = TestDataBuilder.CreateUser(id: "user-1");
                var user2 = TestDataBuilder.CreateUser(id: "user-2");
                var creator = TestDataBuilder.CreateUser(id: "creator-1");
                context.Users.AddRange(user1, user2, creator);
                await context.SaveChangesAsync();

                var user1Notification = TestDataBuilder.CreateNotification(
                    userId: user1.Id,
                    createdByUserId: creator.Id,
                    readAt: null
                );
                user1Notification.User = user1;
                user1Notification.CreatedByUser = creator;

                var user2Notification = TestDataBuilder.CreateNotification(
                    userId: user2.Id,
                    createdByUserId: creator.Id,
                    readAt: null
                );
                user2Notification.User = user2;
                user2Notification.CreatedByUser = creator;

                context.Notifications.AddRange(user1Notification, user2Notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(user1.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].Id.Should().Be(user1Notification.Id);
                result.Should().NotContain(n => n.Id == user2Notification.Id);
            }

            [Fact]
            public async Task WithMultipleNotifications_ReturnsOrderedByCreatedAtDescending()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var creator = TestDataBuilder.CreateUser(id: "creator-1");
                context.Users.AddRange(user, creator);
                await context.SaveChangesAsync();

                var oldestNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    createdAt: DateTime.UtcNow.AddHours(-3),
                    readAt: null
                );
                oldestNotification.User = user;
                oldestNotification.CreatedByUser = creator;

                var middleNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    createdAt: DateTime.UtcNow.AddHours(-2),
                    readAt: null
                );
                middleNotification.User = user;
                middleNotification.CreatedByUser = creator;

                var newestNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    createdAt: DateTime.UtcNow.AddHours(-1),
                    readAt: null
                );
                newestNotification.User = user;
                newestNotification.CreatedByUser = creator;

                context.Notifications.AddRange(oldestNotification, middleNotification, newestNotification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(user.Id);

                // Assert
                result.Should().HaveCount(3);
                result[0].Id.Should().Be(newestNotification.Id);
                result[1].Id.Should().Be(middleNotification.Id);
                result[2].Id.Should().Be(oldestNotification.Id);
            }

            [Fact]
            public async Task WithValidRequest_IncludesShareNavigationProperty()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: borrower.Id,
                    createdByUserId: lender.Id,
                    shareId: share.Id,
                    readAt: null
                );
                notification.User = borrower;
                notification.CreatedByUser = lender;
                notification.Share = share;

                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].Share.Should().NotBeNull();
                result[0].Share!.Id.Should().Be(share.Id);
            }

            [Fact]
            public async Task WithValidRequest_IncludesUserBookNavigationProperty()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: borrower.Id,
                    createdByUserId: lender.Id,
                    shareId: share.Id,
                    readAt: null
                );
                notification.User = borrower;
                notification.CreatedByUser = lender;
                notification.Share = share;

                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].Share.Should().NotBeNull();
                result[0].Share!.UserBook.Should().NotBeNull();
                result[0].Share?.UserBook.Id.Should().Be(userBook.Id);
            }

            [Fact]
            public async Task WithValidRequest_IncludesBookNavigationProperty()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book Title");

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: borrower.Id,
                    createdByUserId: lender.Id,
                    shareId: share.Id,
                    readAt: null
                );
                notification.User = borrower;
                notification.CreatedByUser = lender;
                notification.Share = share;

                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].Share.Should().NotBeNull();
                result[0].Share!.UserBook.Should().NotBeNull();
                result[0].Share?.UserBook.Book.Should().NotBeNull();
                result[0].Share?.UserBook.Book.Title.Should().Be("Test Book Title");
            }

            [Fact]
            public async Task WithValidRequest_IncludesCreatedByUserNavigationProperty()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");

                context.Users.AddRange(lender, borrower);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: borrower.Id,
                    createdByUserId: lender.Id,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].CreatedByUser.Should().NotBeNull();
                result[0].CreatedByUser.Id.Should().Be(lender.Id);
                result[0].CreatedByUser.FirstName.Should().Be("John");
            }

            [Fact]
            public async Task WithMixedNotificationTypes_ReturnsAllUnreadTypes()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var creator = TestDataBuilder.CreateUser(id: "creator-1");
                context.Users.AddRange(user, creator);
                await context.SaveChangesAsync();

                var statusNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    readAt: null
                );
                statusNotification.User = user;
                statusNotification.CreatedByUser = creator;

                var dueDateNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    notificationType: NotificationType.ShareDueDateChanged,
                    readAt: null
                );
                dueDateNotification.User = user;
                dueDateNotification.CreatedByUser = creator;

                var messageNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    createdByUserId: creator.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    readAt: null
                );
                messageNotification.User = user;
                messageNotification.CreatedByUser = creator;

                context.Notifications.AddRange(statusNotification, dueDateNotification, messageNotification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.GetUnreadNotificationsAsync(user.Id);

                // Assert
                result.Should().HaveCount(3);
                result.Should().Contain(n => n.NotificationType == NotificationType.ShareStatusChanged);
                result.Should().Contain(n => n.NotificationType == NotificationType.ShareDueDateChanged);
                result.Should().Contain(n => n.NotificationType == NotificationType.ShareMessageReceived);
            }
        }

        public class CreateShareNotificationAsyncTests : NotificationServiceTestBase
        {
            [Fact]
            public async Task WhenLenderCreatesNotification_BorrowerIsRecipient()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.UserId.Should().Be(borrower.Id);
            }

            [Fact]
            public async Task WhenBorrowerCreatesNotification_LenderIsRecipient()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    borrower.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.UserId.Should().Be(lender.Id);
            }

            [Fact]
            public async Task WithValidRequest_SetsNotificationTypeCorrectly()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareDueDateChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.NotificationType.Should().Be(NotificationType.ShareDueDateChanged);
            }

            [Fact]
            public async Task WithValidRequest_SetsMessageCorrectly()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var expectedMessage = "Custom test message";

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    expectedMessage,
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.Message.Should().Be(expectedMessage);
            }

            [Fact]
            public async Task WithValidRequest_SetsShareIdCorrectly()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.ShareId.Should().Be(share.Id);
            }

            [Fact]
            public async Task WithValidRequest_SetsCreatedByUserIdCorrectly()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.CreatedByUserId.Should().Be(lender.Id);
            }

            [Fact]
            public async Task WithValidRequest_SetsCreatedAtToUtcNow()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var beforeTime = DateTime.UtcNow.AddSeconds(-1);

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                var afterTime = DateTime.UtcNow.AddSeconds(1);

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.CreatedAt.Should().BeAfter(beforeTime);
                notification.CreatedAt.Should().BeBefore(afterTime);
            }

            [Fact]
            public async Task WithValidRequest_SetsReadAtToNull()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithValidRequest_PersistsNotificationToDatabase()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notificationCount = await context.Notifications.CountAsync();
                notificationCount.Should().Be(1);
            }

            [Fact]
            public async Task WhenShareNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var nonExistentShareId = 999;

                // Act
                var act = async () => await notificationService.CreateShareNotificationAsync(
                    nonExistentShareId,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    "user-1"
                );

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share not found");
            }

            [Fact]
            public async Task WhenUserNotInvolvedInShare_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var outsider = TestDataBuilder.CreateUser(id: "outsider-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower, outsider);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    outsider.Id
                );

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("User is not involved in this share");
            }

            [Fact]
            public async Task WhenRecipientHasArchivedShare_DoesNotCreateNotification()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Borrower has archived the share
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: share.Id,
                    userId: borrower.Id,
                    isArchived: true,
                    archivedAt: DateTime.UtcNow,
                    share: share,
                    user: borrower
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act - Lender creates notification (recipient is borrower who has archived)
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notificationCount = await context.Notifications.CountAsync();
                notificationCount.Should().Be(0);
            }

            [Fact]
            public async Task WhenRecipientHasNotArchivedShare_CreatesNotification()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Lender has archived, but borrower (recipient) has not
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: share.Id,
                    userId: lender.Id,
                    isArchived: true,
                    archivedAt: DateTime.UtcNow,
                    share: share,
                    user: lender
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act - Lender creates notification (recipient is borrower who has NOT archived)
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.UserId.Should().Be(borrower.Id);
            }

            [Fact]
            public async Task WhenRecipientHasUnarchivedShare_CreatesNotification()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Borrower has a ShareUserState but IsArchived is false (unarchived)
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: share.Id,
                    userId: borrower.Id,
                    isArchived: false,
                    archivedAt: null,
                    share: share,
                    user: borrower
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act - Lender creates notification (recipient is borrower who has unarchived)
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notification = await context.Notifications.FirstOrDefaultAsync();
                notification.Should().NotBeNull();
                notification!.UserId.Should().Be(borrower.Id);
            }

            [Fact]
            public async Task WhenRecipientReArchivesAfterUnarchiving_DoesNotCreateNotification()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Borrower archived, then unarchived, then re-archived (simulated by final state)
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: share.Id,
                    userId: borrower.Id,
                    isArchived: true,  // Currently re-archived
                    archivedAt: DateTime.UtcNow,
                    share: share,
                    user: borrower
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act - Lender creates notification (recipient is borrower who has re-archived)
                await notificationService.CreateShareNotificationAsync(
                    share.Id,
                    NotificationType.ShareStatusChanged,
                    "Test message",
                    lender.Id
                );

                // Assert
                var notificationCount = await context.Notifications.CountAsync();
                notificationCount.Should().Be(0);
            }
        }

        public class MarkShareNotificationsAsReadAsyncTests : NotificationServiceTestBase
        {
            [Fact]
            public async Task WithUnreadShareStatusChangedNotification_MarksAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().NotBeNull();
            }

            [Fact]
            public async Task WithUnreadShareDueDateChangedNotification_MarksAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareDueDateChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().NotBeNull();
            }

            [Fact]
            public async Task WithUnreadShareMessageReceivedNotification_DoesNotMarkAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithMixedNotificationTypes_OnlyMarksShareStatusAndDueDateTypes()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var statusNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var dueDateNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareDueDateChanged,
                    shareId: 1,
                    readAt: null
                );
                var messageNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(statusNotification, dueDateNotification, messageNotification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedStatusNotification = await context.Notifications.FindAsync(statusNotification.Id);
                var updatedDueDateNotification = await context.Notifications.FindAsync(dueDateNotification.Id);
                var updatedMessageNotification = await context.Notifications.FindAsync(messageNotification.Id);

                updatedStatusNotification!.ReadAt.Should().NotBeNull();
                updatedDueDateNotification!.ReadAt.Should().NotBeNull();
                updatedMessageNotification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithNotificationsForDifferentUser_DoesNotMarkAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user1 = TestDataBuilder.CreateUser(id: "user-1");
                var user2 = TestDataBuilder.CreateUser(id: "user-2");
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();

                var user1Notification = TestDataBuilder.CreateNotification(
                    userId: user1.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var user2Notification = TestDataBuilder.CreateNotification(
                    userId: user2.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(user1Notification, user2Notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user1.Id);

                // Assert
                var updatedUser1Notification = await context.Notifications.FindAsync(user1Notification.Id);
                var updatedUser2Notification = await context.Notifications.FindAsync(user2Notification.Id);

                updatedUser1Notification!.ReadAt.Should().NotBeNull();
                updatedUser2Notification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithNotificationsForDifferentShare_DoesNotMarkAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var share1Notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var share2Notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 2,
                    readAt: null
                );
                context.Notifications.AddRange(share1Notification, share2Notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedShare1Notification = await context.Notifications.FindAsync(share1Notification.Id);
                var updatedShare2Notification = await context.Notifications.FindAsync(share2Notification.Id);

                updatedShare1Notification!.ReadAt.Should().NotBeNull();
                updatedShare2Notification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithReadShareNotifications_DoesNotUpdateReadAt()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var originalReadTime = DateTime.UtcNow.AddHours(-2);
                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: originalReadTime
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().Be(originalReadTime);
            }

            [Fact]
            public async Task WhenMarkingAsRead_SetsReadAtToUtcNow()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                var beforeTime = DateTime.UtcNow.AddSeconds(-1);

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                var afterTime = DateTime.UtcNow.AddSeconds(1);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().NotBeNull();
                updatedNotification.ReadAt.Should().BeAfter(beforeTime);
                updatedNotification.ReadAt.Should().BeBefore(afterTime);
            }

            [Fact]
            public async Task WithMultipleUnreadShareNotifications_MarksAllAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification1 = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var notification2 = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareDueDateChanged,
                    shareId: 1,
                    readAt: null
                );
                var notification3 = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(notification1, notification2, notification3);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareNotificationsAsReadAsync(1, user.Id);

                // Assert
                var markedAsReadCount = await context.Notifications
                    .Where(n => n.UserId == user.Id && n.ShareId == 1 && n.ReadAt != null)
                    .CountAsync();

                markedAsReadCount.Should().Be(3);
            }

            [Fact]
            public async Task WithNoMatchingNotifications_DoesNotThrowException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await notificationService.MarkShareNotificationsAsReadAsync(999, user.Id);

                // Assert
                await act.Should().NotThrowAsync();
            }
        }

        public class MarkShareChatNotificationsAsReadAsyncTests : NotificationServiceTestBase
        {
            [Fact]
            public async Task WithUnreadShareMessageReceivedNotification_MarksAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().NotBeNull();
            }

            [Fact]
            public async Task WithUnreadShareStatusChangedNotification_DoesNotMarkAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithUnreadShareDueDateChangedNotification_DoesNotMarkAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareDueDateChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithMixedNotificationTypes_OnlyMarksShareMessageReceivedType()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var statusNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var dueDateNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareDueDateChanged,
                    shareId: 1,
                    readAt: null
                );
                var messageNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(statusNotification, dueDateNotification, messageNotification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedStatusNotification = await context.Notifications.FindAsync(statusNotification.Id);
                var updatedDueDateNotification = await context.Notifications.FindAsync(dueDateNotification.Id);
                var updatedMessageNotification = await context.Notifications.FindAsync(messageNotification.Id);

                updatedStatusNotification!.ReadAt.Should().BeNull();
                updatedDueDateNotification!.ReadAt.Should().BeNull();
                updatedMessageNotification!.ReadAt.Should().NotBeNull();
            }

            [Fact]
            public async Task WithNotificationsForDifferentUser_DoesNotMarkAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user1 = TestDataBuilder.CreateUser(id: "user-1");
                var user2 = TestDataBuilder.CreateUser(id: "user-2");
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();

                var user1Notification = TestDataBuilder.CreateNotification(
                    userId: user1.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                var user2Notification = TestDataBuilder.CreateNotification(
                    userId: user2.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(user1Notification, user2Notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user1.Id);

                // Assert
                var updatedUser1Notification = await context.Notifications.FindAsync(user1Notification.Id);
                var updatedUser2Notification = await context.Notifications.FindAsync(user2Notification.Id);

                updatedUser1Notification!.ReadAt.Should().NotBeNull();
                updatedUser2Notification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithNotificationsForDifferentShare_DoesNotMarkAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var share1Notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                var share2Notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 2,
                    readAt: null
                );
                context.Notifications.AddRange(share1Notification, share2Notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedShare1Notification = await context.Notifications.FindAsync(share1Notification.Id);
                var updatedShare2Notification = await context.Notifications.FindAsync(share2Notification.Id);

                updatedShare1Notification!.ReadAt.Should().NotBeNull();
                updatedShare2Notification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithReadChatNotifications_DoesNotUpdateReadAt()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var originalReadTime = DateTime.UtcNow.AddHours(-2);
                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: originalReadTime
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().Be(originalReadTime);
            }

            [Fact]
            public async Task WhenMarkingAsRead_SetsReadAtToUtcNow()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                var beforeTime = DateTime.UtcNow.AddSeconds(-1);

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                var afterTime = DateTime.UtcNow.AddSeconds(1);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().NotBeNull();
                updatedNotification.ReadAt.Should().BeAfter(beforeTime);
                updatedNotification.ReadAt.Should().BeBefore(afterTime);
            }

            [Fact]
            public async Task WithMultipleUnreadChatNotifications_MarksAllAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification1 = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                var notification2 = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                var notification3 = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(notification1, notification2, notification3);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkShareChatNotificationsAsReadAsync(1, user.Id);

                // Assert
                var markedAsReadCount = await context.Notifications
                    .Where(n => n.UserId == user.Id && n.ShareId == 1 && n.ReadAt != null)
                    .CountAsync();

                markedAsReadCount.Should().Be(3);
            }

            [Fact]
            public async Task WithNoMatchingNotifications_DoesNotThrowException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await notificationService.MarkShareChatNotificationsAsReadAsync(999, user.Id);

                // Assert
                await act.Should().NotThrowAsync();
            }
        }

        public class MarkAllShareNotificationsAsReadForUserAsyncTests : NotificationServiceTestBase
        {
            [Fact]
            public async Task MarksAllUnreadNotificationsAsRead()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Create notifications of all types
                var statusNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var dueDateNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareDueDateChanged,
                    shareId: 1,
                    readAt: null
                );
                var messageNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareMessageReceived,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(statusNotification, dueDateNotification, messageNotification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkAllShareNotificationsAsReadForUserAsync(1, user.Id);

                // Assert
                var allNotifications = await context.Notifications
                    .Where(n => n.UserId == user.Id && n.ShareId == 1)
                    .ToListAsync();

                allNotifications.Should().HaveCount(3);
                allNotifications.Should().AllSatisfy(n => n.ReadAt.Should().NotBeNull());
            }

            [Fact]
            public async Task DoesNotAffectAlreadyReadNotifications()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var originalReadTime = DateTime.UtcNow.AddHours(-2);
                var readNotification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: originalReadTime
                );
                context.Notifications.Add(readNotification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkAllShareNotificationsAsReadForUserAsync(1, user.Id);

                // Assert
                var notification = await context.Notifications.FindAsync(readNotification.Id);
                notification.Should().NotBeNull();
                notification!.ReadAt.Should().Be(originalReadTime);
            }

            [Fact]
            public async Task OnlyAffectsSpecifiedUser()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user1 = TestDataBuilder.CreateUser(id: "user-1");
                var user2 = TestDataBuilder.CreateUser(id: "user-2");
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();

                var user1Notification = TestDataBuilder.CreateNotification(
                    userId: user1.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var user2Notification = TestDataBuilder.CreateNotification(
                    userId: user2.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.AddRange(user1Notification, user2Notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkAllShareNotificationsAsReadForUserAsync(1, user1.Id);

                // Assert
                var updatedUser1Notification = await context.Notifications.FindAsync(user1Notification.Id);
                var updatedUser2Notification = await context.Notifications.FindAsync(user2Notification.Id);

                updatedUser1Notification!.ReadAt.Should().NotBeNull();
                updatedUser2Notification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task OnlyAffectsSpecifiedShare()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var share1Notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                var share2Notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 2,
                    readAt: null
                );
                context.Notifications.AddRange(share1Notification, share2Notification);
                await context.SaveChangesAsync();

                // Act
                await notificationService.MarkAllShareNotificationsAsReadForUserAsync(1, user.Id);

                // Assert
                var updatedShare1Notification = await context.Notifications.FindAsync(share1Notification.Id);
                var updatedShare2Notification = await context.Notifications.FindAsync(share2Notification.Id);

                updatedShare1Notification!.ReadAt.Should().NotBeNull();
                updatedShare2Notification!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WhenMarkingAsRead_SetsReadAtToUtcNow()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(
                    userId: user.Id,
                    notificationType: NotificationType.ShareStatusChanged,
                    shareId: 1,
                    readAt: null
                );
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                var beforeTime = DateTime.UtcNow.AddSeconds(-1);

                // Act
                await notificationService.MarkAllShareNotificationsAsReadForUserAsync(1, user.Id);

                var afterTime = DateTime.UtcNow.AddSeconds(1);

                // Assert
                var updatedNotification = await context.Notifications.FindAsync(notification.Id);
                updatedNotification.Should().NotBeNull();
                updatedNotification!.ReadAt.Should().NotBeNull();
                updatedNotification.ReadAt.Should().BeAfter(beforeTime);
                updatedNotification.ReadAt.Should().BeBefore(afterTime);
            }

            [Fact]
            public async Task WithNoMatchingNotifications_DoesNotThrowException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await notificationService.MarkAllShareNotificationsAsReadForUserAsync(999, user.Id);

                // Assert
                await act.Should().NotThrowAsync();
            }
        }

        public class MarkNotificationAsReadAsyncTests : NotificationServiceTestBase
        {
            [Fact]
            public async Task WithUnreadNotification_SetsReadAt()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(userId: user.Id, readAt: null);
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.MarkNotificationAsReadAsync(notification.Id, user.Id);

                // Assert
                result.Should().BeTrue();
                var updated = await context.Notifications.FindAsync(notification.Id);
                updated!.ReadAt.Should().NotBeNull();
            }

            [Fact]
            public async Task WithAlreadyReadNotification_ReturnsTrueWithoutUpdating()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var originalReadAt = DateTime.UtcNow.AddHours(-1);
                var notification = TestDataBuilder.CreateNotification(userId: user.Id, readAt: originalReadAt);
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.MarkNotificationAsReadAsync(notification.Id, user.Id);

                // Assert
                result.Should().BeTrue();
                var updated = await context.Notifications.FindAsync(notification.Id);
                updated!.ReadAt.Should().Be(originalReadAt);
            }

            [Fact]
            public async Task WithNotificationBelongingToOtherUser_ReturnsFalse()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var owner = TestDataBuilder.CreateUser(id: "owner-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-1");
                context.Users.AddRange(owner, otherUser);
                await context.SaveChangesAsync();

                var notification = TestDataBuilder.CreateNotification(userId: owner.Id, readAt: null);
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.MarkNotificationAsReadAsync(notification.Id, otherUser.Id);

                // Assert
                result.Should().BeFalse();
                var unchanged = await context.Notifications.FindAsync(notification.Id);
                unchanged!.ReadAt.Should().BeNull();
            }

            [Fact]
            public async Task WithNonExistentNotification_ReturnsFalse()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var notificationService = new NotificationService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Act
                var result = await notificationService.MarkNotificationAsReadAsync(999, user.Id);

                // Assert
                result.Should().BeFalse();
            }
        }
    }
}
