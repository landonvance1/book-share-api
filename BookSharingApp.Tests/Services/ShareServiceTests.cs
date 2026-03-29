using BookSharingApp.Common;
using BookSharingApp.Services;
using BookSharingApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.Tests.Services
{
    public class ShareServiceTests
    {
        // Shared base class for all nested test classes
        public abstract class ShareServiceTestBase : IDisposable
        {
            protected readonly Mock<ILogger<ShareService>> LoggerMock;
            protected readonly Mock<INotificationService> NotificationServiceMock;

            protected ShareServiceTestBase()
            {
                LoggerMock = new Mock<ILogger<ShareService>>();
                NotificationServiceMock = new Mock<INotificationService>();
            }

            public void Dispose()
            {
                // Cleanup if needed
            }
        }

        public class CreateShareAsyncTests : ShareServiceTestBase
        {
            [Fact]
            public async Task CreateShareAsync_WithValidRequest_CreatesShareWithRequestedStatus()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");
                var community = TestDataBuilder.CreateCommunity(name: "Book Club");

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                var communityUser1 = TestDataBuilder.CreateCommunityUser(community.Id, lender.Id);
                var communityUser2 = TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id);
                context.CommunityUsers.AddRange(communityUser1, communityUser2);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                result.Should().NotBeNull();
                result.UserBookId.Should().Be(userBook.Id);
                result.Borrower.Should().Be(borrower.Id);
                result.Status.Should().Be(ShareStatus.Requested);
                result.ReturnDate.Should().BeNull();

                var shareInDb = await context.Shares.FindAsync(result.Id);
                shareInDb.Should().NotBeNull();
                shareInDb!.Status.Should().Be(ShareStatus.Requested);
            }

            [Fact]
            public async Task CreateShareAsync_WithValidRequest_CreatesNotificationForLender()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id)
                );
                await context.SaveChangesAsync();

                // Act
                await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        It.IsAny<int>(),
                        NotificationType.ShareStatusChanged,
                        It.Is<string>(msg => msg.Contains("New share request")),
                        borrower.Id
                    ),
                    Times.Once
                );
            }

            [Fact]
            public async Task CreateShareAsync_WhenUserBookNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.Add(borrower);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.CreateShareAsync(999, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("UserBook not found");
            }

            [Fact]
            public async Task CreateShareAsync_WhenBorrowingOwnBook_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.Add(user);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: user.Id,
                    bookId: book.Id,
