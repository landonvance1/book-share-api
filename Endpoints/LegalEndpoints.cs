namespace BookSharingApp.Endpoints
{
    public static class LegalEndpoints
    {
        public static void MapLegalEndpoints(this WebApplication app)
        {
            app.MapGet("/privacy-policy", (IWebHostEnvironment env) =>
            {
                var path = Path.Combine(env.WebRootPath, "privacy-policy.html");
                return Results.File(path, "text/html");
            })
            .AllowAnonymous()
            .WithTags("Legal")
            .WithName("GetPrivacyPolicy")
            .WithOpenApi();
        }
    }
}
