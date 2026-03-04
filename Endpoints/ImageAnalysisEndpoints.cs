using BookSharingApp.Common;
using BookSharingWebAPI.Services;
using BookSharingWebAPI.Validators;

namespace BookSharingWebAPI.Endpoints;

public static class ImageAnalysisEndpoints
{
    public static void MapImageAnalysisEndpoints(this WebApplication app)
    {
        var analysis = app.MapGroup("/books/analyze")
            .WithTags("Book Analysis")
            .RequireAuthorization();

        analysis.MapPost("/cover", AnalyzeCoverAsync)
            .WithName("AnalyzeBookCover")
            .DisableAntiforgery()  // Required for file uploads
            .WithMetadata(new RateLimitAttribute(RateLimitNames.ImageAnalysis, RateLimitScope.User));
    }

    private static async Task<IResult> AnalyzeCoverAsync(
        IFormFile imageFile,
        HttpContext httpContext,
        IBookCoverAnalysisService coverAnalysisService,
        CoverImageValidator validator,
        ILogger<Program> logger)
    {
        var validation = validator.Validate(imageFile);
        if (!validation.IsValid)
            return Results.BadRequest(new { error = validation.ErrorMessage });

        var requestId = Guid.NewGuid().ToString("N")[..8];
        var userId = httpContext.User.FindFirst("sub")?.Value ?? "anonymous";
        var contentType = imageFile.ContentType.ToLowerInvariant();

        logger.LogInformation(
            "Starting cover image analysis [RequestId={RequestId}, UserId={UserId}, FileSize={FileSizeKb}KB, ContentType={ContentType}]",
            requestId, userId, imageFile.Length / 1024, contentType);

        try
        {
            using var stream = imageFile.OpenReadStream();
            var cancellationToken = httpContext.RequestAborted;
            var response = await coverAnalysisService.AnalyzeCoverAsync(stream, contentType, requestId, cancellationToken);

            if (!response.Analysis.IsSuccess)
                return Results.BadRequest(new { error = response.Analysis.ErrorMessage });

            return Results.Ok(response);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex,
                "Cover analysis OCR processing timed out [RequestId={RequestId}, UserId={UserId}].",
                requestId, userId);
            return Results.StatusCode(504);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("image") || ex.Message.Contains("Image"))
        {
            logger.LogWarning(ex,
                "Invalid image provided for cover analysis [RequestId={RequestId}, UserId={UserId}]: {ErrorMessage}",
                requestId, userId, ex.Message);
            return Results.BadRequest(new { error = "Invalid image file. Please ensure file is a valid JPEG, PNG, or WebP image." });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Cover detection service request failed [RequestId={RequestId}, UserId={UserId}, StatusCode={StatusCode}].",
                requestId, userId, ex.StatusCode);
            return Results.StatusCode(503);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error during cover analysis [RequestId={RequestId}, UserId={UserId}, ExceptionType={ExceptionType}]",
                requestId, userId, ex.GetType().Name);
            return Results.StatusCode(500);
        }
    }

}