user: user,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.CreateShareAsync(userBook.Id, user.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Cannot borrow your own book");
            }

            [Fact]
            public async Task CreateShareAsync_WhenNoSharedCommunity_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var lenderCommunity = TestDataBuilder.CreateCommunity(name: "Lender Community");
                var borrowerCommunity = TestDataBuilder.CreateCommunity(name: "Borrower Community");

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.AddRange(lenderCommunity, borrowerCommunity);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                // Users are in different communities - no shared community
                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(lenderCommunity.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(borrowerCommunity.Id, borrower.Id)
                );
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("You must share a community with the book owner to request this book");
            }

            [Fact]
            public async Task CreateShareAsync_WhenActiveShareAlreadyExists_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id)
                );
                await context.SaveChangesAsync();

                // Create an existing active share (status <= Returned)
                var existingShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(existingShare);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("You already have an active share request for this book");
            }

            [Fact]
            public async Task CreateShareAsync_WhenPreviousShareIsTerminal_AllowsNewShareRequest()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id)
                );
                await context.SaveChangesAsync();

                // Create a previous share in terminal state (HomeSafe)
                var previousShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,  // Terminal state
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(previousShare);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                result.Should().NotBeNull();
                result.Status.Should().Be(ShareStatus.Requested);
            }

            [Fact]
            public async Task CreateShareAsync_WhenBookHasActiveShareReady_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower1 = TestDataBuilder.CreateUser(id: "borrower-1");
                var borrower2 = TestDataBuilder.CreateUser(id: "borrower-2");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower1, borrower2);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower1.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower2.Id)
                );
                await context.SaveChangesAsync();

                // Create an existing active share in Ready state
                var existingShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower1.Id,
                    status: ShareStatus.Ready,
                    userBook: userBook,
                    borrowerUser: borrower1
                );
                context.Shares.Add(existingShare);
                await context.SaveChangesAsync();

                // Act - Different user tries to request the same book
                var act = async () => await shareService.CreateShareAsync(userBook.Id, borrower2.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("This book is currently out on loan");
            }

            [Fact]
            public async Task CreateShareAsync_WhenBookHasActiveSharePickedUp_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower1 = TestDataBuilder.CreateUser(id: "borrower-1");
                var borrower2 = TestDataBuilder.CreateUser(id: "borrower-2");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower1, borrower2);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower1.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower2.Id)
                );
                await context.SaveChangesAsync();

                // Create an existing active share in PickedUp state
                var existingShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower1.Id,
                    status: ShareStatus.PickedUp,
                    userBook: userBook,
                    borrowerUser: borrower1
                );
                context.Shares.Add(existingShare);
                await context.SaveChangesAsync();

                // Act - Different user tries to request the same book
                var act = async () => await shareService.CreateShareAsync(userBook.Id, borrower2.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("This book is currently out on loan");
            }

            [Fact]
            public async Task CreateShareAsync_WhenBookHasActiveShareReturned_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower1 = TestDataBuilder.CreateUser(id: "borrower-1");
                var borrower2 = TestDataBuilder.CreateUser(id: "borrower-2");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower1, borrower2);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower1.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower2.Id)
                );
                await context.SaveChangesAsync();

                // Create an existing active share in Returned state
                var existingShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower1.Id,
                    status: ShareStatus.Returned,
                    userBook: userBook,
                    borrowerUser: borrower1
                );
                context.Shares.Add(existingShare);
                await context.SaveChangesAsync();

                // Act - Different user tries to request the same book
                var act = async () => await shareService.CreateShareAsync(userBook.Id, borrower2.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("This book is currently out on loan");
            }

            [Fact]
            public async Task CreateShareAsync_WhenBookHasRequestedShare_AllowsNewRequest()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower1 = TestDataBuilder.CreateUser(id: "borrower-1");
                var borrower2 = TestDataBuilder.CreateUser(id: "borrower-2");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower1, borrower2);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower1.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower2.Id)
                );
                await context.SaveChangesAsync();

                // Create an existing share in Requested state (not yet approved)
                var existingShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower1.Id,
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: borrower1
                );
                context.Shares.Add(existingShare);
                await context.SaveChangesAsync();

                // Act - Different user tries to request the same book (should succeed since not approved yet)
                var result = await shareService.CreateShareAsync(userBook.Id, borrower2.Id);

                // Assert
                result.Should().NotBeNull();
                result.Status.Should().Be(ShareStatus.Requested);
                result.Borrower.Should().Be(borrower2.Id);
            }

            [Fact]
            public async Task CreateShareAsync_WhenBookHasTerminalShareHomeSafe_AllowsNewRequestByDifferentUser()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower1 = TestDataBuilder.CreateUser(id: "borrower-1");
                var borrower2 = TestDataBuilder.CreateUser(id: "borrower-2");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower1, borrower2);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower1.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower2.Id)
                );
                await context.SaveChangesAsync();

                // Create a previous share in terminal state HomeSafe (book was borrowed and returned)
                var previousShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower1.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower1
                );
                context.Shares.Add(previousShare);
                await context.SaveChangesAsync();

                // Act - Different user tries to request the same book (should succeed since previous transaction completed)
                var result = await shareService.CreateShareAsync(userBook.Id, borrower2.Id);

                // Assert
                result.Should().NotBeNull();
                result.Status.Should().Be(ShareStatus.Requested);
                result.Borrower.Should().Be(borrower2.Id);
            }

            [Fact]
            public async Task CreateShareAsync_WhenSameUserHasTerminalShareHomeSafe_AllowsNewRequest()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id)
                );
                await context.SaveChangesAsync();

                // Create a previous share in terminal state HomeSafe - same borrower borrowed and returned the book
                var previousShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(previousShare);
                await context.SaveChangesAsync();

                // Act - Same borrower requests the book again (should succeed - users can borrow the same book multiple times)
                var result = await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                result.Should().NotBeNull();
                result.Status.Should().Be(ShareStatus.Requested);
                result.Borrower.Should().Be(borrower.Id);

                // Verify there are now 2 shares for this book by this borrower
                var allShares = await context.Shares
                    .Where(s => s.UserBookId == userBook.Id && s.Borrower == borrower.Id)
                    .ToListAsync();
                allShares.Should().HaveCount(2);
            }

            [Fact]
            public async Task CreateShareAsync_WithMultipleSharedCommunities_CreatesShare()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community1 = TestDataBuilder.CreateCommunity(name: "Community 1");
                var community2 = TestDataBuilder.CreateCommunity(name: "Community 2");

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.AddRange(community1, community2);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                // Both users share multiple communities
                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community1.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community1.Id, borrower.Id),
                    TestDataBuilder.CreateCommunityUser(community2.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community2.Id, borrower.Id)
                );
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                result.Should().NotBeNull();
                result.Status.Should().Be(ShareStatus.Requested);
            }

            [Fact]
            public async Task CreateShareAsync_WhenBorrowerInNoCommunities_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                // Only lender is in a community, borrower is not
                context.CommunityUsers.Add(TestDataBuilder.CreateCommunityUser(community.Id, lender.Id));
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("You must share a community with the book owner to request this book");
            }

            [Fact]
            public async Task CreateShareAsync_WhenUserBookIsSoftDeleted_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook();
                var community = TestDataBuilder.CreateCommunity();

                context.Users.AddRange(lender, borrower);
                context.Books.Add(book);
                context.Communities.Add(community);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: lender.Id,
                    bookId: book.Id,
