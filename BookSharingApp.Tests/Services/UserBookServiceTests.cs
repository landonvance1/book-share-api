using BookSharingApp.Common;
using BookSharingApp.Services;
using BookSharingApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.Tests.Services
{
    public class UserBookServiceTests
    {
        public abstract class UserBookServiceTestBase : IDisposable
        {
            protected readonly Mock<ILogger<UserBookService>> LoggerMock;
            protected readonly Mock<IShareService> ShareServiceMock;

            protected UserBookServiceTestBase()
            {
                LoggerMock = new Mock<ILogger<UserBookService>>();
                ShareServiceMock = new Mock<IShareService>();
            }

            public void Dispose()
            {
                // Cleanup if needed
            }
        }

        public class GetUserBooksAsyncTests : UserBookServiceTestBase
        {
            [Fact]
            public async Task GetUserBooksAsync_ReturnsOnlyNonDeletedBooks()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book1 = TestDataBuilder.CreateBook(title: "Active Book");
                var book2 = TestDataBuilder.CreateBook(title: "Deleted Book");

                context.Users.Add(user);
                context.Books.AddRange(book1, book2);
                await context.SaveChangesAsync();

                var activeUserBook = TestDataBuilder.CreateUserBook(
                    userId: user.Id,
                    bookId: book1.Id,
                    isDeleted: false,
                    user: user,
                    book: book1
                );
                var deletedUserBook = TestDataBuilder.CreateUserBook(
                    userId: user.Id,
                    bookId: book2.Id,
                    isDeleted: true,
                    deletedAt: DateTime.UtcNow,
                    user: user,
                    book: book2
                );

                context.UserBooks.AddRange(activeUserBook, deletedUserBook);
                await context.SaveChangesAsync();

                // Act
                var result = await service.GetUserBooksAsync(user.Id);

                // Assert
                result.Should().HaveCount(1);
                result[0].Id.Should().Be(activeUserBook.Id);
                result[0].Book.Title.Should().Be("Active Book");
            }

            [Fact]
            public async Task GetUserBooksAsync_ReturnsEmptyListForUserWithNoBooks()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Act
                var result = await service.GetUserBooksAsync(user.Id);

                // Assert
                result.Should().BeEmpty();
            }
        }

        public class AddUserBookAsyncTests : UserBookServiceTestBase
        {
            [Fact]
            public async Task AddUserBookAsync_WithValidBook_CreatesUserBook()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book = TestDataBuilder.CreateBook(title: "New Book");

                context.Users.Add(user);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                // Act
                var result = await service.AddUserBookAsync(book.Id, user.Id);

                // Assert
                result.Should().NotBeNull();
                result.UserId.Should().Be(user.Id);
                result.BookId.Should().Be(book.Id);
                result.IsDeleted.Should().BeFalse();
            }

            [Fact]
            public async Task AddUserBookAsync_WhenBookNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                context.Users.Add(user);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await service.AddUserBookAsync(999, user.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Book not found");
            }

            [Fact]
            public async Task AddUserBookAsync_WhenUserAlreadyHasBook_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book = TestDataBuilder.CreateBook(title: "Existing Book");

                context.Users.Add(user);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var existingUserBook = TestDataBuilder.CreateUserBook(
                    userId: user.Id,
                    bookId: book.Id,
                    user: user,
                    book: book
                );
                context.UserBooks.Add(existingUserBook);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await service.AddUserBookAsync(book.Id, user.Id);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("User already has this book");
            }

            [Fact]
            public async Task AddUserBookAsync_WhenSoftDeletedBookExists_ReactivatesBook()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book = TestDataBuilder.CreateBook(title: "Deleted Book");

                context.Users.Add(user);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var deletedUserBook = TestDataBuilder.CreateUserBook(
                    userId: user.Id,
                    bookId: book.Id,
                    isDeleted: true,
                    deletedAt: DateTime.UtcNow.AddDays(-1),
                    user: user,
                    book: book
                );
                context.UserBooks.Add(deletedUserBook);
                await context.SaveChangesAsync();

                // Act
                var result = await service.AddUserBookAsync(book.Id, user.Id);

                // Assert
                result.Should().NotBeNull();
                result.Id.Should().Be(deletedUserBook.Id);
                result.IsDeleted.Should().BeFalse();
                result.DeletedAt.Should().BeNull();
            }
        }

        public class DeleteUserBookAsyncTests : UserBookServiceTestBase
        {
            [Fact]
            public async Task DeleteUserBookAsync_WithNoActiveShares_SoftDeletesBook()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book = TestDataBuilder.CreateBook(title: "Test Book");

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
                var result = await service.DeleteUserBookAsync(userBook.Id, user.Id, confirmed: false);

                // Assert
                result.Success.Should().BeTrue();
                result.RequiresConfirmation.Should().BeFalse();

                var deletedBook = await context.UserBooks.FindAsync(userBook.Id);
                deletedBook!.IsDeleted.Should().BeTrue();
                deletedBook.DeletedAt.Should().NotBeNull();
            }

            [Fact]
            public async Task DeleteUserBookAsync_WithActiveShares_RequiresConfirmation()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Shared Book");

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
                var result = await service.DeleteUserBookAsync(userBook.Id, lender.Id, confirmed: false);

                // Assert
                result.Success.Should().BeFalse();
                result.RequiresConfirmation.Should().BeTrue();
                result.Message.Should().Contain("Shared Book");
                result.Message.Should().Contain("has an active share");

                // Book should NOT be deleted yet
                var userBookAfter = await context.UserBooks.FindAsync(userBook.Id);
                userBookAfter!.IsDeleted.Should().BeFalse();
            }

            [Fact]
            public async Task DeleteUserBookAsync_WithActiveSharesAndConfirmed_DeletesBook()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                var book = TestDataBuilder.CreateBook(title: "Shared Book");

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
                var result = await service.DeleteUserBookAsync(userBook.Id, lender.Id, confirmed: true);

                // Assert
                result.Success.Should().BeTrue();
                result.RequiresConfirmation.Should().BeFalse();

                ShareServiceMock.Verify(
                    x => x.HandleUserBookDeletionAsync(userBook.Id, lender.Id),
                    Times.Once
                );

                var deletedBook = await context.UserBooks.FindAsync(userBook.Id);
                deletedBook!.IsDeleted.Should().BeTrue();
            }

            [Fact]
            public async Task DeleteUserBookAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var owner = TestDataBuilder.CreateUser(id: "owner-1");
                var otherUser = TestDataBuilder.CreateUser(id: "other-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.AddRange(owner, otherUser);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: owner.Id,
                    bookId: book.Id,
                    user: owner,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await service.DeleteUserBookAsync(userBook.Id, otherUser.Id, false);

                // Assert
                await act.Should().ThrowAsync<UnauthorizedAccessException>()
                    .WithMessage("You do not own this book");
            }

            [Fact]
            public async Task DeleteUserBookAsync_WhenAlreadyDeleted_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book = TestDataBuilder.CreateBook();

                context.Users.Add(user);
                context.Books.Add(book);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook(
                    userId: user.Id,
                    bookId: book.Id,
                    isDeleted: true,
                    deletedAt: DateTime.UtcNow,
                    user: user,
                    book: book
                );
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                // Act
                var act = async () => await service.DeleteUserBookAsync(userBook.Id, user.Id, false);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Book has already been removed from your library");
            }

            [Fact]
            public async Task DeleteUserBookAsync_WhenNotFound_ThrowsInvalidOperationException()
            {
                // Arrange
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserBookService(context, ShareServiceMock.Object, LoggerMock.Object);

                // Act
                var act = async () => await service.DeleteUserBookAsync(999, "user-1", false);

                // Assert
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("UserBook not found");
            }
        }
    }
}
