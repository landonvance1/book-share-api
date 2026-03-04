using BookSharingApp.Data;
using BookSharingApp.IntegrationTests.Helpers;
using BookSharingApp.Services;
using BookSharingWebAPI.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for BookCoverAnalysisService using real book cover images.
    ///
    /// Calls the real CoverDetectionService and OpenLibraryService. Requires the coverdotnet user-secrets set "CoverDetection:BaseUrl" "http://localhost:8000" --project BookSharingApp.csproj
    /// detection microservice to be running — tests return early (pass vacuously) when not configured.
    ///
    /// To configure:
    ///   dotnet user-secrets set "CoverDetection:BaseUrl" "http://localhost:8000" --project BookSharingApp.csproj
    ///
    /// To run:
    ///   dotnet test BookSharingApp.IntegrationTests/BookSharingApp.IntegrationTests.csproj
    /// </summary>
    public class BookCoverAnalysisServiceIntegrationTests
    {
        private static readonly string CoverImagesPath = Path.Combine(
            AppContext.BaseDirectory, "TestData", "CoverImages");

        public abstract class BookCoverAnalysisServiceIntegrationTestBase : IDisposable
        {
            // Null when cover detection service is not configured — tests return early before use.
            protected readonly BookCoverAnalysisService? Service;
            private readonly ApplicationDbContext _context;
            private readonly bool _credentialsConfigured;

            protected BookCoverAnalysisServiceIntegrationTestBase()
            {
                var configuration = new ConfigurationBuilder()
                    .AddUserSecrets(typeof(BookCoverAnalysisService).Assembly, optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                var baseUrl = configuration["CoverDetection:BaseUrl"];
                _credentialsConfigured = !string.IsNullOrWhiteSpace(baseUrl);

                var bookLookupService = new OpenLibraryService(
                    new HttpClient(),
                    new Mock<ILogger<OpenLibraryService>>().Object);

                _context = DbContextHelper.CreateInMemoryContext();

                if (_credentialsConfigured)
                {
                    var detectionService = new CoverDetectionService(
                        new HttpClient { BaseAddress = new Uri(baseUrl!) },
                        new Mock<ILogger<CoverDetectionService>>().Object);

                    Service = new BookCoverAnalysisService(
                        detectionService,
                        bookLookupService,
                        _context,
                        new Mock<ILogger<BookCoverAnalysisService>>().Object);
                }
            }

            /// <summary>
            /// Returns true when Azure credentials are absent — use at the top of each test to skip:
            /// <code>if (CredentialsMissing) return;</code>
            /// </summary>
            protected bool CredentialsMissing => !_credentialsConfigured;

            /// <summary>
            /// Opens a stream for a cover image by filename (e.g. "mistborn.jpg").
            /// </summary>
            protected static Stream OpenCoverImage(string filename) =>
                File.OpenRead(Path.Combine(CoverImagesPath, filename));

            public void Dispose() => _context.Dispose();
        }

        public class AnalyzeCoverAsyncTests : BookCoverAnalysisServiceIntegrationTestBase
        {
            [SkippableFact]
            public async Task AnalyzeCoverAsync_MistbornCover_ReturnsExpectedBook()
            {
                Skip.If(CredentialsMissing, "CoverDetection:BaseUrl not configured.");
                using var imageStream = OpenCoverImage("mistborn.jpg");

                var result = await Service!.AnalyzeCoverAsync(imageStream, "image/jpeg", "test-mistborn");

                result.MatchedBooks.Should().Contain(b => b.Title == "Mistborn" && b.Author == "Brandon Sanderson");
            }

            [SkippableFact]
            public async Task AnalyzeCoverAsync_SnowCrashCover_ReturnsExpectedBook()
            {
                Skip.If(CredentialsMissing, "CoverDetection:BaseUrl not configured.");
                using var imageStream = OpenCoverImage("snow-crash.jpg");

                var result = await Service!.AnalyzeCoverAsync(imageStream, "image/jpeg", "test-snow-crash");
                
                result.MatchedBooks.Should().Contain(b => b.Title == "Snow Crash" && b.Author == "Neal Stephenson");
            }

            [SkippableFact]
            public async Task AnalyzeCoverAsync_JadeCityCover_ReturnsExpectedBook()
            {
                Skip.If(CredentialsMissing, "CoverDetection:BaseUrl not configured.");
                using var imageStream = OpenCoverImage("jade-city.jpg");

                var result = await Service!.AnalyzeCoverAsync(imageStream, "image/jpeg", "test-jade-city");

                result.MatchedBooks.Should().Contain(b => b.Title == "Jade City" && b.Author == "Fonda Lee");
            }

            [SkippableFact]
            public async Task AnalyzeCoverAsync_GardensOfTheMoonCover_ReturnsExpectedBook()
            {
                Skip.If(CredentialsMissing, "CoverDetection:BaseUrl not configured.");
                using var imageStream = OpenCoverImage("gardens-of-the-moon.jpg");

                var result = await Service!.AnalyzeCoverAsync(imageStream, "image/jpeg", "test-gardens");

                result.MatchedBooks.Should().Contain(b => b.Title == "Gardens of the Moon" && b.Author == "Steven Erikson");
            }

            [SkippableFact]
            public async Task AnalyzeCoverAsync_ToKillAMockingbirdCover_ReturnsExpectedBook()
            {
                Skip.If(CredentialsMissing, "CoverDetection:BaseUrl not configured.");
                using var imageStream = OpenCoverImage("to-kill-a-mockingbird.jpg");

                var result = await Service!.AnalyzeCoverAsync(imageStream, "image/jpeg", "test-mockingbird");

                result.MatchedBooks.Should().Contain(b => b.Title == "To Kill a Mockingbird" && b.Author == "Harper Lee");
            }

            [SkippableFact]
            public async Task AnalyzeCoverAsync_ARestlessTruthCover_ReturnsExpectedBook()
            {
                Skip.If(CredentialsMissing, "CoverDetection:BaseUrl not configured.");
                using var imageStream = OpenCoverImage("a-restless-truth.jpg");

                var result = await Service!.AnalyzeCoverAsync(imageStream, "image/jpeg", "test-restless-truth");

                result.MatchedBooks.Should().Contain(b => b.Title == "A Restless Truth" && b.Author == "Freya Marske");
            }

            [SkippableFact]
            public async Task AnalyzeCoverAsync_UnderTheWhisperingDoorCover_ReturnsExpectedBook()
            {
                Skip.If(CredentialsMissing, "CoverDetection:BaseUrl not configured.");
                using var imageStream = OpenCoverImage("under-the-whispering-door.jpg");

                var result = await Service!.AnalyzeCoverAsync(imageStream, "image/jpeg", "test-whispering-door");

                result.MatchedBooks.Should().Contain(b => b.Title == "Under the Whispering Door" && b.Author == "T. J. Klune");
            }
        }
    }
}
