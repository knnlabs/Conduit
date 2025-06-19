using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonNamingPolicy

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data; // Added for database initialization
using ConduitLLM.Configuration.Extensions; // Added for DataProtectionExtensions and HealthCheckExtensions
using ConduitLLM.Configuration.Repositories; // Added for repository interfaces
using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions; // Add namespace for custom exceptions
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Adapters;
using ConduitLLM.Http.Controllers; // Added for RealtimeController
using ConduitLLM.Http.Extensions; // Added for AudioServiceExtensions
using ConduitLLM.Http.Middleware; // Added for Security middleware extensions
using ConduitLLM.Http.Security;
using ConduitLLM.Http.Services; // Added for ApiVirtualKeyService
using ConduitLLM.Providers; // Assuming LLMClientFactory is here
using ConduitLLM.Providers.Extensions; // Add namespace for HttpClient extensions

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore; // Added for EF Core
using Microsoft.EntityFrameworkCore.Diagnostics; // Added for warning suppression
using Microsoft.Extensions.Options; // Added for IOptions

using Npgsql.EntityFrameworkCore.PostgreSQL; // Added for PostgreSQL
using StackExchange.Redis; // Added for Redis-based task service

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Don't load appsettings.json
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});
builder.Configuration.Sources.Clear();

// Add appsettings files for development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
}

builder.Configuration.AddEnvironmentVariables();

// Database initialization strategy
// We use a flexible approach that works for both development and production
bool skipDatabaseInit = Environment.GetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT") == "true";

if (skipDatabaseInit)
{
    Console.WriteLine("[Conduit] WARNING: Skipping database initialization. Ensure database schema is up to date.");
}
else
{
    Console.WriteLine("[Conduit] Database will be initialized automatically.");
}

// Configure JSON options for snake_case serialization (OpenAI compatibility)
var jsonSerializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// --- Dependency Injection Setup ---

// 1. Configure Conduit Settings
builder.Services.AddOptions<ConduitSettings>()
    .Bind(builder.Configuration.GetSection("Conduit"))
    .ValidateDataAnnotations(); // Add validation if using DataAnnotations in settings classes

// Add database-sourced settings provider to populate settings from DB
builder.Services.AddTransient<IStartupFilter, DatabaseSettingsStartupFilter>();

// Rate Limiter registration
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<Microsoft.AspNetCore.Http.HttpContext>("VirtualKeyPolicy", context =>
    {
        // Use the actual partition provider from the policy instance
        var policy = context.RequestServices.GetRequiredService<VirtualKeyRateLimitPolicy>();
        return policy.GetPartition(context);
    });
});
builder.Services.AddScoped<VirtualKeyRateLimitPolicy>();

// Model costs tracking service
builder.Services.AddScoped<ConduitLLM.Configuration.Services.IModelCostService, ConduitLLM.Configuration.Services.ModelCostService>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.ICostCalculationService, ConduitLLM.Core.Services.CostCalculationService>();
builder.Services.AddMemoryCache();

// 2. Register DbContext Factory (using connection string from environment variables)
var connectionStringManager = new ConduitLLM.Core.Data.ConnectionStringManager();
var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString();
if (dbProvider == "sqlite")
{
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
    {
        options.UseSqlite(dbConnectionString);
        // Suppress PendingModelChangesWarning in production
        if (builder.Environment.IsProduction())
        {
            options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
    });
}
else if (dbProvider == "postgres")
{
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
    {
        options.UseNpgsql(dbConnectionString);
        // Suppress PendingModelChangesWarning in production
        if (builder.Environment.IsProduction())
        {
            options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
    });
}
else
{
    throw new InvalidOperationException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlite' and 'postgres'.");
}

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Add Core API Security services
builder.Services.AddCoreApiSecurity(builder.Configuration);

// Add all the service registrations BEFORE calling builder.Build()
// Register HttpClientFactory - REQUIRED for LLMClientFactory
builder.Services.AddHttpClient();

// Add dependencies needed for the Conduit service
builder.Services.AddScoped<ILLMClientFactory, ConduitLLM.Providers.LLMClientFactory>();
builder.Services.AddScoped<ConduitRegistry>();

// Add performance metrics service
builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IPerformanceMetricsService, ConduitLLM.Core.Services.PerformanceMetricsService>();

// Add required services for the router components
builder.Services.AddScoped<ConduitLLM.Core.Routing.Strategies.IModelSelectionStrategy, ConduitLLM.Core.Routing.Strategies.SimpleModelSelectionStrategy>();
builder.Services.AddScoped<ILLMRouter, ConduitLLM.Core.Routing.DefaultLLMRouter>();

