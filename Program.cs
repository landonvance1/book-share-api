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

var builder = WebApplication.CreateBuilder(args);

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
});

builder.Services.AddAuthorization();

// Configure SignalR with JWT authentication
builder.Services.AddSignalR();

// Configure JWT for SignalR
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Register custom services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserBookService, UserBookService>();
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

var app = builder.Build();

// Apply migrations and seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    await context.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(context, userManager);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

// Map SignalR hub
app.MapHub<ChatHub>("/chathub");

app.Run();
