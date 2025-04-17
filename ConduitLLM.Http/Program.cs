using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Providers; // Assuming LLMClientFactory is here
using ConduitLLM.Core.Exceptions; // Add namespace for custom exceptions
using Microsoft.AspNetCore.Mvc;
using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonNamingPolicy
using Microsoft.Extensions.Options; // Added for IOptions
using Microsoft.EntityFrameworkCore; // Added for EF Core
using ConduitLLM.WebUI.Data; // Added for DbContext and models
using ConduitLLM.WebUI.Interfaces; // Added for IVirtualKeyService
using ConduitLLM.WebUI.Services;  // Added for VirtualKeyService
using Npgsql.EntityFrameworkCore.PostgreSQL; // Added for PostgreSQL

var builder = WebApplication.CreateBuilder(args);

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

// 2. Register DbContext Factory (using connection string from appsettings.json)
// Get database provider configuration from environment variables
string dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "sqlite";
string? dbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

// Default connection string for SQLite if not specified and SQLite is selected
if (string.IsNullOrEmpty(dbConnectionString))
{
    if (dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("DB_CONNECTION_STRING environment variable must be set when using PostgreSQL provider");
    }
    
    // Use the connection string from configuration for SQLite
    dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(dbConnectionString))
    {
        // Handle missing connection string
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
    }
}

// Configure DbContext Factory based on the database provider
if (dbProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase)) 
{
    builder.Services.AddDbContextFactory<ConfigurationDbContext>(options =>
        options.UseSqlite(dbConnectionString)); // SQLite
}
else if (dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    // PostgreSQL configuration - handled at runtime with the correct dependencies
    builder.Services.AddDbContextFactory<ConfigurationDbContext>(options =>
        options.UseNpgsql(dbConnectionString));
}
else
{
    throw new InvalidOperationException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlite' and 'postgres'.");
}

// 3. Register Conduit Services
// TODO: Consider creating a dedicated extension method like AddConduit() in Core or Providers
builder.Services.AddHttpClient(); // Required by LLMClientFactory and potentially clients
builder.Services.AddSingleton<ILLMClientFactory, LLMClientFactory>();
builder.Services.AddScoped<Conduit>(); // Scoped might be appropriate if clients hold state per request

// 4. Register Virtual Key Service (scoped as it uses DbContextFactory)
builder.Services.AddScoped<IVirtualKeyService, VirtualKeyService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

// Ensure static files are served properly for development
app.UseStaticFiles();
app.UseDefaultFiles();

app.UseHttpsRedirection(); // Consider if needed depending on deployment

// --- API Endpoints ---

