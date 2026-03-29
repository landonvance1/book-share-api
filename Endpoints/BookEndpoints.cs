using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Models;
using BookSharingApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace BookSharingApp.Endpoints
{
    public static class BookEndpoints
    {
        public static void MapBookEndpoints(this WebApplication app)
        {
            var books = app.MapGroup("/books").WithTags("Books").RequireAuthorization();

            books.MapGet("/", async (ApplicationDbContext context) =>
                await context.Books.ToListAsync())
               .WithName("GetAllBooks")
               .WithOpenApi();

            books.MapGet("/{id:int}", async (int id, ApplicationDbContext context) =>
            {
                var book = await context.Books.FindAsync(id);
                return book is not null ? Results.Ok(book) : Results.NotFound();
            })
            .WithName("GetBookById")
            .WithOpenApi();

            books.MapPost("/", async (Book book, bool addToUser, HttpContext httpContext, ApplicationDbContext context) =>
            {
                // Check if title + author already exists
                var existingBook = await context.Books.FirstOrDefaultAsync(b => b.Title == book.Title && b.Author == book.Author);
                if (existingBook != null)
                {
                    return Results.Conflict($"A book with title '{book.Title}' by '{book.Author}' already exists.");
                }

                book.Id = 0; // Ensure EF generates new ID
                context.Books.Add(book);
                await context.SaveChangesAsync();

                // If addToUser is true, create a UserBook record
                if (addToUser)
                {
                    var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                    var userBook = new UserBook
                    {
                        UserId = currentUserId,
                        BookId = book.Id
                    };

                    context.UserBooks.Add(userBook);
                    await context.SaveChangesAsync();
                }

                // Download external thumbnail if provided
                if (!string.IsNullOrWhiteSpace(book.ExternalThumbnailUrl))
                {
                    try
                    {
                        using var httpClient = new HttpClient();
                        var imageBytes = await httpClient.GetByteArrayAsync(book.ExternalThumbnailUrl);
                        var imagesPath = Path.Combine("wwwroot", "images");
                        Directory.CreateDirectory(imagesPath);
                        var imagePath = Path.Combine(imagesPath, $"{book.Id}.jpg");
                        await File.WriteAllBytesAsync(imagePath, imageBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to download thumbnail for book {book.Id}: {ex.Message}");
                        // Continue without failing the book creation
                    }
                }

                return Results.Created($"/books/{book.Id}", book);
            })
            .WithName("AddBook")
            .WithOpenApi();

            books.MapGet("/search", async (string? title, string? author, bool includeExternal, ApplicationDbContext context, IBookLookupService bookLookupService) =>
            {
                var results = new List<Book>();

                // Search local database only if title or author is provided
                if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(author))
                {
                    var query = context.Books.AsQueryable();

                    if (!string.IsNullOrWhiteSpace(title))
                        query = query.Where(b => b.Title.Contains(title));

                    if (!string.IsNullOrWhiteSpace(author))
                        query = query.Where(b => b.Author.Contains(author));

                    var localResults = await query.ToListAsync();
                    results.AddRange(localResults);
                }

                // If includeExternal is true, search OpenLibrary
                if (includeExternal)
                {
                    var externalResults = await bookLookupService.SearchBooksAsync(null, title, author);

                    // Convert external results to Book objects and merge
                    foreach (var externalBook in externalResults)
                    {
                        // Skip if we already have this book locally (by title and author)
                        bool isDuplicate = results.Any(b =>
                            string.Equals(NormalizeString(b.Title), NormalizeString(externalBook.Title), StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(NormalizeString(b.Author), NormalizeString(externalBook.Author), StringComparison.OrdinalIgnoreCase));

                        if (isDuplicate)
                            continue;

                        // Add external book with negative ID
                        results.Add(new Book
                        {
                            Id = -results.Count - 1,
                            Title = externalBook.Title,
                            Author = externalBook.Author,
                            ExternalThumbnailUrl = externalBook.ThumbnailUrl
                        });
                    }
                }

                // Check if we have too many results (more than 20)
                if (results.Count > Constants.MaxExternalSearchResults)
                {
                    return Results.Ok(new
                    {
                        books = results.Take(Constants.MaxExternalSearchResults).ToList(),
                        hasMore = true,
                        message = "Too many results. Please use more specific filters."
                    });
                }

                return Results.Ok(new
                {
                    books = results,
                    hasMore = false
                });
            })
            .WithName("SearchBooks")
            .WithOpenApi();

            books.MapGet("/isbn/{isbn}", async (string isbn, ApplicationDbContext context, IBookLookupService bookLookupService) =>
            {
                // Search OpenLibrary by ISBN only
                var externalResults = await bookLookupService.SearchBooksAsync(isbn, null, null);

                if (externalResults.Count == 0)
                {
                    return Results.NotFound($"No book found with ISBN: {isbn}");
                }

                if (externalResults.Count > 1)
                {
                    throw new InvalidOperationException($"Multiple books found for ISBN {isbn}. ISBN should be unique.");
                }

                var externalBook = externalResults[0];

                // Check if this book already exists locally by title + author
                var existingBook = await context.Books.FirstOrDefaultAsync(b =>
                    b.Title == externalBook.Title && b.Author == externalBook.Author);

                if (existingBook != null)
                {
                    // Return the existing local book
                    return Results.Ok(existingBook);
                }
                else
                {
                    // Return external book with negative ID
                    var book = new Book
                    {
                        Id = -1,
                        Title = externalBook.Title,
                        Author = externalBook.Author,
                        ExternalThumbnailUrl = externalBook.ThumbnailUrl
                    };

                    return Results.Ok(book);
                }
            })
            .WithName("SearchByIsbn")
            .WithOpenApi();

        }

        private static string NormalizeString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return input.Trim().ToLowerInvariant();
        }

    }
}