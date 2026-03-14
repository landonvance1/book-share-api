using Microsoft.EntityFrameworkCore;
using BookSharingApp.Data;
using BookSharingApp.Models;
using BookSharingApp.Services;
using BookSharingApp.Common;
using BookSharingWebAPI.Models;
using FuzzySharp;

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
        var topAuthor = detectionResult.PotentialAuthors.FirstOrDefault();
        var topTitle = detectionResult.PotentialTitles.FirstOrDefault();

        List<BookLookupResult>? authorResults = null;
        List<BookLookupResult>? titleResults = null;

        // Pass 1: author-only structured query, recovers when NLP title is too garbled for a combined hit
        if (topAuthor != null)
        {
            _logger.LogInformation(
                "Searching by author [RequestId={RequestId}, Author={Author}]", requestId, topAuthor);

            authorResults = await _bookLookupService.SearchBooksAsync(author: topAuthor);
            var scored = ScoreAndFilterMatches(authorResults, topTitle, topAuthor);

            if (scored.Count > 0)
            {
                _logger.LogInformation(
                    "Author search matched [RequestId={RequestId}, Matches={Count}]",
                    requestId, scored.Count);
                return await MergeWithLocalBooksAsync(scored, topTitle, topAuthor);
            }
        }

        // Pass 2: title-only text query — widest fallback, used when no author was detected or author searches failed
        if (topTitle != null)
        {
            _logger.LogInformation(
                "Falling back to title-only text search [RequestId={RequestId}, Title={Title}]", requestId, topTitle);

            titleResults = await _bookLookupService.SearchBooksByTextAsync(topTitle);
            var scored = ScoreAndFilterMatches(titleResults, topTitle, topAuthor);

            if (scored.Count > 0)
                return await MergeWithLocalBooksAsync(scored, topTitle, topAuthor);
        }

        // Pass 3: best-guess — no new lookups, pick the single highest-scoring candidate
        // from prior fetches regardless of threshold
        var allPriorResults = (authorResults ?? []).Concat(titleResults ?? []).ToList();
        if (allPriorResults.Count > 0)
        {
            _logger.LogInformation(
                "Falling back to best-guess from prior results [RequestId={RequestId}]", requestId);

            var bestGuess = allPriorResults
                .Select(r => (result: r, score: CalculateWordMatchScore(r.Title, r.Author, topTitle, topAuthor)))
                .OrderByDescending(x => x.score)
                .First();

            if (bestGuess.score > 0)
            {
                _logger.LogInformation(
                    "Best-guess match found [RequestId={RequestId}, Title={Title}, Score={Score}]",
                    requestId, bestGuess.result.Title, bestGuess.score);
                return await MergeWithLocalBooksAsync([bestGuess.result], topTitle, topAuthor);
            }
        }

        return [];
    }

    private async Task<List<(Book book, double score)>> MergeWithLocalBooksAsync(
        List<BookLookupResult> scoredExternalMatches, string? ocrTitle, string? ocrAuthor)
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
            var score = CalculateWordMatchScore(localBook.Title, localBook.Author, ocrTitle, ocrAuthor);
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
                var score = CalculateWordMatchScore(ext.Title, ext.Author, ocrTitle, ocrAuthor);
                allScoredBooks.Add((externalBook, score, false));
            }
        }

        return allScoredBooks
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.isLocal)
            .Select(x => (x.book, x.score))
            .ToList();
    }

    private List<BookLookupResult> ScoreAndFilterMatches(
        List<BookLookupResult> candidates, string? ocrTitle, string? ocrAuthor) =>
        candidates
            .Select(match => new
            {
                Book = match,
                Score = CalculateWordMatchScore(match.Title, match.Author, ocrTitle, ocrAuthor)
            })
            .Where(m => m.Score >= ImageAnalysisConstants.MinWordMatchThreshold)
            .OrderByDescending(m => m.Score)
            .Select(m => m.Book)
            .ToList();

    private static double CalculateWordMatchScore(
        string title, string author, string? ocrTitle, string? ocrAuthor)
    {
        var bookTitleWords = ParseWords(title);
        var bookAuthorWords = ParseWords(author);

        var titleScore = ocrTitle != null
            ? ScoreWordBucket(bookTitleWords, ParseWords(ocrTitle))
            : (double?)null;

        var authorScore = ocrAuthor != null
            ? ScoreWordBucket(bookAuthorWords, ParseWords(ocrAuthor))
            : (double?)null;

        if (titleScore == null && authorScore == null) return 0;
        if (authorScore == null) return titleScore!.Value;    // title-only OCR: full credit
        if (titleScore == null) return authorScore.Value / 2; // author-only OCR: half credit
        return (titleScore.Value + authorScore.Value) / 2;
    }

    private static double ScoreWordBucket(List<string> bookWords, List<string> ocrWords)
    {
        if (bookWords.Count == 0) return 0;
        var remaining = ocrWords.ToList();
        return (double)CountAndConsume(bookWords, remaining) / bookWords.Count;
    }

    private static List<string> ParseWords(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLower().Trim(',', '.', '!', '?', ';', ':'))
            .Where(w => w.Length > 2)
            .ToList();

    private static int CountAndConsume(List<string> bookWords, List<string> remainingOcrWords)
    {
        var count = 0;
        foreach (var bookWord in bookWords)
        {
            var idx = FindFuzzyMatchIndex(bookWord, remainingOcrWords);
            if (idx >= 0)
            {
                count++;
                remainingOcrWords.RemoveAt(idx);
            }
        }
        return count;
    }

    private static int FindFuzzyMatchIndex(string bookWord, List<string> ocrWords)
    {
        for (var i = 0; i < ocrWords.Count; i++)
        {
            if (ocrWords[i] == bookWord ||
                Fuzz.Ratio(bookWord, ocrWords[i]) >= ImageAnalysisConstants.FuzzyWordMatchThreshold)
                return i;
        }
        return -1;
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