app.MapPost("/v1/chat/completions", async (
    [FromBody] ChatCompletionRequest request,
    [FromServices] Conduit conduit,
    [FromServices] ILogger<Program> logger,
    [FromServices] IVirtualKeyService virtualKeyService, // Inject Virtual Key Service
    HttpRequest httpRequest,
    HttpResponse httpResponse) =>
{
    logger.LogInformation("Received /v1/chat/completions request for model: {Model}", request.Model);

    // 1. Extract API Key from header
    string? apiKey = null;
    string? originalApiKey = null; // Store the original key for virtual key check
    if (httpRequest.Headers.TryGetValue("Authorization", out var authHeader) &&
        authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        apiKey = authHeader.ToString().Substring("Bearer ".Length).Trim();
        originalApiKey = apiKey; // Keep the original key
        logger.LogDebug("Extracted API Key from Authorization header.");
    }
    else
    {
        logger.LogDebug("No Authorization header found or invalid format.");
    }

    // --- Virtual Key Check & Validation ---
    int? virtualKeyId = null; // Store the ID if a virtual key is used
    bool useVirtualKey = originalApiKey?.StartsWith("condt_", StringComparison.OrdinalIgnoreCase) ?? false;

    if (useVirtualKey)
    {
        logger.LogInformation("Virtual Key detected. Validating...");
        var virtualKey = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!);
        if (virtualKey == null)
        {
            logger.LogWarning("Virtual Key validation failed for key starting with: {Prefix}", originalApiKey!.Substring(0, Math.Min(originalApiKey.Length, 10)));
            // Return an OpenAI-compatible error response for invalid key
            return Results.Json(new OpenAIErrorResponse { Error = new OpenAIError { Message = "Invalid API Key provided.", Type = "invalid_request_error", Param = null, Code = "invalid_api_key" } }, statusCode: (int)HttpStatusCode.Unauthorized, options: jsonSerializerOptions);
        }

        logger.LogInformation("Virtual Key ID {KeyId} validated successfully.", virtualKey.Id);
        virtualKeyId = virtualKey.Id;
        apiKey = null; // *** CRITICAL: Do not pass the virtual key down to the actual provider ***
    }

    // --- Non-Streaming Path ---
    if (request.Stream != true) // Handle null or false
    {
        logger.LogInformation("Handling non-streaming request.");
        try
        {
            // Pass the actual provider apiKey (which might be null if virtual key was used)
            var response = await conduit.CreateChatCompletionAsync(request, apiKey, httpRequest.HttpContext.RequestAborted);
            // Ensure response is serialized correctly according to OpenAI spec

            // ---> TODO: Implement actual cost calculation based on response.Usage <--- 
            if (virtualKeyId.HasValue)
            {
                decimal cost = 0.01m; // Placeholder cost
                logger.LogInformation("Updating spend for Virtual Key ID {KeyId} by {Cost}", virtualKeyId.Value, cost);
                await virtualKeyService.UpdateSpendAsync(virtualKeyId.Value, cost);
            }

            return Results.Json(response, options: jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing non-streaming chat completion request.");
            // Map exceptions to OpenAI-compatible error JSON responses
            return MapExceptionToHttpResult(ex, logger);
        }
    }
    else
    {
        // --- Streaming Path ---
        logger.LogInformation("Handling streaming request.");
        httpResponse.Headers.ContentType = "text/event-stream";
        httpResponse.Headers.CacheControl = "no-cache";
        httpResponse.Headers.Connection = "keep-alive"; // Recommended for SSE

        try
        {
            // Pass the actual provider apiKey (which might be null if virtual key was used)
            await foreach (var chunk in conduit.StreamChatCompletionAsync(request, apiKey, httpRequest.HttpContext.RequestAborted)
                .WithCancellation(httpRequest.HttpContext.RequestAborted))
            {
                var chunkJson = JsonSerializer.Serialize(chunk, jsonSerializerOptions);
                await httpResponse.WriteAsync($"data: {chunkJson}\n\n", httpRequest.HttpContext.RequestAborted);
                await httpResponse.Body.FlushAsync(httpRequest.HttpContext.RequestAborted);
                logger.LogDebug("Sent stream chunk.");
            }

            // Send the [DONE] message
            await httpResponse.WriteAsync("data: [DONE]\n\n", httpRequest.HttpContext.RequestAborted);
            await httpResponse.Body.FlushAsync(httpRequest.HttpContext.RequestAborted);
            logger.LogInformation("Finished sending stream.");

            // ---> TODO: Implement actual cost calculation (might need accumulated usage data) <--- 
            if (virtualKeyId.HasValue)
            {
                decimal cost = 0.01m; // Placeholder cost - streaming calculation is harder
                logger.LogInformation("Updating spend for Virtual Key ID {KeyId} by {Cost} after stream completion", virtualKeyId.Value, cost);
                // Note: Spend update happens *after* the stream completes.
                await virtualKeyService.UpdateSpendAsync(virtualKeyId.Value, cost);
            }
        }
        catch (OperationCanceledException)
        {
            // If the stream fails or is cancelled, we might *not* want to charge spend, or handle it differently.
            // Current logic only updates spend on successful stream completion.
            if (virtualKeyId.HasValue)
            {
                logger.LogWarning("Streaming request cancelled for Virtual Key ID {KeyId}. Spend not updated for this partial stream.", virtualKeyId.Value);
            }
            logger.LogInformation("Streaming request cancelled by client.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing streaming chat completion request.");
            // Difficult to send a clean error response once streaming has started.
            // The connection might just be closed abruptly from the client's perspective.
            // Logging the error server-side is the most reliable action here.
            if (!httpResponse.HasStarted)
            {
                // If we haven't started writing the response yet, we can send a proper error code.
                // This is unlikely if the error happens mid-stream but possible if it occurs during setup.
                httpResponse.StatusCode = 500;
                // If we haven't started writing the response yet, we can try to send a proper error code.
                // This is unlikely if the error happens mid-stream but possible if it occurs during setup.
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                await httpResponse.WriteAsync("data: {\"error\": {\"message\": \"An internal server error occurred during streaming setup.\"}}\n\n", httpRequest.HttpContext.RequestAborted);
                await httpResponse.WriteAsync("data: [DONE]\n\n", httpRequest.HttpContext.RequestAborted); // Terminate stream
            }
            // Otherwise, the stream will likely just terminate abruptly.
        }

        // Return Empty result because the response is written directly to the stream
        return Results.Empty;
    }

}).WithTags("LLM Proxy");

