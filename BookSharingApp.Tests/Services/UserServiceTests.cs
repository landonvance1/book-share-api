using BookSharingApp.Common;
using BookSharingApp.Services;
using BookSharingApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.Tests.Services
{
    public class UserServiceTests
    {
        public abstract class UserServiceTestBase : IDisposable
        {
            protected readonly Mock<ILogger<UserService>> LoggerMock;

            protected UserServiceTestBase()
            {
                LoggerMock = new Mock<ILogger<UserService>>();
            }

            public void Dispose() { }
        }

        public class GetUserReputationAsyncTests : UserServiceTestBase
        {
            [Fact]
            public async Task GetUserReputationAsync_ReturnsZerosWhenUserHasNoShares()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var result = await service.GetUserReputationAsync("unknown-user", "borrower");

                result.CompletedCount.Should().Be(0);
                result.DisputeCount.Should().Be(0);
            }

            [Fact]
            public async Task GetUserReputationAsync_BorrowerRole_CountsHomeSafeSharesAsCompleted()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.Add(borrower);
                await context.SaveChangesAsync();

                // Share one UserBook instance across both shares so EF doesn't get a duplicate stub
                var userBook = TestDataBuilder.CreateUserBook();
                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, userBook: userBook, status: ShareStatus.HomeSafe));
                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, userBook: userBook, status: ShareStatus.HomeSafe));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(borrower.Id, "borrower");

                result.CompletedCount.Should().Be(2);
                result.DisputeCount.Should().Be(0);
            }

            [Fact]
            public async Task GetUserReputationAsync_BorrowerRole_CountsIsDisputedSharesAsDisputes()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.Add(borrower);
                await context.SaveChangesAsync();

                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, isDisputed: true));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(borrower.Id, "borrower");

                result.CompletedCount.Should().Be(0);
                result.DisputeCount.Should().Be(1);
            }

            [Fact]
            public async Task GetUserReputationAsync_BorrowerRole_DoesNotCountInProgressShares()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.Add(borrower);
                await context.SaveChangesAsync();

                var userBook = TestDataBuilder.CreateUserBook();
                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, userBook: userBook, status: ShareStatus.Requested));
                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, userBook: userBook, status: ShareStatus.Ready));
                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, userBook: userBook, status: ShareStatus.PickedUp));
                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, userBook: userBook, status: ShareStatus.Returned));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(borrower.Id, "borrower");

                result.CompletedCount.Should().Be(0);
                result.DisputeCount.Should().Be(0);
            }

            [Fact]
            public async Task GetUserReputationAsync_BorrowerRole_DoesNotCountDeclinedShares()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.Add(borrower);
                await context.SaveChangesAsync();

                context.Shares.Add(TestDataBuilder.CreateShare(borrowerUser: borrower, status: ShareStatus.Declined));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(borrower.Id, "borrower");

                result.CompletedCount.Should().Be(0);
                result.DisputeCount.Should().Be(0);
            }

            [Fact]
            public async Task GetUserReputationAsync_BorrowerRole_ArchivedHomeSafeSharesStillCount()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var borrower = TestDataBuilder.CreateUser(id: "borrower-1");
                context.Users.Add(borrower);
                await context.SaveChangesAsync();

                var share = TestDataBuilder.CreateShare(borrowerUser: borrower, status: ShareStatus.HomeSafe);
                context.Shares.Add(share);
                await context.SaveChangesAsync();

                // Archive the share — reputation count must be unaffected
                context.ShareUserStates.Add(TestDataBuilder.CreateShareUserState(
                    shareId: share.Id,
                    share: share,
                    userId: borrower.Id,
                    user: borrower,
                    isArchived: true,
                    archivedAt: DateTime.UtcNow
                ));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(borrower.Id, "borrower");

                result.CompletedCount.Should().Be(1);
            }

            [Fact]
            public async Task GetUserReputationAsync_LenderRole_CountsHomeSafeSharesAsCompleted()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var book = TestDataBuilder.CreateBook();
                var userBook = TestDataBuilder.CreateUserBook(userId: lender.Id, user: lender, book: book);

                context.Users.Add(lender);
                context.Books.Add(book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                // Use distinct borrower IDs so EF doesn't conflict on BorrowerUser stubs
                context.Shares.Add(TestDataBuilder.CreateShare(
                    userBookId: userBook.Id, userBook: userBook,
                    borrower: "borrower-1", status: ShareStatus.HomeSafe));
                context.Shares.Add(TestDataBuilder.CreateShare(
                    userBookId: userBook.Id, userBook: userBook,
                    borrower: "borrower-2", status: ShareStatus.HomeSafe));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(lender.Id, "lender");

                result.CompletedCount.Should().Be(2);
                result.DisputeCount.Should().Be(0);
            }

            [Fact]
            public async Task GetUserReputationAsync_LenderRole_CountsIsDisputedSharesAsDisputes()
            {
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var lender = TestDataBuilder.CreateUser(id: "lender-1");
                var book = TestDataBuilder.CreateBook();
                var userBook = TestDataBuilder.CreateUserBook(userId: lender.Id, user: lender, book: book);

                context.Users.Add(lender);
                context.Books.Add(book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                context.Shares.Add(TestDataBuilder.CreateShare(
                    userBookId: userBook.Id, userBook: userBook,
                    borrower: "borrower-1", isDisputed: true));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(lender.Id, "lender");

                result.CompletedCount.Should().Be(0);
                result.DisputeCount.Should().Be(1);
            }

            [Fact]
            public async Task GetUserReputationAsync_BorrowerStatsDoNotIncludeLenderShares()
            {
                // A user's HomeSafe shares as lender must not appear in their borrower reputation
                using var context = DbContextHelper.CreateInMemoryContext();
                var service = new UserService(context, LoggerMock.Object);

                var user = TestDataBuilder.CreateUser(id: "user-1");
                var book = TestDataBuilder.CreateBook();
                var userBook = TestDataBuilder.CreateUserBook(userId: user.Id, user: user, book: book);

                context.Users.Add(user);
                context.Books.Add(book);
                context.UserBooks.Add(userBook);
                await context.SaveChangesAsync();

                // Share where user is the lender
                context.Shares.Add(TestDataBuilder.CreateShare(
                    userBookId: userBook.Id, userBook: userBook,
                    borrower: "borrower-1", status: ShareStatus.HomeSafe));
                await context.SaveChangesAsync();

                var result = await service.GetUserReputationAsync(user.Id, "borrower");

                result.CompletedCount.Should().Be(0);
                result.DisputeCount.Should().Be(0);
            }
        }
    }
}