// Register token counter service for context management
builder.Services.AddScoped<ITokenCounter, ConduitLLM.Core.Services.TiktokenCounter>();
builder.Services.AddScoped<IContextManager, ConduitLLM.Core.Services.ContextManager>();

// Register all repositories using the extension method
builder.Services.AddRepositories();

// Register services
builder.Services.AddScoped<ConduitLLM.Configuration.IModelProviderMappingService, ConduitLLM.Configuration.ModelProviderMappingService>();
builder.Services.AddScoped<ConduitLLM.Configuration.IProviderCredentialService, ConduitLLM.Configuration.ProviderCredentialService>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService, ConduitLLM.Http.Services.ApiVirtualKeyService>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IProviderDiscoveryService, ConduitLLM.Core.Services.ProviderDiscoveryService>();

// Register cache service based on configuration
builder.Services.AddCacheService(builder.Configuration);

// Configure Data Protection with Redis persistence
// Check for REDIS_URL first, then fall back to CONDUIT_REDIS_CONNECTION_STRING
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
var redisConnectionString = Environment.GetEnvironmentVariable("CONDUIT_REDIS_CONNECTION_STRING");

if (!string.IsNullOrEmpty(redisUrl))
{
    try
    {
        redisConnectionString = ConduitLLM.Configuration.Utilities.RedisUrlParser.ParseRedisUrl(redisUrl);
    }
    catch
    {
        // Failed to parse REDIS_URL, will use legacy connection string if available
        // Validation will be logged during startup after logger is available
    }
}

builder.Services.AddRedisDataProtection(redisConnectionString, "Conduit");

// Register Configuration adapters (moved from Core)
builder.Services.AddConfigurationAdapters();

// Register provider model list service
builder.Services.AddScoped<ModelListService>();

// Register async task service
var useRedisForTasks = builder.Configuration.GetValue<bool>("ConduitLLM:Tasks:UseRedis", false);
if (useRedisForTasks && !string.IsNullOrEmpty(redisConnectionString))
{
    // Use Redis for distributed task management
    builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IAsyncTaskService>(sp =>
    {
        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        var logger = sp.GetRequiredService<ILogger<ConduitLLM.Core.Services.RedisAsyncTaskService>>();
        return new ConduitLLM.Core.Services.RedisAsyncTaskService(redis, logger);
    });
}
else
{
    // Use in-memory task service for single instance deployments
    builder.Services.AddSingleton<ConduitLLM.Core.Interfaces.IAsyncTaskService, ConduitLLM.Core.Services.InMemoryAsyncTaskService>();
}

// Register Conduit service
builder.Services.AddScoped<Conduit>();

// Register File Retrieval Service
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IFileRetrievalService, ConduitLLM.Core.Services.FileRetrievalService>();

// Register Audio services
builder.Services.AddConduitAudioServices(builder.Configuration);

// Register Real-time Audio services
builder.Services.AddSingleton<IRealtimeConnectionManager, RealtimeConnectionManager>();
builder.Services.AddSingleton<IRealtimeMessageTranslatorFactory, RealtimeMessageTranslatorFactory>();
builder.Services.AddScoped<IRealtimeProxyService, RealtimeProxyService>();
builder.Services.AddScoped<IRealtimeUsageTracker, RealtimeUsageTracker>();
builder.Services.AddHostedService<RealtimeConnectionManager>(provider =>
    provider.GetRequiredService<IRealtimeConnectionManager>() as RealtimeConnectionManager ??
    throw new InvalidOperationException("RealtimeConnectionManager not registered properly"));

// Register Real-time Message Translators
builder.Services.AddSingleton<ConduitLLM.Providers.Translators.OpenAIRealtimeTranslatorV2>();
builder.Services.AddSingleton<ConduitLLM.Providers.Translators.UltravoxRealtimeTranslator>();
builder.Services.AddSingleton<ConduitLLM.Providers.Translators.ElevenLabsRealtimeTranslator>();

// Register Audio routing
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IAudioRouter, ConduitLLM.Core.Routing.SimpleAudioRouter>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IAudioCapabilityDetector, ConduitLLM.Core.Services.AudioCapabilityDetector>();

// Register Media Storage Service
var storageProvider = builder.Configuration.GetValue<string>("ConduitLLM:Storage:Provider") ?? "InMemory";
if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<ConduitLLM.Core.Options.S3StorageOptions>(
        builder.Configuration.GetSection(ConduitLLM.Core.Options.S3StorageOptions.SectionName));
    builder.Services.AddSingleton<IMediaStorageService, S3MediaStorageService>();
}
else
{
    // Use in-memory storage for development
    builder.Services.AddSingleton<IMediaStorageService>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<InMemoryMediaStorageService>>();
        var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000";
        var baseUrl = urls.Split(';').First();
        return new InMemoryMediaStorageService(logger, baseUrl);
    });
}