// Add a configuration refresh endpoint
app.MapPost("/admin/refresh-configuration", async (
    [FromServices] IOptions<ConduitSettings> settingsOptions,
    [FromServices] ILogger<Program> logger,
    [FromServices] IDbContextFactory<ConfigurationDbContext> dbContextFactory) =>
{
    logger.LogInformation("Received configuration refresh request");
    
    try
    {
        // Get the settings instance from IOptions
        var settings = settingsOptions.Value;
        
        // Load the latest configuration from database
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        
        // Load provider credentials
        var providerCredsList = await dbContext.ProviderCredentials.ToListAsync();
        
        if (providerCredsList.Any())
        {
            logger.LogInformation("Refreshing {Count} provider credentials from database", providerCredsList.Count);
            
            // Convert database provider credentials to Core provider credentials
            var providersList = providerCredsList.Select(p => new ProviderCredentials
            {
                ProviderName = p.ProviderName,
                ApiKey = p.ApiKey,
                ApiVersion = p.ApiVersion,
                ApiBase = p.ApiBase
            }).ToList();
            
            // Replace in-memory credentials with database values
            if (settings.ProviderCredentials == null)
            {
                settings.ProviderCredentials = new List<ProviderCredentials>();
            }
            else
            {
                settings.ProviderCredentials.Clear();
            }
            
            settings.ProviderCredentials.AddRange(providersList);
            
            foreach (var cred in providersList)
            {
                logger.LogInformation("Refreshed credentials for provider: {ProviderName}", cred.ProviderName);
            }
        }
        
        // Load model mappings from database
        var modelMappings = await dbContext.ModelMappings.ToListAsync();
        
        if (modelMappings.Any())
        {
            logger.LogInformation("Refreshing {Count} model mappings from database", modelMappings.Count);
            
            // Convert database model mappings to settings model mappings
            var mappingsList = modelMappings.Select(m => new ModelProviderMapping
            {
                ModelAlias = m.ModelAlias,
                ProviderName = m.ProviderName,
                ProviderModelId = m.ProviderModelId
            }).ToList();
            
            // Replace in-memory mappings with database values
            if (settings.ModelMappings == null)
            {
                settings.ModelMappings = new List<ModelProviderMapping>();
            }
            else
            {
                settings.ModelMappings.Clear();
            }
            
            settings.ModelMappings.AddRange(mappingsList);
            
            foreach (var mapping in mappingsList)
            {
                logger.LogInformation("Refreshed model mapping: {ModelAlias} -> {ProviderName}/{ProviderModelId}",
                    mapping.ModelAlias, mapping.ProviderName, mapping.ProviderModelId);
            }
        }
        
        // Reset any client factory caches if needed
        // (We'll address this later if there's a caching issue)
        
        return Results.Ok(new { success = true, message = "Configuration refreshed successfully" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error refreshing configuration from database");
        return Results.Problem(
            title: "Configuration Refresh Failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
}).WithTags("LLM Proxy Administration");

// --- Endpoint to List Models for a Provider ---
app.MapGet("/api/providers/{providerName}/models", async (
    string providerName,
    [FromServices] ILLMClientFactory clientFactory,
    [FromServices] IDbContextFactory<ConfigurationDbContext> dbContextFactory, // Inject DbContextFactory
    [FromServices] ILogger<Program> logger,
    HttpRequest httpRequest) =>
{
    logger.LogInformation("Received /api/providers/{ProviderName}/models request", providerName);

    string? apiKeyFromHeader = null;
    if (httpRequest.Headers.TryGetValue("Authorization", out var authHeader) &&
        authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        apiKeyFromHeader = authHeader.ToString().Substring("Bearer ".Length).Trim();
        logger.LogDebug("Extracted API Key from Authorization header.");
    }

    string? apiKeyFromDb = null;
    try
    {
        // Get credentials from the database
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(httpRequest.HttpContext.RequestAborted);
        var providerCreds = await dbContext.ProviderCredentials
                                    .AsNoTracking() // Read-only operation
                                    .FirstOrDefaultAsync(p => p.ProviderName.ToLower() == providerName.ToLower(),
                                                         httpRequest.HttpContext.RequestAborted);

        if (providerCreds == null)
        {
            logger.LogWarning("Provider configuration not found in database for '{ProviderName}'.", providerName);
            // Return 404 Not Found if provider config doesn't exist in DB
            return Results.NotFound(new { message = $"Provider configuration not found for '{providerName}'." });
        }
        apiKeyFromDb = providerCreds.ApiKey;

        // Determine the effective API key: Header takes precedence
        string? effectiveApiKey = !string.IsNullOrWhiteSpace(apiKeyFromHeader) ? apiKeyFromHeader : apiKeyFromDb;

        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            // Log a warning if no key is available from header or DB
            logger.LogWarning("No API Key available for provider '{ProviderName}' from header or database. Model listing might fail.", providerName);
            // Depending on provider, listing might still work without a key, but often won't.
            // Proceed, but the client call might fail.
        }
        else
        {
             var source = !string.IsNullOrWhiteSpace(apiKeyFromHeader) ? "Header" : "Database";
             logger.LogDebug("Using effective API Key for model listing (source: {Source}).", source);
        }


        // Create a client instance
        var client = clientFactory.GetClientByProvider(providerName);

        // Use the effective API key
        var models = await client.ListModelsAsync(effectiveApiKey, httpRequest.HttpContext.RequestAborted);
        return Results.Ok(models);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Model listing request cancelled for {ProviderName}.", providerName);
        return Results.StatusCode(499); // Client Closed Request
    }
    catch (Exception ex)
    {
        // Use the existing helper to map exceptions
        logger.LogError(ex, "Error listing models for provider {ProviderName}.", providerName);
        return MapExceptionToHttpResult(ex, logger);
    }

}).WithTags("LLM Proxy Configuration");

// Helper function to map exceptions to IResult
// Removed 'static' modifier to allow access to jsonSerializerOptions
IResult MapExceptionToHttpResult(Exception ex, ILogger logger)
{
    logger.LogError(ex, "Error processing chat completion request.");

    HttpStatusCode statusCode;
    OpenAIError error;

    switch (ex)
    {
        case ConfigurationException confEx:
            statusCode = HttpStatusCode.InternalServerError; // Or BadRequest if it's user config error? 500 seems safer.
            error = new OpenAIError { Message = confEx.Message, Type = "server_error", Param = null, Code = "configuration_error" };
            break;
        case UnsupportedProviderException unsupEx:
            statusCode = HttpStatusCode.BadRequest;
            error = new OpenAIError { Message = unsupEx.Message, Type = "invalid_request_error", Param = "model", Code = "unsupported_provider" };
            break;
        case LLMCommunicationException commEx:
            // Use the status code from the inner exception if available and it's an HTTP error
            statusCode = commEx.StatusCode ?? HttpStatusCode.ServiceUnavailable;
            error = new OpenAIError { Message = commEx.Message, Type = "api_error", Param = null, Code = "llm_communication_error" };
            break;
        case ConduitException liteEx: // Catch-all for other library-specific errors
            statusCode = HttpStatusCode.InternalServerError;
            error = new OpenAIError { Message = liteEx.Message, Type = "server_error", Param = null, Code = "internal_lite_llm_error" };
            break;
        case JsonException jsonEx: // Handle request deserialization errors
             statusCode = HttpStatusCode.BadRequest;
             error = new OpenAIError { Message = $"Invalid request body: {jsonEx.Message}", Type = "invalid_request_error", Param = null, Code = "invalid_json" };
             break;
        default:
            statusCode = HttpStatusCode.InternalServerError;
            error = new OpenAIError { Message = "An unexpected internal server error occurred.", Type = "server_error", Param = null, Code = "unexpected_error" };
            break;
    }

    return Results.Json(new OpenAIErrorResponse { Error = error }, statusCode: (int)statusCode, options: jsonSerializerOptions);
}

app.Run();

// --- Helper Types ---
// Moved here to comply with CS8803 (Top-level statements must precede type declarations)

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
    private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
    private readonly IOptions<ConduitSettings> _settingsOptions;
    private readonly ILogger<DatabaseSettingsStartupFilter> _logger;

    public DatabaseSettingsStartupFilter(
        IDbContextFactory<ConfigurationDbContext> dbContextFactory,
        IOptions<ConduitSettings> settingsOptions,
        ILogger<DatabaseSettingsStartupFilter> logger)
    {
        _dbContextFactory = dbContextFactory;
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
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            // Load provider credentials
            var providerCredsList = await dbContext.ProviderCredentials.ToListAsync();
            if (providerCredsList.Any())
            {
                _logger.LogInformation("Found {Count} provider credentials in database", providerCredsList.Count);
                
                // Convert database provider credentials to Core provider credentials
                var providersList = providerCredsList.Select(p => new ProviderCredentials
                {
                    ProviderName = p.ProviderName,
                    ApiKey = p.ApiKey,
                    ApiVersion = p.ApiVersion,
                    ApiBase = p.ApiBase // Corrected property name to match DbProviderCredentials
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
                
                // Log the loaded credentials
                foreach (var cred in providersList)
                {
                    _logger.LogInformation("Loaded credentials for provider: {ProviderName}", cred.ProviderName);
                }
            }
            else
            {
                _logger.LogWarning("No provider credentials found in database");
            }

            // Load model mappings from database
            var modelMappings = await dbContext.ModelMappings.ToListAsync();
            if (modelMappings.Any())
            {
                _logger.LogInformation("Found {Count} model mappings in database", modelMappings.Count);
                
                // Convert database model mappings to settings model mappings
                var mappingsList = modelMappings.Select(m => new ModelProviderMapping
                {
                    ModelAlias = m.ModelAlias,
                    ProviderName = m.ProviderName,
                    ProviderModelId = m.ProviderModelId
                }).ToList();

                // Initialize or clear the existing mappings
                if (settings.ModelMappings == null)
                {
                    settings.ModelMappings = new List<ModelProviderMapping>();
                }
                else
                {
                    // Replace all in-memory mappings with database ones
                    settings.ModelMappings.Clear();
                }
                
                // Add all database mappings
                settings.ModelMappings.AddRange(mappingsList);
                
                // Log the loaded mappings
                foreach (var mapping in mappingsList)
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
