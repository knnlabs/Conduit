using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonNamingPolicy

using ConduitLLM.Configuration;
using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions; // Add namespace for custom exceptions
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Adapters;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers; // Assuming LLMClientFactory is here
using ConduitLLM.Providers.Extensions; // Add namespace for HttpClient extensions
using ConduitLLM.Http.Services; // Added for ApiVirtualKeyService
using ConduitLLM.Configuration.Repositories; // Added for repository interfaces

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Added for EF Core
using Microsoft.Extensions.Options; // Added for IOptions
using Microsoft.AspNetCore.RateLimiting;
using ConduitLLM.Http.Security;
using ConduitLLM.Http.Controllers; // Added for RealtimeController

using Npgsql.EntityFrameworkCore.PostgreSQL; // Added for PostgreSQL

var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    Args = args,
    // Don't load appsettings.json
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});
builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();

// Check if --apply-migrations flag is passed
bool explicitMigration = args.Contains("--apply-migrations");
bool shouldApplyMigrations = explicitMigration || Environment.GetEnvironmentVariable("APPLY_MIGRATIONS") == "true";
bool useEnsureCreated = Environment.GetEnvironmentVariable("CONDUIT_DATABASE_ENSURE_CREATED") == "true";

if (explicitMigration || shouldApplyMigrations)
{
    Console.WriteLine("[Conduit] Running with migration flag. Will prioritize database migration.");
}

if (useEnsureCreated)
{
    Console.WriteLine("[Conduit] Using EnsureCreated for database initialization (skipping migrations).");
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
        options.UseSqlite(dbConnectionString));
}
else if (dbProvider == "postgres")
{
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
        options.UseNpgsql(dbConnectionString));
}
else
{
    throw new InvalidOperationException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlite' and 'postgres'.");
}

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Add all the service registrations BEFORE calling builder.Build()
// Register HttpClientFactory - REQUIRED for LLMClientFactory
builder.Services.AddHttpClient();

// Add dependencies needed for the Conduit service
builder.Services.AddScoped<ILLMClientFactory, ConduitLLM.Providers.LLMClientFactory>();
builder.Services.AddScoped<ConduitRegistry>();

// Add required services for the router components
builder.Services.AddScoped<ConduitLLM.Core.Routing.Strategies.IModelSelectionStrategy, ConduitLLM.Core.Routing.Strategies.SimpleModelSelectionStrategy>();
builder.Services.AddScoped<ILLMRouter, ConduitLLM.Core.Routing.DefaultLLMRouter>();

// Register token counter service for context management
builder.Services.AddScoped<ITokenCounter, ConduitLLM.Core.Services.TiktokenCounter>();
builder.Services.AddScoped<IContextManager, ConduitLLM.Core.Services.ContextManager>();

// Register repositories
builder.Services.AddScoped<ConduitLLM.Configuration.Repositories.IVirtualKeyRepository, ConduitLLM.Configuration.Repositories.VirtualKeyRepository>();
builder.Services.AddScoped<ConduitLLM.Configuration.Repositories.IVirtualKeySpendHistoryRepository, ConduitLLM.Configuration.Repositories.VirtualKeySpendHistoryRepository>();
builder.Services.AddScoped<ConduitLLM.Configuration.Repositories.IModelCostRepository, ConduitLLM.Configuration.Repositories.ModelCostRepository>();
builder.Services.AddScoped<ConduitLLM.Configuration.Repositories.IModelProviderMappingRepository, ConduitLLM.Configuration.Repositories.ModelProviderMappingRepository>();
builder.Services.AddScoped<ConduitLLM.Configuration.Repositories.IProviderCredentialRepository, ConduitLLM.Configuration.Repositories.ProviderCredentialRepository>();

// Register services
builder.Services.AddScoped<ConduitLLM.Configuration.IModelProviderMappingService, ConduitLLM.Configuration.ModelProviderMappingService>();
builder.Services.AddScoped<ConduitLLM.Configuration.IProviderCredentialService, ConduitLLM.Configuration.ProviderCredentialService>();
builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IVirtualKeyService, ConduitLLM.Http.Services.ApiVirtualKeyService>();

// Register cache service based on configuration
builder.Services.AddSingleton<ConduitLLM.Configuration.Services.ICacheService, ConduitLLM.Configuration.Services.CacheService>();

