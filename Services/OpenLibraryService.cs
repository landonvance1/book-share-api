using System.Text.Json;
using System.Text.Json.Serialization;
using BookSharingApp.Common;

namespace BookSharingApp.Services
{
    public class OpenLibraryService : IBookLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenLibraryService> _logger;

        public OpenLibraryService(HttpClient httpClient, ILogger<OpenLibraryService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<BookLookupResult?> GetBookByIsbnAsync(string isbn)
        {
            try
            {
                var cleanIsbn = CleanIsbn(isbn);
                var response = await _httpClient.GetAsync($"https://openlibrary.org/search.json?isbn={cleanIsbn}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenLibrary API returned {StatusCode} for ISBN {ISBN}", response.StatusCode, cleanIsbn);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                if (searchResult?.Docs?.Any() != true)
                {
                    _logger.LogInformation("No books found for ISBN {ISBN}", cleanIsbn);
                    return null;
                }

                var book = searchResult.Docs.First();
                return new BookLookupResult
                {
                    Title = book.Title ?? "Unknown Title",
                    Author = book.AuthorName?.FirstOrDefault() ?? "Unknown Author",
                    Isbn = cleanIsbn,
                    ThumbnailUrl = book.CoverId != null ? $"https://covers.openlibrary.org/b/id/{book.CoverId}-M.jpg" : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching book data from OpenLibrary for ISBN {ISBN}", isbn);
                return null;
            }
        }

        public async Task<List<BookLookupResult>> SearchBooksAsync(string? isbn = null, string? title = null, string? author = null)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(isbn))
                    queryParams.Add($"isbn={Uri.EscapeDataString(CleanIsbn(isbn))}");

                if (!string.IsNullOrWhiteSpace(title))
                    queryParams.Add($"title={Uri.EscapeDataString(title)}");

                if (!string.IsNullOrWhiteSpace(author))
                    queryParams.Add($"author={Uri.EscapeDataString(author)}");

                if (!queryParams.Any())
                    return new List<BookLookupResult>();

                var queryString = string.Join("&", queryParams);
                var response = await _httpClient.GetAsync($"https://openlibrary.org/search.json?{queryString}&limit={Constants.MaxExternalSearchResults + 1}&fields=title,author_name,cover_i,isbn");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenLibrary API returned {StatusCode} for search query {Query}", response.StatusCode, queryString);
                    return new List<BookLookupResult>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                if (searchResult?.Docs?.Any() != true)
                {
                    _logger.LogInformation("No books found for search query {Query}", queryString);
                    return new List<BookLookupResult>();
                }

                return searchResult.Docs.Select(book => new BookLookupResult
                {
                    Title = book.Title ?? "Unknown Title",
                    Author = book.AuthorName?.FirstOrDefault() ?? "Unknown Author",
                    Isbn = book.Isbn?.FirstOrDefault() ?? string.Empty,
                    ThumbnailUrl = book.CoverId != null ? $"https://covers.openlibrary.org/b/id/{book.CoverId}-M.jpg" : null
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching books from OpenLibrary with ISBN: {ISBN}, Title: {Title}, Author: {Author}", isbn, title, author);
                return new List<BookLookupResult>();
            }
        }

        public async Task<List<BookLookupResult>> SearchBooksByTextAsync(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                    return new List<BookLookupResult>();

                // Use OpenLibrary's general query parameter which searches across all fields
                var encodedQuery = Uri.EscapeDataString(searchText);
                var response = await _httpClient.GetAsync(
                    $"https://openlibrary.org/search.json?q={encodedQuery}&limit={Constants.MaxExternalSearchResults + 1}&fields=title,author_name,cover_i,isbn");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenLibrary API returned {StatusCode} for text query: {Query}",
                        response.StatusCode, searchText);
                    return new List<BookLookupResult>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(jsonContent,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                if (searchResult?.Docs?.Any() != true)
                {
                    _logger.LogInformation("No books found for text query: {Query}", searchText);
                    return new List<BookLookupResult>();
                }

                return searchResult.Docs.Select(book => new BookLookupResult
                {
                    Title = book.Title ?? "Unknown Title",
                    Author = book.AuthorName?.FirstOrDefault() ?? "Unknown Author",
                    Isbn = book.Isbn?.FirstOrDefault() ?? string.Empty,
                    ThumbnailUrl = book.CoverId != null
                        ? $"https://covers.openlibrary.org/b/id/{book.CoverId}-M.jpg"
                        : null
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching books from OpenLibrary by text: {SearchText}", searchText);
                return new List<BookLookupResult>();
            }
        }

        private static string CleanIsbn(string isbn)
        {
            return isbn.Replace("-", "").Replace(" ", "");
        }
    }

    internal class OpenLibrarySearchResponse
    {
        public int NumFound { get; set; }
        public int Start { get; set; }
        public List<OpenLibraryBook>? Docs { get; set; }
    }

    internal class OpenLibraryBook
    {
        public string? Title { get; set; }
        public List<string>? AuthorName { get; set; }
        public List<string>? Isbn { get; set; }

        [JsonPropertyName("cover_i")]
        public int? CoverId { get; set; }

        public int? FirstPublishYear { get; set; }
    }
}