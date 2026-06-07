using BookSharingApp.Services;

namespace BookSharingApp.Endpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this WebApplication app)
        {
            var users = app.MapGroup("/users").WithTags("Users").RequireAuthorization();

            // GET /users/{userId}/reputation?role=borrower|lender
            users.MapGet("/{userId}/reputation", async (string userId, string role, IUserService userService) =>
            {
                if (role != "borrower" && role != "lender")
                    return Results.BadRequest("role must be 'borrower' or 'lender'");

                var reputation = await userService.GetUserReputationAsync(userId, role);
                return Results.Ok(reputation);
            })
            .WithName("GetUserReputation")
            .WithOpenApi();
        }
    }
}
