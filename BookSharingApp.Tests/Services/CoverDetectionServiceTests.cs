using System.Net;
using BookSharingApp.Tests.Helpers;
using BookSharingWebAPI.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookSharingApp.Tests.Services
{
    public class CoverDetectionServiceTests
    {
        private static CoverDetectionService CreateService(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var handler = MockHttpMessageHandlerHelper.CreateMockHandler(jsonResponse, statusCode);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
            return new CoverDetectionService(client, new Mock<ILogger<CoverDetectionService>>().Object);
        }

        private static CoverDetectionService CreateServiceWithException(Exception exception)
        {
            var handler = MockHttpMessageHandlerHelper.CreateMockHandlerWithException(exception);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
            return new CoverDetectionService(client, new Mock<ILogger<CoverDetectionService>>().Object);
        }

        public class AnalyzeCoverImageAsyncTests
        {
            [Fact]
            public async Task AnalyzeCoverImageAsync_WhenServiceReturnsSuccess_DeserializesResponse()
            {
                // Arrange
                var json = """
                    {
                        "analysisStatus": { "isSuccess": true, "errorMessage": null },
                        "ocrResult": { "text": "Mistborn Brandon Sanderson", "regions": [] },
                        "nlpAnalysis": { "potentialAuthors": ["Brandon Sanderson"], "potentialTitles": ["Mistborn"] }
                    }
                    """;

                var service = CreateService(json);
                using var stream = new MemoryStream();

                // Act
                var result = await service.AnalyzeCoverImageAsync(stream, "image/jpeg");

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.PotentialAuthors.Should().ContainSingle().Which.Should().Be("Brandon Sanderson");
                result.PotentialTitles.Should().ContainSingle().Which.Should().Be("Mistborn");
                result.FullOcrText.Should().Be("Mistborn Brandon Sanderson");
            }

            [Fact]
            public async Task AnalyzeCoverImageAsync_WhenServiceReturnsFailure_DeserializesFailureStatus()
            {
                // Arrange
                var json = """
                    {
                        "analysisStatus": { "isSuccess": false, "errorMessage": "OCR failed: model error" },
                        "ocrResult": null,
                        "nlpAnalysis": null
                    }
                    """;

                var service = CreateService(json);
                using var stream = new MemoryStream();

                // Act
                var result = await service.AnalyzeCoverImageAsync(stream, "image/jpeg");

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.ErrorMessage.Should().Be("OCR failed: model error");
                result.PotentialAuthors.Should().BeEmpty();
                result.PotentialTitles.Should().BeEmpty();
            }

            [Fact]
            public async Task AnalyzeCoverImageAsync_WhenServiceReturnsNonSuccess_ThrowsHttpRequestException()
            {
                // Arrange
                var service = CreateService("{}", HttpStatusCode.ServiceUnavailable);
                using var stream = new MemoryStream();

                // Act
                var act = async () => await service.AnalyzeCoverImageAsync(stream, "image/jpeg");

                // Assert
                await act.Should().ThrowAsync<HttpRequestException>();
            }

            [Fact]
            public async Task AnalyzeCoverImageAsync_WhenNetworkFails_PropagatesException()
            {
                // Arrange
                var service = CreateServiceWithException(new HttpRequestException("Connection refused"));
                using var stream = new MemoryStream();

                // Act
                var act = async () => await service.AnalyzeCoverImageAsync(stream, "image/jpeg");

                // Assert
                await act.Should().ThrowAsync<HttpRequestException>()
                    .WithMessage("Connection refused");
            }

            [Fact]
            public async Task AnalyzeCoverImageAsync_WhenNlpAnalysisIsNull_ReturnsSafeDefaults()
            {
                // Arrange — service returns success but no NLP analysis
                var json = """
                    {
                        "analysisStatus": { "isSuccess": true },
                        "ocrResult": { "text": "some text", "regions": [] },
                        "nlpAnalysis": null
                    }
                    """;

                var service = CreateService(json);
                using var stream = new MemoryStream();

                // Act
                var result = await service.AnalyzeCoverImageAsync(stream, "image/jpeg");

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.PotentialAuthors.Should().BeEmpty();
                result.PotentialTitles.Should().BeEmpty();
            }
        }
    }
}
