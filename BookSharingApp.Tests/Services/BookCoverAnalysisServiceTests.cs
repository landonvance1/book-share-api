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
                // Arrange — NLP has author but title is partial.
                // Book words: ["Mistborn", "Brandon", "Sanderson"] = 3
                // OCR word set: ["brandon", "sanderson"] — "mistborn" missing → score < 1.0
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: []);

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
                result.ExactMatch.Should().BeNull();
                result.MatchedBooks.Should().NotBeEmpty();
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenNoMatchesFound_DoesNotSetExactMatch()
            {
                // Arrange — both author search and title fallback return no results
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

                // Assert
                result.ExactMatch.Should().NotBeNull();
                result.ExactMatch!.Title.Should().Be("Dune");
                result.MatchedBooks.Should().HaveCountGreaterThan(1);
            }
        }

        public class AuthorFirstSearchTests : BookCoverAnalysisServiceTestBase
        {
            [Fact]
            public async Task AnalyzeCoverAsync_WhenAuthorDetected_SearchesByAuthorFirst()
            {
                // Arrange
                SetupDetectionResult(
                    authors: ["Brandon Sanderson"],
                    titles: ["Mistborn"]);

                BookLookupServiceMock
                    .Setup(s => s.SearchBooksAsync(null, null, "Brandon Sanderson"))
                    .ReturnsAsync([new BookLookupResult { Title = "Mistborn", Author = "Brandon Sanderson" }]);

                using var stream = new MemoryStream();

                // Act
                await Service.AnalyzeCoverAsync(stream, "image/jpeg", "test");

                // Assert — author search was called, title search was NOT
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksAsync(null, null, "Brandon Sanderson"), Times.Once);
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksByTextAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task AnalyzeCoverAsync_WhenAuthorSearchYieldsNoMatches_FallsBackToTitleSearch()
            {
                // Arrange — author search returns results but none score above threshold.
                // "Completely Unrelated Book" has 0 word overlap with "Brandon Sanderson" + "Mistborn".
                // Title-only score for "Completely" = 0/1 = 0, full score = 0/4 = 0 → filtered out.
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

                // Assert — only title search was called
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksAsync(null, null, It.IsAny<string>()), Times.Never);
                BookLookupServiceMock.Verify(
                    s => s.SearchBooksByTextAsync("Mistborn"), Times.Once);
                result.MatchedBooks.Should().NotBeEmpty();
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
    }
}
