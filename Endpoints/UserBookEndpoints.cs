using BookSharingApp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookSharingApp.Endpoints
{
    public static class UserBookEndpoints
    {
        public static void MapUserBookEndpoints(this WebApplication app)
        {
            var userBooks = app.MapGroup("/user-books").WithTags("UserBooks").RequireAuthorization();

            userBooks.MapGet("/user/{userId}", async (string userId, IUserBookService userBookService) =>
            {
                var books = await userBookService.GetUserBooksAsync(userId);
                return Results.Ok(books);
            })
            .WithName("GetUserBooks")
            .WithOpenApi();

            userBooks.MapPost("/", async (
                [FromBody] int bookId,
                HttpContext httpContext,
                IUserBookService userBookService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                try
                {
                    var userBook = await userBookService.AddUserBookAsync(bookId, currentUserId);
                    return Results.Created($"/user-books/{userBook.Id}", userBook);
                }
                catch (InvalidOperationException ex) when (ex.Message == "Book not found")
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (InvalidOperationException ex) when (ex.Message == "User already has this book")
                {
                    return Results.Conflict(ex.Message);
                }
            })
            .WithName("AddUserBook")
            .WithOpenApi();

            userBooks.MapDelete("/{userBookId:int}", async (
                int userBookId,
                [FromQuery] bool confirmed,
                HttpContext httpContext,
                IUserBookService userBookService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                try
                {
                    var result = await userBookService.DeleteUserBookAsync(userBookId, currentUserId, confirmed);

                    if (result.RequiresConfirmation)
                    {
                        return Results.Conflict(new
                        {
                            result.Message,
                            result.RequiresConfirmation
                        });
                    }

                    return Results.NoContent();
                }
                catch (InvalidOperationException ex) when (ex.Message == "UserBook not found")
                {
                    return Results.NotFound();
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            })
            .WithName("DeleteUserBook")
            .WithOpenApi();

            userBooks.MapGet("/search", async (
                string? search,
                HttpContext httpContext,
                IUserBookService userBookService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var results = await userBookService.SearchAccessibleBooksAsync(currentUserId, search);
                return Results.Ok(results);
            })
            .WithName("SearchUserBooks")
            .WithOpenApi();
        }
    }
}
