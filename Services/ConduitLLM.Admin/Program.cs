using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Validation;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Converters;
using ConduitLLM.Security.Middleware;

using Scalar.AspNetCore;

namespace ConduitLLM.Admin;

/// <summary>
/// Entry point for the Admin API application
/// </summary>
public partial class Program
{
    /// <summary>
    /// Application entry point that configures and starts the web application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Create a startup logger for structured logging during service registration
        using var startupLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var startupLogger = startupLoggerFactory.CreateLogger("ConduitLLM.Admin.Startup");

        // Add services to the container
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Configure JSON to use camelCase for compatibility with TypeScript clients
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

                // IMPORTANT: Make JSON deserialization case-insensitive to prevent bugs
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

                // Ensure all DateTime values serialize as UTC with 'Z' suffix
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // Tier 2a (#904): return the Admin API's standard ErrorResponseDto for automatic
                // [ApiController] model-validation failures instead of the default
                // ValidationProblemDetails — unifying the validation error shape with the rest of
                // the Admin API (AdminExceptionMiddleware also emits ErrorResponseDto).
                options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create;
            });

        // Operation-logging action filter — replaces the per-action success logging that used to
        // live in AdminControllerBase.ExecuteAsync. Applied per controller via [ServiceFilter]
        // during the incremental Tier 1a migration (#902); promote to a global filter once all
        // controllers are converted.
        builder.Services.AddScoped<OperationLoggingFilter>();

        builder.Services.AddEndpointsApiExplorer();

        // Add HttpClient factory for provider connection testing
        builder.Services.AddHttpClient();

        // Configure built-in OpenAPI support
        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ConduitLLM.Admin.OpenApi.AdminApiDocumentTransformer>();
            options.AddOperationTransformer<ConduitLLM.Admin.OpenApi.ApiKeySecurityOperationTransformer>();
            // Tier 2b (#905): document the universal 500 once, so controllers can drop the per-action
            // [ProducesResponseType(Status500InternalServerError)] boilerplate.
            options.AddOperationTransformer<ConduitLLM.Admin.OpenApi.DefaultErrorResponsesOperationTransformer>();
        });

        // Configure services (partial class methods)
        ConfigureCoreServices(builder, startupLogger);
        ConfigureMessagingServices(builder, startupLogger);
        ConfigureMonitoringServices(builder, startupLogger);

        var app = builder.Build();

        // Log deprecation warnings and validate Redis URL
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            ConduitLLM.Configuration.Extensions.DeprecationWarnings.LogEnvironmentVariableDeprecations(logger);

            // Validate Redis URL if provided
            var envRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
            if (!string.IsNullOrEmpty(envRedisUrl))
            {
                ConduitLLM.Configuration.Services.RedisUrlValidator.ValidateAndLog(envRedisUrl, logger, "Admin Service");
            }
        }

        // Run database migrations
        await app.RunDatabaseMigrationAsync();
        app.Logger.LogInformation("Database migrations completed successfully");

        // Seed default data (e.g., default retention policy)
        await app.SeedDefaultDataAsync();
        app.Logger.LogInformation("Default data seeding completed");

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi("/openapi/v1.json");
            app.MapScalarApiReference();
            app.Logger.LogInformation("Scalar UI available at /scalar/v1");
        }

        // Only use HTTPS redirection if explicitly enabled
        var enableHttpsRedirection = Environment.GetEnvironmentVariable("CONDUIT_ENABLE_HTTPS_REDIRECTION") != "false";
        if (enableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        // Add health endpoint authorization (early in pipeline, before authentication)
        app.UseHealthEndpointAuthorization();

        // Add middleware for authentication and request tracking
        app.UseAdminMiddleware();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // Tier 3 pilot (#906): ModelAuthor served as Minimal-API endpoints (replaces ModelAuthorController).
        app.MapModelAuthorEndpoints();

        // Map SignalR hub with master key authentication
        app.MapHub<ConduitLLM.Admin.Hubs.AdminNotificationHub>("/hubs/admin-notifications");

        // Map monitoring endpoints (health, metrics, Prometheus)
        MapMonitoringEndpoints(app);

        app.Logger.LogInformation(
            "Admin API started — Environment: {Environment}, URLs: {Urls}",
            app.Environment.EnvironmentName,
            string.Join(", ", app.Urls));

        app.Run();
    }
}

// Make Program accessible for testing
public partial class Program { }