isDeleted: true,
                    deletedAt: DateTime.UtcNow,
                    user: lender,
                    book: book
                );
                context.UserBooks.Add(userBook);

                context.CommunityUsers.AddRange(
                    TestDataBuilder.CreateCommunityUser(community.Id, lender.Id),
                    TestDataBuilder.CreateCommunityUser(community.Id, borrower.Id)
                );
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.CreateShareAsync(userBook.Id, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Book has been removed from the owner's library");
            }
        }

        public class GetSharesTests : ShareServiceTestBase
        {
            [Fact]
            public async Task GetBorrowerSharesAsync_ExcludesArchivedShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Create active share
                var activeShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                // Create archived share
                var archivedShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                context.Shares.AddRange(activeShare, archivedShare);
                await context.SaveChangesAsync();

                // Archive the second share
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: archivedShare.Id,
                    userId: borrower.Id,
                    isArchived: true,
                    archivedAt: DateTime.UtcNow,
                    share: archivedShare,
                    user: borrower
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.GetBorrowerSharesAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                result.Should().Contain(s => s.Id == activeShare.Id);
                result.Should().NotContain(s => s.Id == archivedShare.Id);
            }

            [Fact]
            public async Task GetBorrowerSharesAsync_IncludesNavigationProperties()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book", author: "Test Author");

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
                    status: ShareStatus.Ready,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.GetBorrowerSharesAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                var retrievedShare = result.First();
                retrievedShare.UserBook.Should().NotBeNull();
                retrievedShare.UserBook.Book.Should().NotBeNull();
                retrievedShare.UserBook.Book.Title.Should().Be("Test Book");
                retrievedShare.UserBook.User.Should().NotBeNull();
                retrievedShare.UserBook.User.FirstName.Should().Be("John");
            }

            [Fact]
            public async Task GetLenderSharesAsync_ExcludesArchivedShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Create active share
                var activeShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.PickedUp,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                // Create archived share
                var archivedShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.Declined,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                context.Shares.AddRange(activeShare, archivedShare);
                await context.SaveChangesAsync();

                // Archive the second share for the lender
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: archivedShare.Id,
                    userId: lender.Id,
                    isArchived: true,
                    archivedAt: DateTime.UtcNow,
                    share: archivedShare,
                    user: lender
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.GetLenderSharesAsync(lender.Id);

                // Assert
                result.Should().HaveCount(1);
                result.Should().Contain(s => s.Id == activeShare.Id);
                result.Should().NotContain(s => s.Id == archivedShare.Id);
            }

            [Fact]
            public async Task GetLenderSharesAsync_IncludesNavigationProperties()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Book", author: "Famous Author");

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
                    status: ShareStatus.Returned,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.GetLenderSharesAsync(lender.Id);

                // Assert
                result.Should().HaveCount(1);
                var retrievedShare = result.First();
                retrievedShare.UserBook.Should().NotBeNull();
                retrievedShare.UserBook.Book.Should().NotBeNull();
                retrievedShare.UserBook.Book.Title.Should().Be("The Great Book");
                retrievedShare.BorrowerUser.Should().NotBeNull();
                retrievedShare.BorrowerUser.FirstName.Should().Be("Jane");
            }

            [Fact]
            public async Task GetArchivedBorrowerSharesAsync_ReturnsOnlyArchivedShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Create active share
                var activeShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                // Create archived share
                var archivedShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                context.Shares.AddRange(activeShare, archivedShare);
                await context.SaveChangesAsync();

                // Archive the second share
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: archivedShare.Id,
                    userId: borrower.Id,
                    isArchived: true,
                    archivedAt: DateTime.UtcNow,
                    share: archivedShare,
                    user: borrower
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.GetArchivedBorrowerSharesAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                result.Should().Contain(s => s.Id == archivedShare.Id);
                result.Should().NotContain(s => s.Id == activeShare.Id);
            }

            [Fact]
            public async Task GetArchivedBorrowerSharesAsync_IncludesNavigationProperties()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "Bob", lastName: "Builder");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Archived Book", author: "Past Author");

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
                    status: ShareStatus.PickedUp,
                    isDisputed: true,
                    disputedBy: lender.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

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

                // Act
                var result = await shareService.GetArchivedBorrowerSharesAsync(borrower.Id);

                // Assert
                result.Should().HaveCount(1);
                var retrievedShare = result.First();
                retrievedShare.UserBook.Should().NotBeNull();
                retrievedShare.UserBook.Book.Should().NotBeNull();
                retrievedShare.UserBook.Book.Title.Should().Be("Archived Book");
                retrievedShare.UserBook.User.Should().NotBeNull();
                retrievedShare.UserBook.User.LastName.Should().Be("Builder");
            }

            [Fact]
            public async Task GetArchivedLenderSharesAsync_ReturnsOnlyArchivedShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Create active share
                var activeShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.Ready,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                // Create archived share
                var archivedShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.Declined,
                    userBook: userBook,
                    borrowerUser: borrower
                );

                context.Shares.AddRange(activeShare, archivedShare);
                await context.SaveChangesAsync();

                // Archive the second share for the lender
                var shareUserState = TestDataBuilder.CreateShareUserState(
                    shareId: archivedShare.Id,
                    userId: lender.Id,
                    isArchived: true,
                    archivedAt: DateTime.UtcNow,
                    share: archivedShare,
                    user: lender
                );
                context.ShareUserStates.Add(shareUserState);
                await context.SaveChangesAsync();

                // Act
                var result = await shareService.GetArchivedLenderSharesAsync(lender.Id);

                // Assert
                result.Should().HaveCount(1);
                result.Should().Contain(s => s.Id == archivedShare.Id);
                result.Should().NotContain(s => s.Id == activeShare.Id);
            }

            [Fact]
            public async Task GetArchivedLenderSharesAsync_IncludesNavigationProperties()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Alice", lastName: "Wonder");
                var book = TestDataBuilder.CreateBook(title: "Old Book", author: "Classic Author");

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

                // Act
                var result = await shareService.GetArchivedLenderSharesAsync(lender.Id);

                // Assert
                result.Should().HaveCount(1);
                var retrievedShare = result.First();
                retrievedShare.UserBook.Should().NotBeNull();
                retrievedShare.UserBook.Book.Should().NotBeNull();
                retrievedShare.UserBook.Book.Title.Should().Be("Old Book");
                retrievedShare.BorrowerUser.Should().NotBeNull();
                retrievedShare.BorrowerUser.FirstName.Should().Be("Alice");
            }
        }

        public class ArchiveShareAsyncTests : ShareServiceTestBase
        {
            [Fact]
            public async Task ArchiveShareAsync_WithTerminalStatusHomeSafe_CreatesShareUserState()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                    status: ShareStatus.HomeSafe,  // Terminal status
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Assert
                var shareUserState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);

                shareUserState.Should().NotBeNull();
                shareUserState!.IsArchived.Should().BeTrue();
                shareUserState.ArchivedAt.Should().NotBeNull();
                shareUserState.ArchivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            }

            [Fact]
            public async Task ArchiveShareAsync_WithTerminalStatusDeclined_CreatesShareUserState()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                    status: ShareStatus.Declined,  // Terminal status
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.ArchiveShareAsync(share.Id, borrower.Id);

                // Assert
                var shareUserState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == borrower.Id);

                shareUserState.Should().NotBeNull();
                shareUserState!.IsArchived.Should().BeTrue();
            }

            [Fact]
            public async Task ArchiveShareAsync_WithTerminalStatusDisputed_CreatesShareUserState()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                    status: ShareStatus.Returned,
                    isDisputed: true,
                    disputedBy: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Assert
                var shareUserState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);

                shareUserState.Should().NotBeNull();
                shareUserState!.IsArchived.Should().BeTrue();
            }

            [Fact]
            public async Task ArchiveShareAsync_WithNonTerminalStatus_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                    status: ShareStatus.Requested,  // NOT a terminal status
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Can only archive shares in terminal status (Declined, HomeSafe, Disputed) or when the associated book has been deleted");
            }

            [Fact]
            public async Task ArchiveShareAsync_WhenUserBookIsDeleted_BorrowerCanArchiveActiveShare()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                userBook.IsDeleted = true;  // UserBook has been soft deleted
                userBook.DeletedAt = DateTime.UtcNow;
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.PickedUp,  // Active status, NOT terminal
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act - Borrower should be able to archive even though share is in active status
                await shareService.ArchiveShareAsync(share.Id, borrower.Id);

                // Assert
                var shareUserState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == borrower.Id);

                shareUserState.Should().NotBeNull();
                shareUserState!.IsArchived.Should().BeTrue();
                shareUserState.ArchivedAt.Should().NotBeNull();
            }

            [Fact]
            public async Task ArchiveShareAsync_WhenUserBookIsDeleted_LenderCanArchiveActiveShare()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                userBook.IsDeleted = true;  // UserBook has been soft deleted
                userBook.DeletedAt = DateTime.UtcNow;
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: ShareStatus.PickedUp,  // Active status, NOT terminal
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act - Lender should be able to archive even though share is in active status
                await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Assert
                var shareUserState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);

                shareUserState.Should().NotBeNull();
                shareUserState!.IsArchived.Should().BeTrue();
                shareUserState.ArchivedAt.Should().NotBeNull();
            }

            [Theory]
            [InlineData(ShareStatus.Ready)]
            [InlineData(ShareStatus.PickedUp)]
            [InlineData(ShareStatus.Returned)]
            public async Task ArchiveShareAsync_WhenUserBookIsDeleted_AllowsArchivingAnyActiveStatus(ShareStatus activeStatus)
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                userBook.IsDeleted = true;  // UserBook has been soft deleted
                userBook.DeletedAt = DateTime.UtcNow;
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower.Id,
                    status: activeStatus,  // Test each active status
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act - Should be able to archive any active status when UserBook is deleted
                await shareService.ArchiveShareAsync(share.Id, borrower.Id);

                // Assert
                var shareUserState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == borrower.Id);

                shareUserState.Should().NotBeNull();
                shareUserState!.IsArchived.Should().BeTrue();
            }

            [Fact]
            public async Task ArchiveShareAsync_WhenNotLenderOrBorrower_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var unauthorizedUser = TestDataBuilder.CreateUser(id: "unauthorized-user");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower, unauthorizedUser);
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

                // Act
                var act = async () => await shareService.ArchiveShareAsync(share.Id, unauthorizedUser.Id);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Only the lender or borrower can archive the share");
            }

            [Fact]
            public async Task ArchiveShareAsync_WhenAlreadyArchived_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Archive the share first time
                await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Act - Try to archive again
                var act = async () => await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share is already archived");
            }

            [Fact]
            public async Task ArchiveShareAsync_AllowsIndependentArchivingByLenderAndBorrower()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Act - Archive by lender first
                await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Then archive by borrower
                await shareService.ArchiveShareAsync(share.Id, borrower.Id);

                // Assert
                var lenderState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);
                var borrowerState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == borrower.Id);

                lenderState.Should().NotBeNull();
                lenderState!.IsArchived.Should().BeTrue();

                borrowerState.Should().NotBeNull();
                borrowerState!.IsArchived.Should().BeTrue();

                // Both should have independent archive states
                context.ShareUserStates.Count(sus => sus.ShareId == share.Id).Should().Be(2);
            }

            [Fact]
            public async Task ArchiveShareAsync_CallsMarkAllShareNotificationsAsReadForUser()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Act
                await shareService.ArchiveShareAsync(share.Id, lender.Id);

                // Assert
                NotificationServiceMock.Verify(
                    x => x.MarkAllShareNotificationsAsReadForUserAsync(share.Id, lender.Id),
                    Times.Once
                );
            }
        }

        public class UpdateShareDueDateAsyncTests : ShareServiceTestBase
        {
            [Fact]
            public async Task UpdateShareDueDateAsync_ByLender_UpdatesReturnDateAndCreatesNotification()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby");

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
                    status: ShareStatus.Ready,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var newDueDate = DateTime.UtcNow.AddDays(14);

                // Act
                await shareService.UpdateShareDueDateAsync(share.Id, newDueDate, lender.Id);

                // Assert
                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare.Should().NotBeNull();
                updatedShare!.ReturnDate.Should().BeCloseTo(newDueDate, TimeSpan.FromSeconds(1));

                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.ShareDueDateChanged,
                        It.Is<string>(msg => msg.Contains("Return date") && msg.Contains("The Great Gatsby")),
                        lender.Id
                    ),
                    Times.Once
                );
            }

            [Fact]
            public async Task UpdateShareDueDateAsync_ByBorrower_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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
                    status: ShareStatus.PickedUp,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var newDueDate = DateTime.UtcNow.AddDays(7);

                // Act
                var act = async () => await shareService.UpdateShareDueDateAsync(share.Id, newDueDate, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Only the lender can set the return date");
            }

            [Fact]
            public async Task UpdateShareDueDateAsync_WhenShareNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                context.Users.Add(lender);
                await context.SaveChangesAsync();

                var newDueDate = DateTime.UtcNow.AddDays(14);

                // Act
                var act = async () => await shareService.UpdateShareDueDateAsync(999, newDueDate, lender.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share not found");
            }

            [Fact]
            public async Task UpdateShareDueDateAsync_ByUnauthorizedUser_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var unauthorizedUser = TestDataBuilder.CreateUser(id: "unauthorized-user");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower, unauthorizedUser);
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
                    status: ShareStatus.Ready,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                var newDueDate = DateTime.UtcNow.AddDays(14);

                // Act
                var act = async () => await shareService.UpdateShareDueDateAsync(share.Id, newDueDate, unauthorizedUser.Id);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Only the lender can set the return date");
            }
        }

        public class UpdateShareStatusAsyncTests : ShareServiceTestBase
        {
            [Fact]
            public async Task UpdateShareStatusAsync_WithValidStatus_UpdatesStatusAndCreatesNotification()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby");

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
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.UpdateShareStatusAsync(share.Id, ShareStatus.Ready, lender.Id);

                // Assert
                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare.Should().NotBeNull();
                updatedShare!.Status.Should().Be(ShareStatus.Ready);

                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.ShareStatusChanged,
                        It.Is<string>(msg => msg.Contains("ready for pickup")),
                        lender.Id
                    ),
                    Times.Once
                );
            }

            [Fact]
            public async Task UpdateShareStatusAsync_ToPickedUp_UpdatesStatusWithCorrectMessage()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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
                    status: ShareStatus.Ready,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.UpdateShareStatusAsync(share.Id, ShareStatus.PickedUp, borrower.Id);

                // Assert
                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare!.Status.Should().Be(ShareStatus.PickedUp);

                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.ShareStatusChanged,
                        It.Is<string>(msg => msg.Contains("picked up") && msg.Contains("Jane Smith")),
                        borrower.Id
                    ),
                    Times.Once
                );
            }

            [Fact]
            public async Task UpdateShareStatusAsync_WhenShareNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.UpdateShareStatusAsync(999, ShareStatus.Ready, user.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share not found");
            }

            [Fact]
            public async Task UpdateShareStatusAsync_ToHomeSafe_UpdatesStatusAndPersistsToDatabase()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Book Title");

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
                    status: ShareStatus.Returned,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.UpdateShareStatusAsync(share.Id, ShareStatus.HomeSafe, lender.Id);

                // Assert
                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare!.Status.Should().Be(ShareStatus.HomeSafe);

                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.ShareStatusChanged,
                        It.Is<string>(msg => msg.Contains("confirmed home safe")),
                        lender.Id
                    ),
                    Times.Once
                );
            }
        }

        public class UnarchiveShareAsyncTests : ShareServiceTestBase
        {
            [Fact]
            public async Task UnarchiveShareAsync_WhenArchived_SetsIsArchivedToFalseAndClearsArchivedAt()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Archive the share first
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

                // Act
                await shareService.UnarchiveShareAsync(share.Id, lender.Id);

                // Assert
                var updatedState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);

                updatedState.Should().NotBeNull();
                updatedState!.IsArchived.Should().BeFalse();
                updatedState.ArchivedAt.Should().BeNull();
            }

            [Fact]
            public async Task UnarchiveShareAsync_WhenNotArchived_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

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

                // Act - Try to unarchive without archiving first
                var act = async () => await shareService.UnarchiveShareAsync(share.Id, lender.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share is not archived");
            }

            [Fact]
            public async Task UnarchiveShareAsync_WhenNotLenderOrBorrower_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var unauthorizedUser = TestDataBuilder.CreateUser(id: "unauthorized-user");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, borrower, unauthorizedUser);
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

                // Archive the share
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

                // Act - Try to unarchive as unauthorized user
                var act = async () => await shareService.UnarchiveShareAsync(share.Id, unauthorizedUser.Id);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Only the lender or borrower can unarchive the share");
            }
        }

        public class RaiseDisputeAsyncTests : ShareServiceTestBase
        {
            [Fact]
            public async Task RaiseDisputeAsync_WithValidRequest_SetsDisputeFlags()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");

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
                    status: ShareStatus.PickedUp,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.RaiseDisputeAsync(share.Id, borrower.Id);

                // Assert
                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare.Should().NotBeNull();
                updatedShare!.IsDisputed.Should().BeTrue();
                updatedShare.DisputedBy.Should().Be(borrower.Id);
            }

            [Fact]
            public async Task RaiseDisputeAsync_WithValidRequest_CreatesNotification()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");

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
                    status: ShareStatus.PickedUp,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.RaiseDisputeAsync(share.Id, borrower.Id);

                // Assert
                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.ShareStatusChanged,
                        It.Is<string>(s => s.Contains("dispute") && s.Contains(book.Title)),
                        borrower.Id),
                    Times.Once);
            }

            [Fact]
            public async Task RaiseDisputeAsync_WhenShareNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                // Act
                var act = async () => await shareService.RaiseDisputeAsync(999, "user-1");

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share not found");
            }

            [Fact]
            public async Task RaiseDisputeAsync_WhenUserNotAuthorized_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var unauthorizedUser = TestDataBuilder.CreateUser(id: "unauthorized-1", firstName: "Bob", lastName: "Jones");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");

                context.Users.AddRange(lender, borrower, unauthorizedUser);
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
                    status: ShareStatus.PickedUp,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.RaiseDisputeAsync(share.Id, unauthorizedUser.Id);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Only the lender or borrower can raise a dispute");
            }

            [Fact]
            public async Task RaiseDisputeAsync_WhenAlreadyDisputed_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");

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
                    status: ShareStatus.PickedUp,
                    isDisputed: true,
                    disputedBy: lender.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.RaiseDisputeAsync(share.Id, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Share is already disputed");
            }

            [Fact]
            public async Task RaiseDisputeAsync_WhenStatusIsHomeSafe_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");

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

                // Act
                var act = async () => await shareService.RaiseDisputeAsync(share.Id, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Cannot dispute shares in terminal status (HomeSafe or Declined)");
            }

            [Fact]
            public async Task RaiseDisputeAsync_WhenStatusIsDeclined_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");

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
                    status: ShareStatus.Declined,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await shareService.RaiseDisputeAsync(share.Id, borrower.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Cannot dispute shares in terminal status (HomeSafe or Declined)");
            }

            [Fact]
            public async Task RaiseDisputeAsync_AsLender_SetsDisputeFlags()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1", firstName: "Jane", lastName: "Smith");
                var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");

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
                    status: ShareStatus.Returned,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.RaiseDisputeAsync(share.Id, lender.Id);

                // Assert
                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare.Should().NotBeNull();
                updatedShare!.IsDisputed.Should().BeTrue();
                updatedShare.DisputedBy.Should().Be(lender.Id);
            }
        }

        public class HandleUserBookDeletionAsyncTests : ShareServiceTestBase
        {
            [Fact]
            public async Task HandleUserBookDeletionAsync_WhenUserBookNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                // Act
                var act = async () => await shareService.HandleUserBookDeletionAsync(999, "lender-1");

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("UserBook not found");
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-user");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(lender, otherUser);
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

                // Act
                var act = async () => await shareService.HandleUserBookDeletionAsync(userBook.Id, otherUser.Id);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("Only the owner can delete this book from their library");
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_AutoDeclinesRequestedShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var hadActiveShares = await shareService.HandleUserBookDeletionAsync(userBook.Id, lender.Id);

                // Assert
                hadActiveShares.Should().BeFalse(); // Requested is not considered "active"
                var updatedShare = await context.Shares.FindAsync(share.Id);
                updatedShare.Should().NotBeNull();
                updatedShare!.Status.Should().Be(ShareStatus.Declined);

                // Verify notification was sent
                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.UserBookWithdrawn,
                        It.Is<string>(msg => msg.Contains("was declined") && msg.Contains("removed it from their library")),
                        lender.Id
                    ),
                    Times.Once
                );
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_NotifiesBorrowerForActiveShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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
                    status: ShareStatus.PickedUp,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var hadActiveShares = await shareService.HandleUserBookDeletionAsync(userBook.Id, lender.Id);

                // Assert
                hadActiveShares.Should().BeTrue();

                // Verify notification was sent about withdrawal
                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        NotificationType.UserBookWithdrawn,
                        It.Is<string>(msg => msg.Contains("has been removed") && msg.Contains("coordinate the return")),
                        lender.Id
                    ),
                    Times.Once
                );
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_ArchivesShareForLender()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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
                    status: ShareStatus.Ready,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                await shareService.HandleUserBookDeletionAsync(userBook.Id, lender.Id);

                // Assert
                var archiveState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);
                archiveState.Should().NotBeNull();
                archiveState!.IsArchived.Should().BeTrue();
                archiveState.ArchivedAt.Should().NotBeNull();
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_SilentlyArchivesDisputedShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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
                    status: ShareStatus.PickedUp,
                    isDisputed: true,
                    disputedBy: borrower.Id,
                    userBook: userBook,
                    borrowerUser: borrower
                );
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Act
                var hadActiveShares = await shareService.HandleUserBookDeletionAsync(userBook.Id, lender.Id);

                // Assert
                hadActiveShares.Should().BeFalse(); // Disputed shares are not counted as active

                // Verify no notification was sent for disputed share
                NotificationServiceMock.Verify(
                    x => x.CreateShareNotificationAsync(
                        share.Id,
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()
                    ),
                    Times.Never
                );

                // But should still be archived
                var archiveState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);
                archiveState.Should().NotBeNull();
                archiveState!.IsArchived.Should().BeTrue();
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_ArchivesTerminalStateShares()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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

                // Act
                var hadActiveShares = await shareService.HandleUserBookDeletionAsync(userBook.Id, lender.Id);

                // Assert
                hadActiveShares.Should().BeFalse(); // HomeSafe is terminal, not active

                // Should be archived silently (no notification for terminal states)
                var archiveState = await context.ShareUserStates
                    .FirstOrDefaultAsync(sus => sus.ShareId == share.Id && sus.UserId == lender.Id);
                archiveState.Should().NotBeNull();
                archiveState!.IsArchived.Should().BeTrue();
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_HandlesMultipleSharesCorrectly()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower1 = TestDataBuilder.CreateUser(id: "borrower-1");
                var borrower2 = TestDataBuilder.CreateUser(id: "borrower-2");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

                context.Users.AddRange(lender, borrower1, borrower2);
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

                // One requested share
                var requestedShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower1.Id,
                    status: ShareStatus.Requested,
                    userBook: userBook,
                    borrowerUser: borrower1
                );

                // One terminal share
                var terminalShare = TestDataBuilder.CreateShare(
                    userBookId: userBook.Id,
                    borrower: borrower2.Id,
                    status: ShareStatus.HomeSafe,
                    userBook: userBook,
                    borrowerUser: borrower2
                );

                context.Shares.AddRange(requestedShare, terminalShare);
                await context.SaveChangesAsync();

                // Act
                var hadActiveShares = await shareService.HandleUserBookDeletionAsync(userBook.Id, lender.Id);

                // Assert
                hadActiveShares.Should().BeFalse(); // Neither is active

                // Requested share should be declined
                var updatedRequestedShare = await context.Shares.FindAsync(requestedShare.Id);
                updatedRequestedShare!.Status.Should().Be(ShareStatus.Declined);

                // Both should be archived for lender
                var archiveStates = await context.ShareUserStates
                    .Where(sus => sus.UserId == lender.Id)
                    .ToListAsync();
                archiveStates.Should().HaveCount(2);
                archiveStates.Should().OnlyContain(s => s.IsArchived);
            }

            [Fact]
            public async Task HandleUserBookDeletionAsync_DoesNotDuplicateArchiveState()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var shareService = new ShareService(context, LoggerMock.Object, NotificationServiceMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1", firstName: "John", lastName: "Doe");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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

                // Pre-existing archive state (no navigation properties to avoid tracking conflicts)
                var existingArchiveState = new Models.ShareUserState
                {
                    ShareId = share.Id,
                    UserId = lender.Id,
                    IsArchived = true,
                    ArchivedAt = DateTime.UtcNow.AddDays(-1)
                };
                context.ShareUserStates.Add(existingArchiveState);
                await context.SaveChangesAsync();

                // Act
                await shareService.HandleUserBookDeletionAsync(userBook.Id, lender.Id);

                // Assert - should not create duplicate
                var archiveStates = await context.ShareUserStates
                    .Where(sus => sus.ShareId == share.Id && sus.UserId == lender.Id)
                    .ToListAsync();
                archiveStates.Should().HaveCount(1);
            }
        }
    }
}
