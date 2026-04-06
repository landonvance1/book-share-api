using BookSharingApp.Data;
using BookSharingApp.Models;
using BookSharingApp.Endpoints;
using BookSharingApp.Services;
using BookSharingApp.Hubs;
using BookSharingApp.Middleware;
using BookSharingApp.Common;
using BookSharingWebAPI.Services;
using BookSharingWebAPI.Endpoints;
using BookSharingWebAPI.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Diagnostics.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON console logging
builder.Logging.AddJsonConsole(options =>
{
    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var jwtKey = builder.Configuration["JWT_KEY"] ?? builder.Configuration["JWT:Key"] ??
    throw new InvalidOperationException("JWT key not found. Set JWT_KEY environment variable or JWT:Key in user secrets.");
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        // Allow SignalR to pass the JWT via query string
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        // Reject tokens for deleted/locked-out accounts immediately
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
            {
                context.Fail("Missing user identifier");
                return;
            }

            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null || user.LockoutEnd == DateTimeOffset.MaxValue)
            {
                context.Fail("Account has been deleted");
            }
        }
    };
});

builder.Services.AddAuthorization();

// Configure SignalR with JWT authentication
builder.Services.AddSignalR();

// Register custom services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserBookService, UserBookService>();
builder.Services.AddScoped<IUserDeletionService, UserDeletionService>();
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddSingleton<IRateLimiter>(provider =>
{
    var rateLimiter = new InMemoryRateLimiter();
    rateLimiter.ConfigureLimit(RateLimitNames.GlobalApiIp, 200, TimeSpan.FromMinutes(1)); // 200 requests per minute per IP
    rateLimiter.ConfigureLimit(RateLimitNames.GlobalApiUser, 500, TimeSpan.FromMinutes(1)); // 500 requests per minute per user
    rateLimiter.ConfigureLimit(RateLimitNames.AuthLogin, 10, TimeSpan.FromMinutes(1)); // 10 login attempts per minute
    rateLimiter.ConfigureLimit(RateLimitNames.AuthRegister, 5, TimeSpan.FromMinutes(1)); // 5 registrations per minute
    rateLimiter.ConfigureLimit(RateLimitNames.AuthRefresh, 30, TimeSpan.FromMinutes(1)); // 30 token refreshes per minute
    rateLimiter.ConfigureLimit(RateLimitNames.ChatSend, 30, TimeSpan.FromMinutes(2)); // 30 messages per 2 minutes
    rateLimiter.ConfigureLimit(RateLimitNames.ImageAnalysis, 10, TimeSpan.FromMinutes(1)); // 10 image analyses per minute
    rateLimiter.ConfigureLimit(RateLimitNames.ChatReport, 10, TimeSpan.FromMinutes(1)); // 10 reports per minute per user
    return rateLimiter;
});

builder.Services.AddHttpClient<IBookLookupService, OpenLibraryService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Community Bookshare App (landonpvance@gmail.com)");
});

builder.Services.AddHttpClient<ICoverDetectionService, CoverDetectionService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CoverDetection:BaseUrl"]
        ?? throw new InvalidOperationException("CoverDetection:BaseUrl is not configured."));
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IBookCoverAnalysisService, BookCoverAnalysisService>();
builder.Services.AddScoped<CoverImageValidator>();

// HTTP request/response logging
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(name: "postgresql", tags: new[] { "ready" });

// OpenTelemetry metrics
builder.Services.AddSingleton(new Meter("book-share-api"));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("book-share-api"))
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddMeter("book-share-api")
         .AddPrometheusExporter();
    });

var app = builder.Build();

// Apply migrations and seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    await context.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(context, userManager);
}

var meter = app.Services.GetRequiredService<Meter>();
app.RegisterBookShareMetrics(meter);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Attach TraceIdentifier as a logging scope for all requests
app.Use(async (context, next) =>
{
    using (app.Logger.BeginScope(new Dictionary<string, object>
    {
        ["RequestId"] = context.TraceIdentifier
    }))
    {
        await next();
    }
});

app.UseHttpLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Add rate limiting middleware
app.UseMiddleware<RateLimitMiddleware>();

app.UseStaticFiles();

// Map endpoints
app.MapAuthEndpoints();
app.MapBookEndpoints();
app.MapImageAnalysisEndpoints();
app.MapCommunityEndpoints();
app.MapCommunityUserEndpoints();
app.MapUserBookEndpoints();
app.MapShareEndpoints();
app.MapChatEndpoints();
app.MapNotificationEndpoints();
app.MapLegalEndpoints();

// Map SignalR hub
app.MapHub<ChatHub>("/chathub");

// Health checks
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });

// Prometheus metrics
app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();
