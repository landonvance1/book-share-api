namespace BookSharingApp.Common
{
    /// <summary>
    /// Configuration constants for book cover image analysis feature.
    /// </summary>
    public static class ImageAnalysisConstants
    {
        /// <summary>Maximum file size for image uploads (4MB).</summary>
        public const int MaxImageFileSizeBytes = 4 * 1024 * 1024;

        /// <summary>Supported image MIME types for cover analysis.</summary>
        public static readonly string[] SupportedImageTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        /// <summary>Maximum number of results to return from endpoint.</summary>
        public const int MaxResultsPerResponse = 5;

        /// <summary>Minimum word match percentage for filtering results (0.0 to 1.0).</summary>
        public const double MinWordMatchThreshold = 0.5;
    }
}
