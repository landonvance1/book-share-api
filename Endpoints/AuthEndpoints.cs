using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Models;
using BookSharingApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BookSharingApp.Endpoints
{
    public static class AuthEndpoints
    {
        public static void MapAuthEndpoints(this WebApplication app)
        {
            var auth = app.MapGroup("/auth").WithTags("Authentication");

            auth.MapPost("/register", RegisterAsync)
                .WithName("Register")
                .WithSummary("Register a new user")
                .WithMetadata(new RateLimitAttribute(RateLimitNames.AuthRegister, RateLimitScope.Ip))
                .Produces<AuthResponseDto>()
                .ProducesValidationProblem();

            auth.MapPost("/login", LoginAsync)
                .WithName("Login")
                .WithSummary("Login with email and password")
                .WithMetadata(new RateLimitAttribute(RateLimitNames.AuthLogin, RateLimitScope.Ip))
                .Produces<AuthResponseDto>()
                .ProducesValidationProblem();

            auth.MapPost("/refresh", RefreshTokenAsync)
                .WithName("RefreshToken")
                .WithSummary("Refresh access token")
                .WithMetadata(new RateLimitAttribute(RateLimitNames.AuthRefresh, RateLimitScope.Ip))
                .Produces<AuthResponseDto>()
                .ProducesValidationProblem();

            auth.MapDelete("/account", DeleteAccountAsync)
                .WithName("DeleteAccount")
                .WithSummary("Permanently delete the authenticated user's account and all associated personal data")
                .RequireAuthorization()
                .Produces(StatusCodes.Status204NoContent);
        }

        private static async Task<IResult> DeleteAccountAsync(
            HttpContext httpContext,
            IUserDeletionService userDeletionService)
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            await userDeletionService.DeleteUserAsync(userId);
            return Results.NoContent();
        }

        private static async Task<IResult> RegisterAsync(
            [FromBody] RegisterDto request,
            UserManager<User> userManager,
            ITokenService tokenService,
            ApplicationDbContext context)
        {
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
            {
                var errors = validationResults.ToDictionary(
                    vr => vr.MemberNames.FirstOrDefault() ?? "Error",
                    vr => new[] { vr.ErrorMessage ?? "Validation error" });
                return Results.ValidationProblem(errors);
            }

            var existingUser = await userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return Results.BadRequest(new { message = "User with this email already exists" });

            var user = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { message = "Registration failed", errors = result.Errors });

            var token = tokenService.GenerateAccessToken(user);
            var refreshToken = new RefreshToken
            {
                Token = tokenService.GenerateRefreshToken(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            context.RefreshTokens.Add(refreshToken);
            await context.SaveChangesAsync();

            return Results.Ok(new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken.Token,
                Expires = DateTime.UtcNow.AddHours(24),
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.FullName
                }
            });
        }

        private static async Task<IResult> LoginAsync(
            [FromBody] LoginDto request,
            UserManager<User> userManager,
            ITokenService tokenService,
            ApplicationDbContext context)
        {
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
            {
                var errors = validationResults.ToDictionary(
                    vr => vr.MemberNames.FirstOrDefault() ?? "Error",
                    vr => new[] { vr.ErrorMessage ?? "Validation error" });
                return Results.ValidationProblem(errors);
            }

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Results.BadRequest(new { message = "Invalid email or password" });

            var isPasswordValid = await userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid)
                return Results.BadRequest(new { message = "Invalid email or password" });

            var token = tokenService.GenerateAccessToken(user);
            var refreshToken = new RefreshToken
            {
                Token = tokenService.GenerateRefreshToken(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            context.RefreshTokens.Add(refreshToken);
            await context.SaveChangesAsync();

            return Results.Ok(new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken.Token,
                Expires = DateTime.UtcNow.AddHours(24),
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.FullName
                }
            });
        }

        private static async Task<IResult> RefreshTokenAsync(
            [FromBody] RefreshTokenDto request,
            UserManager<User> userManager,
            ITokenService tokenService,
            ApplicationDbContext context)
        {
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
            {
                var errors = validationResults.ToDictionary(
                    vr => vr.MemberNames.FirstOrDefault() ?? "Error",
                    vr => new[] { vr.ErrorMessage ?? "Validation error" });
                return Results.ValidationProblem(errors);
            }

            var storedRefreshToken = await context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (storedRefreshToken == null || !storedRefreshToken.IsActive)
                return Results.BadRequest(new { message = "Invalid or expired refresh token" });

            // Revoke the old refresh token
            storedRefreshToken.RevokedAt = DateTime.UtcNow;

            // Generate new tokens
            var newAccessToken = tokenService.GenerateAccessToken(storedRefreshToken.User);
            var newRefreshToken = new RefreshToken
            {
                Token = tokenService.GenerateRefreshToken(),
                UserId = storedRefreshToken.UserId,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            context.RefreshTokens.Add(newRefreshToken);
            await context.SaveChangesAsync();

            return Results.Ok(new AuthResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                Expires = DateTime.UtcNow.AddHours(24),
                User = new UserDto
                {
                    Id = storedRefreshToken.User.Id,
                    Email = storedRefreshToken.User.Email!,
                    FirstName = storedRefreshToken.User.FirstName,
                    LastName = storedRefreshToken.User.LastName,
                    FullName = storedRefreshToken.User.FullName
                }
            });
        }
    }
}