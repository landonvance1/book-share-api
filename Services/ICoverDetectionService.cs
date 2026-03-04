using BookSharingWebAPI.Models;

namespace BookSharingWebAPI.Services;

public interface ICoverDetectionService
{
    Task<CoverDetectionResult> AnalyzeCoverImageAsync(Stream imageStream, string contentType, CancellationToken cancellationToken = default);
}
