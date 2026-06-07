using BookSharingApp.Models;

namespace BookSharingApp.Services
{
    public interface IUserService
    {
        Task<UserReputationDto> GetUserReputationAsync(string userId, string role);
    }
}
