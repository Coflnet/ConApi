using System.Text.Json.Serialization;
using Coflnet.Auth;
using Coflnet.Connections.Services;
using Coflnet.Connections.Middleware;
using Coflnet.Core;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<PersonService>();
builder.Services.AddSingleton<PlaceService>();
builder.Services.AddSingleton<ThingService>();
builder.Services.AddSingleton<EventService>();
builder.Services.AddSingleton<RelationshipService>();
builder.Services.AddSingleton<ShareService>();
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton<ExportService>();
builder.Services.AddSingleton<EnhancedSearchService>();
builder.Services.AddSingleton<SourceCitationService>();
builder.Services.AddSingleton<CachingService>();
builder.Services.AddSingleton<PersonEnhancedService>();
builder.Services.AddSingleton<RelationshipSuggestionService>();
// Database initialization service for centralized table management
builder.Services.AddSingleton<DatabaseInitializationService>();
// Migration runner depends on the services that expose EnsureSchema
builder.Services.AddSingleton<MigrationRunner>();

// Add memory cache for caching service
builder.Services.AddMemoryCache();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<CassandraHealthCheck>("cassandra")
    .AddCheck<StorageHealthCheck>("storage");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionHandler>();
}).AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.AddCoflAuthService();
builder.Services.AddCoflnetCore();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Force initialization of centralized mapping configuration so tables created
// by services use the same mapping information (prevents driver inferring enum types)
_ = Coflnet.Connections.Services.GlobalMapping.Instance;

// Initialize database on startup
using (var scope = app.Services.CreateScope())
{
    var dbInitService = scope.ServiceProvider.GetService<DatabaseInitializationService>();
    if (dbInitService != null)
    {
        var (success, message) = await dbInitService.InitializeDatabaseAsync();
        if (success)
        {
            app.Logger.LogInformation("Database initialized successfully on startup");
        }
        else
        {
            app.Logger.LogWarning("Database initialization failed on startup: {Message}", message);
        }
    }
}

// Note: MigrationRunner is kept for backward compatibility but DatabaseInitializationService
// is now the preferred way to manage database schema
/*
// Run centralized migrations once on startup to avoid individual services doing this repeatedly
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetService<MigrationRunner>();
    if (runner != null)
    {
        runner.RunMigrations();
    }
}
*/

app.UseRouting();
app.UseCoflnetCore();
app.UseCors("AllowAll");
app.UseRateLimiting();
// log every request
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Path}");
    await next();
    var responseCode = context.Response.StatusCode;
    Console.WriteLine($"Response: {responseCode}");
});
app.UseCoflAuthService();
app.MapControllers();
app.MapHealthChecks("/health");

var googleConfigpath = app.Configuration["GOOGLE_APPLICATION_CREDENTIALS"];
if (googleConfigpath == null)
    app.Logger.LogWarning("GOOGLE_APPLICATION_CREDENTIALS not set, not initializing Firebase");
else
{
    var credentials = await  GoogleCredential.FromFileAsync(googleConfigpath, default);
    FirebaseApp.Create(new AppOptions()
    {
        Credential = credentials
    });
}

// Create a test user and token on startup (useful for local development).
// This is intentionally best-effort and will not crash the app if it fails.
try
{
    using (var scope2 = app.Services.CreateScope())
    {
        var authService = scope2.ServiceProvider.GetService<AuthService>();
        if (authService != null)
        {
            // Create token for the user. CreateTokenFor may be sync or return a Task.
            string? token = authService.CreateTokenFor(Guid.Empty, 600);

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var tokenPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), ".test_token");
                    System.IO.File.WriteAllText(tokenPath, token);
                    app.Logger.LogInformation("Wrote test token to {path}", tokenPath);
                }
                catch (Exception writeEx)
                {
                    app.Logger.LogWarning(writeEx, "Failed to write test token to disk");
                }
            }
            else
            {
                app.Logger.LogInformation("Test token was null or empty; skipping writing to disk.");
            }
        }
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Failed to create test user/token on startup");
}

app.Run();