// Add CORS support for WebUI requests
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5001",  // WebUI access
                "http://webui:8080",      // Docker internal
                "http://localhost:8080",  // Alternative local access
                "http://127.0.0.1:5001"   // Alternative localhost format
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // Enable credentials for auth headers
    });
});

// Add Controller support
builder.Services.AddControllers();

// Add standardized health checks (skip in test environment to avoid conflicts)
if (builder.Environment.EnvironmentName != "Test")
{
    // Use the same Redis connection string we configured above for health checks
    var healthChecksBuilder = builder.Services.AddConduitHealthChecks(dbConnectionString, redisConnectionString);

    // Add audio-specific health checks if audio services are configured
    if (builder.Configuration.GetSection("AudioService:Providers").Exists())
    {
        healthChecksBuilder.AddAudioHealthChecks(builder.Configuration);
    }
}

// Add database initialization services
builder.Services.AddScoped<ConduitLLM.Configuration.Data.DatabaseInitializer>();

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
        ConduitLLM.Configuration.Services.RedisUrlValidator.ValidateAndLog(envRedisUrl, logger, "Http Service");
    }
}

// Initialize database - Always run unless explicitly told to skip
// This ensures users get automatic schema updates when pulling new versions
if (!skipDatabaseInit)
{
    using (var scope = app.Services.CreateScope())
    {
        var dbInitializer = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.Data.DatabaseInitializer>();
        var initLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            initLogger.LogInformation("Starting database initialization...");

            // Wait for database to be available (especially important in Docker)
            var maxRetries = 10;
            var retryDelay = 3000; // 3 seconds between retries

            var success = await dbInitializer.InitializeDatabaseAsync(maxRetries, retryDelay);

            if (success)
            {
                initLogger.LogInformation("Database initialization completed successfully");
            }
            else
            {
                initLogger.LogError("Database initialization failed after {MaxRetries} attempts", maxRetries);
                // Always fail hard if database initialization fails
                // This prevents running with an incomplete schema
                throw new InvalidOperationException($"Database initialization failed after {maxRetries} attempts. Please check database connectivity and logs.");
            }
        }
        catch (Exception ex)
        {
            initLogger.LogError(ex, "Critical error during database initialization");
            // Re-throw to prevent the application from starting with a broken database
            throw new InvalidOperationException("Failed to initialize database. Application cannot start.", ex);
        }
    }
}
else
{
    using (var scope = app.Services.CreateScope())
    {
        var initLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        initLogger.LogWarning("Database initialization is skipped. Ensure database schema is up to date.");
    }
}

// Enable CORS
app.UseCors();

// Add security headers
app.UseCoreApiSecurityHeaders();

// Add Virtual Key authentication
app.UseVirtualKeyAuthentication();

// Add security middleware (IP filtering, rate limiting, ban checks)
app.UseCoreApiSecurity();

// Enable rate limiting (now that Virtual Keys are authenticated)
app.UseRateLimiter();

// Enable WebSockets for real-time communication
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
});

// Add controllers to the app
app.MapControllers();
Console.WriteLine("[Conduit API] Controllers registered");

// Map standardized health check endpoints
app.MapConduitHealthChecks();

// Add completions endpoint (legacy)
app.MapPost("/v1/completions", ([FromServices] ILogger<Program> logger) =>
{
    logger.LogInformation("Legacy /completions endpoint called.");
    return Results.Json(
        new
        {
            error = "The /completions endpoint is not implemented. Please use /chat/completions."
        },
        statusCode: 501,
        options: jsonSerializerOptions
    );
});

// Add embeddings endpoint
app.MapPost("/v1/embeddings", (
    [FromBody] EmbeddingRequest? request,
    [FromServices] ILogger<Program> logger) =>
{

    if (request == null)
    {
        return Results.BadRequest(new { error = "Invalid request body." });
    }

    try
    {
        // Currently embeddings are not fully implemented
        logger.LogWarning("Embeddings endpoint called but CreateEmbeddingAsync not implemented.");
        return Results.Json(
            new
            {
                error = "Embeddings routing not yet implemented."
            },
            statusCode: 501,
            options: jsonSerializerOptions
        );

        // Future implementation:
        // var response = await router.CreateEmbeddingAsync(request, cancellationToken);
        // return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing embeddings request for model: {Model}", request.Model);
        return Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = ex.Message,
                Type = "server_error",
                Code = "internal_error"
            }
        }, statusCode: 500, options: jsonSerializerOptions);
    }
});

