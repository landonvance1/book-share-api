namespace BookSharingApp.Common
{
    /// <summary>
    /// Rate limit category names used throughout the application.
    /// <para>
    /// Configuration (see Program.cs):
    /// <list type="bullet">
    ///   <item>GlobalApiIp: 200 requests per minute per IP (prevents brute force attacks)</item>
    ///   <item>GlobalApiUser: 500 requests per minute per user (prevents API abuse by authenticated users)</item>
    ///   <item>AuthLogin: 10 requests per minute per IP (prevents password brute force)</item>
    ///   <item>AuthRegister: 5 requests per minute per IP (prevents account creation spam)</item>
    ///   <item>AuthRefresh: 30 requests per minute per IP (prevents token refresh abuse)</item>
    ///   <item>ChatSend: 30 messages per 2 minutes per user (prevents chat spam)</item>
    ///   <item>ImageAnalysis: 10 requests per minute per user (prevents abuse of expensive OCR calls)</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class RateLimitNames
    {
        /// <summary>Rate limit for sending chat messages (30 per 2 minutes per user).</summary>
        public const string ChatSend = "chat-send";

        /// <summary>Global per-IP rate limit for all API endpoints (200 per minute).</summary>
        public const string GlobalApiIp = "global-api-ip";

        /// <summary>Global per-user rate limit for authenticated requests (500 per minute).</summary>
        public const string GlobalApiUser = "global-api-user";

        /// <summary>Rate limit for login endpoint attempts (10 per minute per IP).</summary>
        public const string AuthLogin = "auth-login";

        /// <summary>Rate limit for account registration attempts (5 per minute per IP).</summary>
        public const string AuthRegister = "auth-register";

        /// <summary>Rate limit for token refresh attempts (30 per minute per IP).</summary>
        public const string AuthRefresh = "auth-refresh";

        /// <summary>Rate limit for book cover image analysis (10 per minute per user).</summary>
        public const string ImageAnalysis = "image-analysis";

        /// <summary>Rate limit for reporting chat messages (10 per minute per user).</summary>
        public const string ChatReport = "chat-report";
    }
}