// Register Configuration adapters (moved from Core)
builder.Services.AddConfigurationAdapters();

// Register provider model list service
builder.Services.AddScoped<ModelListService>();

// Register Conduit service
builder.Services.AddScoped<Conduit>();

// Register Audio services
builder.Services.AddConduitAudioServices();

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

var app = builder.Build();

// Enable CORS
app.UseCors();

// Enable WebSockets for real-time communication
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
});

// Add a health check endpoint
// Add controllers to the app
app.MapControllers();
Console.WriteLine("[Conduit API] Controllers registered");

// Add a health check endpoint
app.MapGet("/health", () => {
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
});

// Add completions endpoint (legacy)
app.MapPost("/v1/completions", ([FromServices] ILogger<Program> logger) => {
    logger.LogInformation("Legacy /completions endpoint called.");
    return Results.Json(
        new {
            error = "The /completions endpoint is not implemented. Please use /chat/completions."
        },
        statusCode: 501,
        options: jsonSerializerOptions
    );
});

// Add embeddings endpoint
app.MapPost("/v1/embeddings", (
    [FromBody] EmbeddingRequest? request,
    [FromServices] ILogger<Program> logger) => {

    if (request == null) {
        return Results.BadRequest(new { error = "Invalid request body." });
    }

    try {
        // Currently embeddings are not fully implemented
        logger.LogWarning("Embeddings endpoint called but CreateEmbeddingAsync not implemented.");
        return Results.Json(
            new {
                error = "Embeddings routing not yet implemented."
            },
            statusCode: 501,
            options: jsonSerializerOptions
        );

        // Future implementation:
        // var response = await router.CreateEmbeddingAsync(request, cancellationToken);
        // return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex) {
        logger.LogError(ex, "Error processing embeddings request for model: {Model}", request.Model);
        return Results.Json(new OpenAIErrorResponse {
            Error = new OpenAIError {
                Message = ex.Message,
                Type = "server_error",
                Code = "internal_error"
            }
        }, statusCode: 500, options: jsonSerializerOptions);
    }
});

// Add models endpoint
app.MapGet("/v1/models", ([FromServices] ILLMRouter router, [FromServices] ILogger<Program> logger) => {
    try {
        logger.LogInformation("Getting available models");

        // Get model names from the router
        var modelNames = router.GetAvailableModels();

        // Convert to OpenAI format
        var basicModelData = modelNames.Select(m => new {
            id = m,
            @object = "model"
        }).ToList();

        // Create the response envelope
        var response = new {
            data = basicModelData,
            @object = "list"
        };

        return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex) {
        logger.LogError(ex, "Error retrieving models list");
        return Results.Json(new OpenAIErrorResponse {
            Error = new OpenAIError {
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

    try {
        // Non-streaming path
        if (request.Stream != true) {
            logger.LogInformation("Handling non-streaming request.");
            var response = await conduit.CreateChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted);
            return Results.Json(response, options: jsonSerializerOptions);
        } else {
            logger.LogInformation("Handling streaming request.");
            // Implement streaming response
            var response = httpRequest.HttpContext.Response;
            response.Headers["Content-Type"] = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["Connection"] = "keep-alive";
            
            try {
                await foreach (var chunk in conduit.StreamChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted)) {
                    var json = JsonSerializer.Serialize(chunk, jsonSerializerOptions);
                    await response.WriteAsync($"data: {json}\n\n");
                    await response.Body.FlushAsync();
                }
                
                // Write [DONE] to signal the end of the stream
                await response.WriteAsync("data: [DONE]\n\n");
                await response.Body.FlushAsync();
            }
            catch (Exception streamEx) {
                logger.LogError(streamEx, "Error in stream processing");
                var errorJson = JsonSerializer.Serialize(new OpenAIErrorResponse { 
                    Error = new OpenAIError { 
                        Message = streamEx.Message, 
                        Type = "server_error", 
                        Code = "streaming_error" 
                    } 
                }, jsonSerializerOptions);
                
                await response.WriteAsync($"data: {errorJson}\n\n");
                await response.Body.FlushAsync();
            }
            
            return Results.Empty;
        }
    } catch (Exception ex) {
        logger.LogError(ex, "Error processing request");
        return Results.Json(new OpenAIErrorResponse { 
            Error = new OpenAIError { 
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