// Add models endpoint
app.MapGet("/v1/models", ([FromServices] ILLMRouter router, [FromServices] ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Getting available models");

        // Get model names from the router
        var modelNames = router.GetAvailableModels();

        // Convert to OpenAI format
        var basicModelData = modelNames.Select(m => new
        {
            id = m,
            @object = "model"
        }).ToList();

        // Create the response envelope
        var response = new
        {
            data = basicModelData,
            @object = "list"
        };

        return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving models list");
        return Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = ex.Message,
                Type = "server_error",
                Code = "internal_error"
            }
        }, statusCode: 500, options: jsonSerializerOptions);
    }
});

// Add chat completions endpoint
app.MapPost("/v1/chat/completions", async (
    [FromBody] ChatCompletionRequest request,
    [FromServices] Conduit conduit,
    [FromServices] ILogger<Program> logger,
    HttpRequest httpRequest) =>
{
    logger.LogInformation("Received /v1/chat/completions request for model: {Model}", request.Model);

    try
    {
        // Non-streaming path
        if (request.Stream != true)
        {
            logger.LogInformation("Handling non-streaming request.");
            var response = await conduit.CreateChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted);
            return Results.Json(response, options: jsonSerializerOptions);
        }
        else
        {
            logger.LogInformation("Handling streaming request.");
            
            // Use enhanced SSE writer for performance metrics support
            var response = httpRequest.HttpContext.Response;
            var sseWriter = response.CreateEnhancedSSEWriter(jsonSerializerOptions);
            
            // Create metrics collector if performance tracking is enabled
            var settings = httpRequest.HttpContext.RequestServices.GetRequiredService<IOptions<ConduitSettings>>().Value;
            StreamingMetricsCollector? metricsCollector = null;
            
            if (settings.PerformanceTracking?.Enabled == true && settings.PerformanceTracking.TrackStreamingMetrics)
            {
                logger.LogInformation("Performance tracking enabled for streaming request");
                var requestId = Guid.NewGuid().ToString();
                response.Headers["X-Request-ID"] = requestId;
                
                // Get provider info for metrics from settings
                var modelMapping = settings.ModelMappings?.FirstOrDefault(m => 
                    string.Equals(m.ModelAlias, request.Model, StringComparison.OrdinalIgnoreCase));
                var providerName = modelMapping?.ProviderName ?? "unknown";
                
                logger.LogInformation("Creating StreamingMetricsCollector for model {Model}, provider {Provider}", request.Model, providerName);
                metricsCollector = new StreamingMetricsCollector(
                    requestId,
                    request.Model,
                    providerName);
            }
            else
            {
                logger.LogInformation("Performance tracking disabled for streaming request. Enabled: {Enabled}, TrackStreaming: {TrackStreaming}", 
                    settings.PerformanceTracking?.Enabled, 
                    settings.PerformanceTracking?.TrackStreamingMetrics);
            }

            try
            {
                await foreach (var chunk in conduit.StreamChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted))
                {
                    // Write content event
                    await sseWriter.WriteContentEventAsync(chunk);
                    
                    // Track metrics if enabled
                    if (metricsCollector != null && chunk?.Choices?.Count > 0)
                    {
                        var hasContent = chunk.Choices.Any(c => !string.IsNullOrEmpty(c.Delta?.Content));
                        if (hasContent)
                        {
                            if (metricsCollector.GetMetrics().TimeToFirstTokenMs == null)
                            {
                                metricsCollector.RecordFirstToken();
                            }
                            else
                            {
                                metricsCollector.RecordToken();
                            }
                        }
                        
                        // Emit metrics periodically
                        if (metricsCollector.ShouldEmitMetrics())
                        {
                            logger.LogDebug("Emitting streaming metrics");
                            await sseWriter.WriteMetricsEventAsync(metricsCollector.GetMetrics());
                        }
                    }
                }

                // Write final metrics if tracking is enabled
                if (metricsCollector != null)
                {
                    var finalMetrics = metricsCollector.GetFinalMetrics();
                    await sseWriter.WriteFinalMetricsEventAsync(finalMetrics);
                }

                // Write [DONE] to signal the end of the stream
                await sseWriter.WriteDoneEventAsync();
            }
            catch (Exception streamEx)
            {
                logger.LogError(streamEx, "Error in stream processing");
                await sseWriter.WriteErrorEventAsync(streamEx.Message);
            }

            return Results.Empty;
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing request");
        return Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = ex.Message,
                Type = "server_error",
                Code = "internal_error"
            }
        }, statusCode: 500, options: jsonSerializerOptions);
    }
});

