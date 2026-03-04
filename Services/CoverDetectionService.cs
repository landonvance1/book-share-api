using System.Net.Http.Headers;
using System.Text.Json;
using BookSharingWebAPI.Models;

namespace BookSharingWebAPI.Services;

public class CoverDetectionService : ICoverDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoverDetectionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CoverDetectionService(HttpClient httpClient, ILogger<CoverDetectionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CoverDetectionResult> AnalyzeCoverImageAsync(
        Stream imageStream, string contentType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", "cover-image");

        var response = await _httpClient.PostAsync("/analyze", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cover detection service returned {StatusCode}", response.StatusCode);
            throw new HttpRequestException(
                $"Cover detection service returned {(int)response.StatusCode}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<CoverDetectionResult>(json, JsonOptions);

        if (result is null)
            return CoverDetectionResult.Failure("Failed to deserialize cover detection response");

        return result;
    }
}
