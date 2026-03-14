using BookSharingApp.Models;
using BookSharingApp.Services;
using BookSharingApp.Tests.Helpers;
using BookSharingWebAPI.Models;
using BookSharingWebAPI.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.Tests.Services
{
    public class BookCoverAnalysisServiceTests
    {
        public abstract class BookCoverAnalysisServiceTestBase : IDisposable
        {
            protected readonly Mock<ICoverDetectionService> CoverDetectionServiceMock;
            protected readonly Mock<IBookLookupService> BookLookupServiceMock;
            protected readonly BookCoverAnalysisService Service;
            private readonly BookSharingApp.Data.ApplicationDbContext _context;

            protected BookCoverAnalysisServiceTestBase()
            {
                CoverDetectionServiceMock = new Mock<ICoverDetectionService>();
                BookLookupServiceMock = new Mock<IBookLookupService>();
                _context = DbContextHelper.CreateInMemoryContext();

                Service = new BookCoverAnalysisService(
                    CoverDetectionServiceMock.Object,
                    BookLookupServiceMock.Object,
                    _context,
                    new Mock<ILogger<BookCoverAnalysisService>>().Object);
            }

            /// <summary>
            /// Seeds a book into the in-memory database.
            /// </summary>
            protected void SeedBook(int id, string title, string author)
            {
                _context.Books.Add(new Book { Id = id, Title = title, Author = author });
                _context.SaveChanges();
            }

            /// <summary>
            /// Sets up ICoverDetectionService to return a successful result with the given authors and titles.
            /// </summary>
            protected void SetupDetectionResult(string[] authors, string[] titles)
            {
                var result = new CoverDetectionResult
                {
                    AnalysisStatus = new CoverDetectionStatus { IsSuccess = true },
                    OcrResult = new CoverDetectionOcrResult
                    {
                        Text = string.Join(" ", authors.Concat(titles))
                    },
                    NlpAnalysis = new CoverDetectionNlpAnalysis
                    {
                        PotentialAuthors = authors.ToList(),
                        PotentialTitles = titles.ToList()
                    }
                };

                CoverDetectionServiceMock
                    .Setup(s => s.AnalyzeCoverImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(result);
            }

            /// <summary>
            /// Sets up ICoverDetectionService to return a failure result.
            /// </summary>
            protected void SetupDetectionFailure(string errorMessage)
            {
                CoverDetectionServiceMock
                    .Setup(s => s.AnalyzeCoverImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(CoverDetectionResult.Failure(errorMessage));
            }

            public void Dispose() => _context.Dispose();
        }

        public class ExactMatchTests : BookCoverAnalysisServiceTestBase
        {
            [Fact]
            public async Task AnalyzeCoverAsync_WhenBookMatchesAllNlpWords_SetsExactMatch()
            {
                // Arrange — NLP extracts author and title that match the book exactly
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: ["Mistborn"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync([new BookLookupResult
                    {
                        Title = "Mistborn",
                        Author = "Brandon Sanderson"
                    }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert
                result.ExactMatch.Should().NotBeNull();
                result.ExactMatch!.Title.Should().Be("Mistborn");
                result.ExactMatch.Author.Should().Be("Brandon Sanderson");
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenNlpIsMissingBookWords_DoesNotSetExactMatch()
            {
                // Arrange — NLP has author but title is missing.
                // ocrTitle = null (no titles detected), ocrAuthor = "Brandon Sanderson"
                // authorScore = 1.0, but author-only OCR gets half credit → combined = 0.5 < 0.8
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: []);

                // No titles → title-only pass skipped; author-only structured search runs first
                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync([new BookLookupResult
                    {
                        Title = "Mistborn",
                        Author = "Brandon Sanderson"
                    }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — author-only OCR with no title yields score = 0.5, below 0.8 threshold
                // so no exact match; Phase 3 best-guess still surfaces the result in MatchedBooks
                result.ExactMatch.Should().BeNull();
                result.MatchedBooks.Should().ContainSingle(b => b.Title == "Mistborn");
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenNoMatchesFound_DoesNotSetExactMatch()
            {
                // Arrange — both search passes return no results
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: ["Mistborn"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync(new List<BookLookupResult>());

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksByTextAsync("Mistborn"))
                    .ReturnsAsync(new List<BookLookupResult>());

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert
                result.ExactMatch.Should().BeNull();
                result.MatchedBooks.Should().BeEmpty();
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenLocalBookMatchesAllNlpWords_SetsExactMatchToLocalBook()
            {
                // Arrange — book exists in local DB
                SeedBook(id: 1, title: "Mistborn", author: "Brandon Sanderson");

                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: ["Mistborn"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync([new BookLookupResult
                    {
                        Title = "Mistborn",
                        Author = "Brandon Sanderson"
                    }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert
                result.ExactMatch.Should().NotBeNull();
                result.ExactMatch!.Id.Should().Be(1); // Local book preferred
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenMultipleBooksReturnedButOneIsExact_SetsExactMatchToHighestScore()
            {
                // Arrange — two books returned, only one is an exact match
                SetupDetectionResult(
                    authors: ["Frank Herbert"],
                    titles: ["Dune"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Frank Herbert"))
                    .ReturnsAsync([
                        new BookLookupResult { Title = "Dune", Author = "Frank Herbert" },
                        new BookLookupResult { Title = "Dune Messiah", Author = "Frank Herbert" }
                    ]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — "Dune Messiah" scores 0.75 (below 0.8 threshold), only "Dune" passes
                result.ExactMatch.Should().NotBeNull();
                result.ExactMatch!.Title.Should().Be("Dune");
            }
        }

        public class SearchPassTests : BookCoverAnalysisServiceTestBase
        {
            [Fact]
            public async Task AnalyzeCoverAsync_WhenBothAuthorAndTitleDetected_SearchesByAuthorFirst()
            {
                // Arrange — author structured search returns a match; title-only pass should not be needed
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: ["Mistborn"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync([new BookLookupResult { Title = "Mistborn", Author = "Brandon Sanderson" }]);

                using var stream = new MemoryStream();

                // Act
                await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — author search was called; title-only fallback was not needed
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksAsync(null, null, "Brandon Sanderson"), Times.Once);
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksByTextAsync("Mistborn"), Times.Never);
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenAuthorSearchYieldsNoMatches_FallsBackToTitleSearch()
            {
                // Arrange — author search returns results that score below threshold;
                // title search returns the real match
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: ["Mistborn"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync([new BookLookupResult { Title = "Completely Unrelated Book", Author = "Unknown" }]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksByTextAsync("Mistborn"))
                    .ReturnsAsync([new BookLookupResult { Title = "Mistborn", Author = "Brandon Sanderson" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — title fallback was called
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksByTextAsync("Mistborn"), Times.Once);
                result.MatchedBooks.Should().NotBeEmpty();
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenNoAuthorsDetected_SearchesByTitleOnly()
            {
                // Arrange — NLP detected no authors, only a title
                SetupDetectionResult(
                    authors: [],
                    titles: ["Mistborn"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksByTextAsync("Mistborn"))
                    .ReturnsAsync([new BookLookupResult { Title = "Mistborn", Author = "Brandon Sanderson" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — only title-only query was called (no author search)
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksByTextAsync("Mistborn"), Times.Once);
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                result.MatchedBooks.Should().NotBeEmpty();
            }
        }

        public class BestGuessTests : BookCoverAnalysisServiceTestBase
        {
            [Fact]
            public async Task AnalyzeCoverAsync_WhenBothPassesBelowThresholdButScoreAboveZero_ReturnsBestGuess()
            {
                // Arrange — author search returns a partial match (score below 0.8 threshold),
                // title search also returns a partial match; Phase 3 should return the highest scorer
                SetupDetectionResult(
                    authors: ["Frank Herbert"],
                    titles: ["Dune"]);

                // Author search: "Dune Messiah" scores below threshold (title doesn't fully match "Dune")
                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Frank Herbert"))
                    .ReturnsAsync([new BookLookupResult { Title = "Dune Messiah", Author = "Frank Herbert" }]);

                // Title search: also returns something below threshold
                BookLookupServiceMock
                    .Setup(s => s.SearchBooksByTextAsync("Dune"))
                    .ReturnsAsync([new BookLookupResult { Title = "Dune Messiah", Author = "Frank Herbert" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — Phase 3 best-guess returns the partial match rather than nothing
                result.MatchedBooks.Should().NotBeEmpty();
                result.MatchedBooks.Should().Contain(b => b.Title == "Dune Messiah");
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenBothPassesBelowThresholdAndScoreIsZero_ReturnsEmpty()
            {
                // Arrange — OCR words have no fuzzy similarity to the returned books at all
                SetupDetectionResult(
                    authors: ["Qxzbvw"],
                    titles: ["Zyxwvut"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Qxzbvw"))
                    .ReturnsAsync([new BookLookupResult { Title = "Dune", Author = "Frank Herbert" }]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksByTextAsync("Zyxwvut"))
                    .ReturnsAsync([new BookLookupResult { Title = "Dune", Author = "Frank Herbert" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — score is 0, Phase 3 does not return a false positive
                result.ExactMatch.Should().BeNull();
                result.MatchedBooks.Should().BeEmpty();
            }
        }

        public class DetectionFailureTests : BookCoverAnalysisServiceTestBase
        {
            [Fact]
            public async Task AnalyzeCoverAsync_WhenDetectionFails_ReturnsFailureWithNoBooks()
            {
                // Arrange
                SetupDetectionFailure("OCR processing failed");

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert
                result.Analysis.IsSuccess.Should().BeFalse();
                result.ExactMatch.Should().BeNull();
                result.MatchedBooks.Should().BeEmpty();
            }
        }

        public class FuzzyMatchTests : BookCoverAnalysisServiceTestBase
        {
            [Fact]
            public async Task AnalyzeCoverAsync_WhenOcrHasSingleCharTypoInTitle_StillSetsExactMatch()
            {
                // Arrange — OCR returns "Mistbom" (missing 'r') instead of "Mistborn"
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: ["Mistbom"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync([new BookLookupResult { Title = "Mistborn", Author = "Brandon Sanderson" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — fuzzy match tolerates single-char typo, exact match set
                result.ExactMatch.Should().NotBeNull();
                result.ExactMatch!.Title.Should().Be("Mistborn");
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenOcrHasSingleCharTypoInAuthorName_StillReturnsMatchedBook()
            {
                // Arrange — OCR returns "Herber" (missing 't') instead of "Herbert"
                SetupDetectionResult(
                    authors: ["Frank Herber"],
                    titles: ["Dune"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Frank Herber"))
                    .ReturnsAsync([new BookLookupResult { Title = "Dune", Author = "Frank Herbert" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — book returned despite author name typo
                result.MatchedBooks.Should().NotBeEmpty();
                result.MatchedBooks.Should().Contain(b => b.Title == "Dune");
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenOcrTypoIsInLocalBook_StillSetsExactMatchToLocalBook()
            {
                // Arrange — local DB book; OCR has "Neuromancr" instead of "Neuromancer"
                SeedBook(id: 42, title: "Neuromancer", author: "William Gibson");

                SetupDetectionResult(
                    authors: ["William Gibson"],
                    titles: ["Neuromancr"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "William Gibson"))
                    .ReturnsAsync([new BookLookupResult { Title = "Neuromancer", Author = "William Gibson" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — local book matched by ID despite title typo in OCR
                result.ExactMatch.Should().NotBeNull();
                result.ExactMatch!.Id.Should().Be(42);
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenOcrWordIsTooDistortedToMatchAnyBookWord_DoesNotMatch()
            {
                // Arrange — "Qxzbvw" has no fuzzy similarity to any real word
                SetupDetectionResult(
                    authors: ["Qxzbvw"],
                    titles: ["Zyxwvut"]);

                // Author search returns Dune, but OCR words have no fuzzy
                // similarity to "dune", "frank", or "herbert" → score = 0, filtered
                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Qxzbvw"))
                    .ReturnsAsync([new BookLookupResult { Title = "Dune", Author = "Frank Herbert" }]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksByTextAsync("Zyxwvut"))
                    .ReturnsAsync([new BookLookupResult { Title = "Dune", Author = "Frank Herbert" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — no false positive from heavily distorted OCR
                result.ExactMatch.Should().BeNull();
                result.MatchedBooks.Should().BeEmpty();
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenOcrHasTyposInBothTitleAndAuthor_StillReturnsMatchedBook()
            {
                // Arrange — "Fondation" (missing 'u') and "Issac" (transposed 'a')
                SetupDetectionResult(
                    authors: ["Issac Asimov"],
                    titles: ["Fondation"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Issac Asimov"))
                    .ReturnsAsync([new BookLookupResult { Title = "Foundation", Author = "Isaac Asimov" }]);

                using var stream = new MemoryStream();

                // Act
                var result = await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — book found despite typos in both title and author
                result.MatchedBooks.Should().NotBeEmpty();
                result.MatchedBooks.Should().Contain(b => b.Title == "Foundation");
            }
        }
    }
}