app.Run();

// Helper class for OpenAI-compatible error response
public class OpenAIErrorResponse
{
    [JsonPropertyName("error")]
    public required OpenAIError Error { get; set; }
}

public class OpenAIError
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("param")]
    public string? Param { get; set; }
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

// Helper for triggering database settings load on startup
public class DatabaseSettingsStartupFilter : IStartupFilter
{
    // Inject both factories
    private readonly IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> _configDbContextFactory;
    private readonly IOptions<ConduitSettings> _settingsOptions;
    private readonly ILogger<DatabaseSettingsStartupFilter> _logger;

    public DatabaseSettingsStartupFilter(
        IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configDbContextFactory, // Inject correct factory
        IOptions<ConduitSettings> settingsOptions,
        ILogger<DatabaseSettingsStartupFilter> logger)
    {
        _configDbContextFactory = configDbContextFactory; // Assign correct factory
        _settingsOptions = settingsOptions;
        _logger = logger;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        LoadSettingsFromDatabaseAsync().GetAwaiter().GetResult(); // Load synchronously during startup
        return next;
    }

    private async Task LoadSettingsFromDatabaseAsync()
    {
        _logger.LogInformation("Attempting to load settings from database on startup...");
        var settings = _settingsOptions.Value;
        try
        {
            // Load provider credentials from Config context
            await using var configDbContext = await _configDbContextFactory.CreateDbContextAsync();
            var providerCredsList = await configDbContext.ProviderCredentials.ToListAsync();
            if (providerCredsList.Any())
            {
                _logger.LogInformation("Found {Count} provider credentials in database", providerCredsList.Count);

                // Convert database provider credentials to Core provider credentials
                var providersList = providerCredsList.Select(p => new ProviderCredentials
                {
                    ProviderName = p.ProviderName,
                    ApiKey = p.ApiKey,
                    ApiVersion = p.ApiVersion,
                    ApiBase = p.BaseUrl // Map BaseUrl from DB entity to ApiBase in settings entity
                }).ToList();

                // Now integrate these with existing settings
                // Two approaches: 
                // 1. Replace in-memory with DB values
                // 2. Merge DB with in-memory (with DB taking precedence)
                // Using approach #2 here

                if (settings.ProviderCredentials == null)
                {
                    settings.ProviderCredentials = new List<ProviderCredentials>();
                }

                // Remove any in-memory providers that exist in DB to avoid duplicates
                settings.ProviderCredentials.RemoveAll(p =>
                    providersList.Any(dbp =>
                        string.Equals(dbp.ProviderName, p.ProviderName, StringComparison.OrdinalIgnoreCase)));

                // Then add all the database credentials
                settings.ProviderCredentials.AddRange(providersList);

                foreach (var cred in providersList)
                {
                    _logger.LogInformation("Loaded credentials for provider: {ProviderName}", cred.ProviderName);
                }
            }
            else
            {
                _logger.LogWarning("No provider credentials found in database");
            }

            // Load model mappings using ModelProviderMappingRepository directly
            var modelMappingsEntities = await configDbContext.ModelProviderMappings
                .Include(m => m.ProviderCredential)
                .ToListAsync();

            if (modelMappingsEntities.Any())
            {
                _logger.LogInformation("Found {Count} model mappings in database", modelMappingsEntities.Count);

                // Convert database model mappings to Core model mappings
                var modelMappingsList = modelMappingsEntities.Select(m => new ModelProviderMapping
                {
                    ModelAlias = m.ModelAlias,
                    ProviderName = m.ProviderCredential.ProviderName,
                    ProviderModelId = m.ProviderModelName
                }).ToList();

                // Configure the model mappings in settings
                if (settings.ModelMappings == null)
                {
                    settings.ModelMappings = new List<ModelProviderMapping>();
                }

                // Remove existing mappings that exist in DB to avoid duplicates
                settings.ModelMappings.RemoveAll(m =>
                    modelMappingsList.Any(dbm =>
                        string.Equals(dbm.ModelAlias, m.ModelAlias, StringComparison.OrdinalIgnoreCase)));

                // Add all the database model mappings
                settings.ModelMappings.AddRange(modelMappingsList);

                foreach (var mapping in modelMappingsList)
                {
                    _logger.LogInformation("Loaded model mapping: {ModelAlias} -> {ProviderName}/{ProviderModelId}",
                        mapping.ModelAlias, mapping.ProviderName, mapping.ProviderModelId);
                }
            }
            else
            {
                _logger.LogWarning("No model mappings found in database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings from database");
        }
    }
}

// Make Program class accessible for testing
public partial class Program { }
