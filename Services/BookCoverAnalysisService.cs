using Microsoft.EntityFrameworkCore;
using BookSharingApp.Data;
using BookSharingApp.Models;
using BookSharingApp.Services;
using BookSharingApp.Common;
using BookSharingWebAPI.Models;

namespace BookSharingWebAPI.Services;

public class BookCoverAnalysisService : IBookCoverAnalysisService
{
    private readonly ICoverDetectionService _coverDetectionService;
    private readonly IBookLookupService _bookLookupService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BookCoverAnalysisService> _logger;

    public BookCoverAnalysisService(
        ICoverDetectionService coverDetectionService,
        IBookLookupService bookLookupService,
        ApplicationDbContext context,
        ILogger<BookCoverAnalysisService> logger)
    {
        _coverDetectionService = coverDetectionService;
        _bookLookupService = bookLookupService;
        _context = context;
        _logger = logger;
    }

    public async Task<CoverAnalysisResponse> AnalyzeCoverAsync(
        Stream imageStream, string contentType, string requestId, CancellationToken cancellationToken = default)
    {
        var detectionResult = await _coverDetectionService.AnalyzeCoverImageAsync(imageStream, contentType, cancellationToken);

        if (!detectionResult.IsSuccess)
            return FailureResponse(detectionResult.ErrorMessage);

        _logger.LogInformation(
            "Cover detection completed [RequestId={RequestId}, Authors={Authors}, Titles={Titles}]",
            requestId,
            string.Join(", ", detectionResult.PotentialAuthors),
            string.Join(", ", detectionResult.PotentialTitles));

        var scoredBooks = await SearchForMatchingBooksAsync(detectionResult, requestId);

        _logger.LogInformation(
            "Cover analysis completed [RequestId={RequestId}, TotalMatches={TotalMatches}, LocalMatches={LocalMatches}, ExternalMatches={ExternalMatches}]",
            requestId,
            scoredBooks.Count,
            scoredBooks.Count(m => m.book.Id > 0),
            scoredBooks.Count(m => m.book.Id < 0));

        var exactMatch = scoredBooks.FirstOrDefault(m => m.score >= 1.0).book;

        return new CoverAnalysisResponse
        {
            Analysis = new CoverAnalysisSummary
            {
                IsSuccess = true,
                ExtractedText = detectionResult.FullOcrText
            },
            MatchedBooks = scoredBooks.Take(ImageAnalysisConstants.MaxResultsPerResponse).Select(m => m.book).ToList(),
            ExactMatch = exactMatch
        };
    }

    private async Task<List<(Book book, double score)>> SearchForMatchingBooksAsync(
        CoverDetectionResult detectionResult, string requestId)
    {
        var ocrWords = BuildOcrWordSet(detectionResult.PotentialAuthors, detectionResult.PotentialTitles);

        // Author-first: search by each detected author and fuzzy match against detected titles
        foreach (var author in detectionResult.PotentialAuthors)
        {
            _logger.LogInformation(
                "Searching by author [RequestId={RequestId}, Author={Author}]", requestId, author);

            var authorResults = await _bookLookupService.SearchBooksAsync(author: author);
            var scored = ScoreAndFilterMatches(authorResults, ocrWords);

            if (scored.Count > 0)
            {
                _logger.LogInformation(
                    "Author search matched [RequestId={RequestId}, Author={Author}, Matches={Count}]",
                    requestId, author, scored.Count);
                return await MergeWithLocalBooksAsync(scored, ocrWords);
            }
        }

        // Fallback: search by top-ranked title
        if (detectionResult.PotentialTitles.Count > 0)
        {
            var title = detectionResult.PotentialTitles.First();

            _logger.LogInformation(
                "Falling back to title search [RequestId={RequestId}, Title={Title}]", requestId, title);

            var titleResults = await _bookLookupService.SearchBooksByTextAsync(title);
            var scored = ScoreAndFilterMatches(titleResults, ocrWords);

            if (scored.Count > 0)
                return await MergeWithLocalBooksAsync(scored, ocrWords);
        }

        return [];
    }

