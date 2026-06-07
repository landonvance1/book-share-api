using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookSharingApp.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserReputationDto> GetUserReputationAsync(string userId, string role)
        {
            IQueryable<Share> query = role == "borrower"
                ? _context.Shares.Where(s => s.Borrower == userId)
                : _context.Shares.Where(s => s.UserBook.UserId == userId);

            var completedCount = await query.CountAsync(s => s.Status == ShareStatus.HomeSafe);
            var disputeCount = await query.CountAsync(s => s.IsDisputed);

            _logger.LogDebug("Reputation for user {UserId} role={Role}: completed={CompletedCount} disputes={DisputeCount}",
                userId, role, completedCount, disputeCount);

            return new UserReputationDto(completedCount, disputeCount);
        }
    }
}