    private async Task<List<(Book book, double score)>> MergeWithLocalBooksAsync(
        List<BookLookupResult> scoredExternalMatches, HashSet<string> ocrWords)
    {
        var externalTitles = scoredExternalMatches.Select(m => m.Title.ToLower()).ToList();
        var externalAuthors = scoredExternalMatches.Select(m => m.Author.ToLower()).ToList();

        var localMatches = await _context.Books
            .Where(b => externalTitles.Contains(b.Title.ToLower()) ||
                        externalAuthors.Contains(b.Author.ToLower()))
            .ToListAsync();

        var allScoredBooks = new List<(Book book, double score, bool isLocal)>();

        foreach (var localBook in localMatches)
        {
            var score = CalculateWordMatchScore(localBook.Title, localBook.Author, ocrWords);
            if (score >= ImageAnalysisConstants.MinWordMatchThreshold)
                allScoredBooks.Add((localBook, score, true));
        }

        var localTitles = localMatches.Select(b => b.Title.ToLower()).ToHashSet();
        var externalId = -2;
        foreach (var ext in scoredExternalMatches)
        {
            if (!localTitles.Contains(ext.Title.ToLower()))
            {
                var externalBook = new Book
                {
                    Id = externalId--,
                    Title = ext.Title,
                    Author = ext.Author,
                    ExternalThumbnailUrl = ext.ThumbnailUrl
                };
                var score = CalculateWordMatchScore(ext.Title, ext.Author, ocrWords);
                allScoredBooks.Add((externalBook, score, false));
            }
        }

        return allScoredBooks
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.isLocal)
            .Select(x => (x.book, x.score))
            .ToList();
    }

    private static HashSet<string> BuildOcrWordSet(List<string> authors, List<string> titles) =>
        authors
            .Concat(titles)
            .SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(w => w.ToLower().Trim(',', '.', '!', '?', ';', ':'))
            .Where(w => w.Length > 2)
            .ToHashSet();

    private List<BookLookupResult> ScoreAndFilterMatches(
        List<BookLookupResult> candidates, HashSet<string> ocrWords) =>
        candidates
            .Select(match => new
            {
                Book = match,
                Score = CalculateWordMatchScore(match.Title, match.Author, ocrWords)
            })
            .Where(m => m.Score >= ImageAnalysisConstants.MinWordMatchThreshold)
            .OrderByDescending(m => m.Score)
            .Select(m => m.Book)
            .ToList();

    private static double CalculateWordMatchScore(string title, string author, HashSet<string> ocrWords)
    {
        if (ocrWords.Count == 0)
            return 0;

        var titleWords = title
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLower().Trim(',', '.', '!', '?', ';', ':'))
            .Where(w => w.Length > 2)
            .ToList();

        var authorWords = author
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLower().Trim(',', '.', '!', '?', ';', ':'))
            .Where(w => w.Length > 2)
            .ToList();

        var allBookWords = titleWords.Concat(authorWords).ToList();

        if (allBookWords.Count == 0)
            return 0;

        // Full score (title + author words)
        var fullMatchCount = allBookWords.Count(w => ocrWords.Contains(w));
        var fullScore = (double)fullMatchCount / allBookWords.Count;

        // Title-only score — rewards cases where OCR detected only the title (no author words)
        if (titleWords.Count > 0)
        {
            var titleMatchCount = titleWords.Count(w => ocrWords.Contains(w));
            var titleScore = (double)titleMatchCount / titleWords.Count;
            return Math.Max(fullScore, titleScore);
        }

        return fullScore;
    }

    private static CoverAnalysisResponse FailureResponse(string? errorMessage) => new()
    {
        Analysis = new CoverAnalysisSummary
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        }
    };
}
