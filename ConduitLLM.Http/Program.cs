using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonNamingPolicy

using ConduitLLM.Configuration;
using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions; // Add namespace for custom exceptions
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers; // Assuming LLMClientFactory is here
using ConduitLLM.Providers.Extensions; // Add namespace for HttpClient extensions

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Added for EF Core
using Microsoft.Extensions.Options; // Added for IOptions
using Microsoft.AspNetCore.RateLimiting;
using ConduitLLM.Http.Security;

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
var (dbProvider, dbConnectionString) = DbConnectionHelper.GetProviderAndConnectionString();
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

var app = builder.Build();

// Log database configuration ONCE, avoid duplicate logger declarations
var dbLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbConnection");
DbConnectionHelper.GetProviderAndConnectionString(msg => dbLogger.LogInformation(msg));

// --- AUTOMATIC DATABASE MIGRATION ---
if (useEnsureCreated)
{
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Initializing database with EnsureCreated (simpler approach)");
        
        try
        {
            // Use EnsureCreated for simpler database initialization
            var configDb = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.ConfigurationDbContext>();
            
            // Retry pattern with exponential backoff
            const int maxRetries = 10;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    // Connect to database and create schema if needed
                    if (!configDb.Database.CanConnect())
                    {
                        logger.LogInformation("Cannot connect to database. Waiting and retrying...");
                        Thread.Sleep(2000); // Brief delay
                        continue;
                    }
                    
                    logger.LogInformation("Connected to database. Creating schema with EnsureCreated...");
                    configDb.Database.EnsureCreated();
                    logger.LogInformation("Database schema created successfully");
                    break;
                }
                catch (Exception ex) when (retry < maxRetries - 1)
                {
                    int delay = (int)(1000 * Math.Pow(2, retry));
                    logger.LogWarning(ex, "Database initialization attempt {Retry} failed. Retrying in {Delay}ms...", 
                        retry + 1, delay);
                    Thread.Sleep(delay); // Exponential backoff
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during database initialization");
            // Don't throw to allow application to start anyway
        }
    }
}
else if (shouldApplyMigrations)
{
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Starting database migration process...");
        
        try
        {
            // Apply migrations with retry logic
            var configDb = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.ConfigurationDbContext>();
            
            // Retry pattern with exponential backoff
            const int maxRetries = 10;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    // First check if database exists
                    if (!configDb.Database.CanConnect())
                    {
                        logger.LogInformation("Database cannot be connected to. Creating...");
                        
                        // Use EnsureCreated instead of Migrate when database doesn't exist yet
                        // This creates the schema without using migrations history
                        configDb.Database.EnsureCreated();
                        logger.LogInformation("Database created successfully using EnsureCreated");
                        
                        // Since we created the database directly, we can skip migration
                        break;
                    }
                    
                    // Check if migrations history table exists
                    bool migrationsHistoryExists = false;
                    try 
                    {
                        // Try to access the migrations history table
                        migrationsHistoryExists = configDb.Database.GetAppliedMigrations().Any();
                    }
                    catch 
                    {
                        // Table doesn't exist, which is fine for a fresh database
                        migrationsHistoryExists = false;
                    }
                    
                    if (!migrationsHistoryExists)
                    {
                        logger.LogInformation("Migrations history table doesn't exist. Creating database schema directly...");
                        // Drop the database first to avoid partial schema issues
                        configDb.Database.EnsureDeleted();
                        configDb.Database.EnsureCreated();
                        logger.LogInformation("Database schema created successfully");
                        break;
                    }

                    // If we get here, migrations history exists, so try applying pending migrations
                    var pendingMigrations = configDb.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Any())
                    {
                        logger.LogInformation("Applying {count} pending migrations: {migrations}", 
                            pendingMigrations.Count, 
                            string.Join(", ", pendingMigrations));
                        configDb.Database.Migrate();
                        logger.LogInformation("Migrations applied successfully");
                    }
                    else
                    {
                        logger.LogInformation("No pending migrations found");
                    }
                    
                    // Migration successful, exit the retry loop
                    break;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning") && retry < maxRetries - 1)
                {
                    logger.LogWarning(ex, "Pending model changes detected. Attempting alternative database initialization.");
                    
                    try 
                    {
                        // When we have pending model changes warning, try to apply the migration we created
                        logger.LogInformation("Attempting to apply the UpdateConfigurationDbContext migration...");
                        
                        // Execute a raw SQL command to ensure the migrations history table exists
                        configDb.Database.ExecuteSql($@"
                            CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                                MigrationId character varying(150) NOT NULL,
                                ProductVersion character varying(32) NOT NULL,
                                CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
                            )
                        ");
                        
                        // Check if this specific migration is already applied
                        var pendingMigrations = configDb.Database.GetPendingMigrations().ToList();
                        if (pendingMigrations.Any(m => m.Contains("UpdateConfigurationDbContext")))
                        {
                            logger.LogInformation("Applying UpdateConfigurationDbContext migration...");
                            // Apply just this migration
                            configDb.Database.ExecuteSql($@"
                                -- Manually mark this migration as applied
                                INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
                                VALUES ('20250429073337_UpdateConfigurationDbContext', '9.0.4')
                                ON CONFLICT DO NOTHING
                            ");
                            logger.LogInformation("Migration record added to history table");
                        }
                        
                        // Last resort: If all else fails, recreate the database
                        if (retry >= maxRetries - 2)
                        {
                            logger.LogWarning("Using EnsureCreated as final fallback for database with pending model changes");
                            configDb.Database.EnsureDeleted();
                            configDb.Database.EnsureCreated();
                            logger.LogInformation("Database created successfully using EnsureCreated fallback");
                        }
                        
                        break;
                    }
                    catch (Exception fallbackEx)
                    {
                        logger.LogError(fallbackEx, "Failed to initialize database with migration fallback");
                        // Continue with retry loop
                    }
                    
                    int delay = (int)(1000 * Math.Pow(2, retry));
                    logger.LogWarning("Retrying after delay of {Delay}ms...", delay);
                    Thread.Sleep(delay);
                }
                catch (Exception ex) when (retry < maxRetries - 1)
                {
                    int delay = (int)(1000 * Math.Pow(2, retry));
                    logger.LogWarning(ex, "Migration attempt {Retry} failed. Retrying in {Delay}ms...", retry + 1, delay);
                    Thread.Sleep(delay); // Exponential backoff
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during database migration");
            // Fail the container startup if migrations cannot be applied
            throw; 
        }
    }
}
else
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            // Apply migrations for all relevant DbContexts
            var configDb = scope.ServiceProvider.GetRequiredService<ConduitLLM.Configuration.ConfigurationDbContext>();
            configDb.Database.Migrate();
            
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred during migration.");
            
            // Don't throw, allow application to continue even if migrations failed
            // The error will be recorded in logs but won't prevent the service from starting
        }
    }
}

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

// app.UseHttpsRedirection(); // Removed as HTTPS is handled by external proxy (e.g., Railway)

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// --- API Endpoints ---

app.MapPost("/v1/chat/completions", async (
    [FromBody] ChatCompletionRequest request,
    [FromServices] Conduit conduit,
    [FromServices] ILogger<Program> logger,
    [FromServices] IVirtualKeyService virtualKeyService, // Inject Virtual Key Service
    [FromServices] ICostCalculationService costCalculator, // Inject Cost Calculation Service
    HttpRequest httpRequest,
    HttpResponse httpResponse) =>
{
    logger.LogInformation("Received /v1/chat/completions request for model: {Model}", request.Model);

    // 1. Extract API Key from header
    string? apiKey = null;
    string? originalApiKey = null; // Store the original key for virtual key check

    if (httpRequest.Headers.TryGetValue("Authorization", out var authHeader) && authHeader.Count > 0)
    {
        // Check if it's a Bearer token
        string auth = authHeader.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            originalApiKey = auth.Substring("Bearer ".Length).Trim();
        }
        else
        {
            originalApiKey = auth.Trim(); // Just in case they sent the raw key
        }
    }

    // --- Virtual Key Check & Validation ---
    ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = null;
    int? virtualKeyId = null; // Store the ID if a virtual key is used
    bool useVirtualKey = originalApiKey?.StartsWith("condt_", StringComparison.OrdinalIgnoreCase) ?? false;

    if (useVirtualKey)
    {
        logger.LogInformation("Virtual Key detected. Validating...");
        // First validate just to get the entity without model check
        virtualKey = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!);
        if (virtualKey == null)
        {
            logger.LogWarning("Invalid or disabled Virtual Key provided.");
            return Results.Json(new OpenAIErrorResponse 
            { 
                Error = new OpenAIError 
                { 
                    Message = "Invalid API key provided", 
                    Type = "invalid_request_error", 
                    Code = "invalid_api_key" 
                } 
            }, statusCode: (int)HttpStatusCode.Unauthorized, options: jsonSerializerOptions);
        }

        virtualKeyId = virtualKey.Id;

        // --- Budget Reset Check ---
        bool budgetWasReset = await virtualKeyService.ResetBudgetIfExpiredAsync(virtualKey.Id, httpRequest.HttpContext.RequestAborted);
        if (budgetWasReset)
        {
            // Reload the key entity to get the updated spend/start date after reset
            virtualKey = await virtualKeyService.GetVirtualKeyInfoForValidationAsync(virtualKey.Id, httpRequest.HttpContext.RequestAborted);
            
            if (virtualKey == null)
            {
                logger.LogError("Virtual key ID {KeyId} disappeared during budget reset", virtualKeyId.Value);
                return Results.Json(new OpenAIErrorResponse 
                { 
                    Error = new OpenAIError 
                    { 
                        Message = "An error occurred processing your request", 
                        Type = "server_error", 
                        Code = "internal_error" 
                    } 
                }, statusCode: (int)HttpStatusCode.InternalServerError, options: jsonSerializerOptions);
            }
            
            logger.LogInformation("Budget was reset for key ID {KeyId}. New budget period started.", virtualKeyId.Value);
        }

        // --- Budget Limit Check ---
        if (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value)
        {
            logger.LogWarning("Virtual key budget exceeded for Key ID {KeyId}. Current: {CurrentSpend}, Max: {MaxBudget}",
                virtualKey.Id, virtualKey.CurrentSpend, virtualKey.MaxBudget.Value);
            
            // Use 402 Payment Required for budget issues
            return Results.Json(new OpenAIErrorResponse 
            { 
                Error = new OpenAIError 
                { 
                    Message = "This key's budget has been exceeded.", 
                    Type = "insufficient_quota", 
                    Code = "billing_hard_limit_reached" 
                } 
            }, statusCode: StatusCodes.Status402PaymentRequired, options: jsonSerializerOptions);
        }

        // --- Model Access Check ---
        if (!string.IsNullOrEmpty(request.Model) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
        {
            // Check if the requested model is allowed for this key
            // Reusing ValidateVirtualKeyAsync with the model parameter
            var validationWithModel = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!, request.Model);
            if (validationWithModel == null)
            {
                logger.LogWarning("Virtual key {KeyId} attempted to access restricted model: {Model}", virtualKey.Id, request.Model);
                return Results.Json(new OpenAIErrorResponse 
                { 
                    Error = new OpenAIError 
                    { 
                        Message = $"The model `{request.Model}` is not allowed for this key.", 
                        Type = "invalid_request_error", 
                        Code = "model_not_allowed" 
                    } 
                }, statusCode: (int)HttpStatusCode.Forbidden, options: jsonSerializerOptions);
            }
        }

        apiKey = null; // *** CRITICAL: Do not pass the virtual key down to the actual provider ***
    }
    else
    {
        apiKey = originalApiKey; // Use the provided key directly
    }

    // --- Non-Streaming Path ---
    if (request.Stream != true) // Handle null or false
    {
        logger.LogInformation("Handling non-streaming request.");
        try
        {
            // Pass the actual provider apiKey (which might be null if virtual key was used)
            var response = await conduit.CreateChatCompletionAsync(request, apiKey, httpRequest.HttpContext.RequestAborted);
            
            // --- Cost Tracking for Virtual Keys ---
            if (virtualKeyId.HasValue && response.Usage != null)
            {
                decimal calculatedCost = await costCalculator.CalculateCostAsync(
                    response.Model, response.Usage, httpRequest.HttpContext.RequestAborted);
                
                if (calculatedCost > 0)
                {
                    logger.LogInformation("Updating spend for Virtual Key ID {KeyId} by {Cost}", virtualKeyId.Value, calculatedCost);
                    bool spendUpdated = await virtualKeyService.UpdateSpendAsync(virtualKeyId.Value, calculatedCost);
                    
                    if (!spendUpdated)
                    {
                        logger.LogError("Failed to update spend for Virtual Key ID {KeyId}", virtualKeyId.Value);
                        // Continue despite failure - logging is sufficient, don't block response
                    }
                }
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
            // Capture the requested model ID for cost calculation
            string responseModelId = request.Model ?? string.Empty;

            // Initialize variables to accumulate token counts
            // Note: We have to track this manually as streaming chunks usually don't include usage info
            int totalPromptTokens = 0;
            int totalCompletionTokens = 0;
            bool hasReceivedUsage = false;

            // Pass the actual provider apiKey (which might be null if virtual key was used)
            await foreach (var chunk in conduit.StreamChatCompletionAsync(request, apiKey, httpRequest.HttpContext.RequestAborted)
                            .WithCancellation(httpRequest.HttpContext.RequestAborted)) 
            {
                if (httpRequest.HttpContext.RequestAborted.IsCancellationRequested)
                {
                    logger.LogInformation("Stream was cancelled.");
                    break;
                }

                // Update model ID if provided in the response
                if (!string.IsNullOrEmpty(chunk.Model))
                {
                    responseModelId = chunk.Model;
                }

                // Check if this chunk contains a message with usage info
                // Some providers send usage in a special message structure
                if (chunk.Choices != null && chunk.Choices.Count > 0)
                {
                    // Extract completion content length as a rough approximation for token count
                    // This is a fallback when no explicit usage info is provided
                    if (chunk.Choices[0].Delta?.Content != null)
                    {
                        totalCompletionTokens += 1; // Increment by a small value per chunk
                    }
                }
                
                // Serialize to JSON and append SSE prefix
                string jsonChunk = JsonSerializer.Serialize(chunk, jsonSerializerOptions);
                await httpResponse.WriteAsync($"data: {jsonChunk}\n\n", httpRequest.HttpContext.RequestAborted);
                await httpResponse.Body.FlushAsync(httpRequest.HttpContext.RequestAborted);
            }

            // Streaming end marker
            await httpResponse.WriteAsync("data: [DONE]\n\n", httpRequest.HttpContext.RequestAborted);
            await httpResponse.Body.FlushAsync(httpRequest.HttpContext.RequestAborted);
            logger.LogInformation("Finished sending stream.");

            // If we haven't received explicit usage info, use the request's messages to estimate prompt tokens
            if (!hasReceivedUsage && request.Messages != null)
            {
                // Rough estimation: about 4 tokens per word
                totalPromptTokens = request.Messages.Sum(m => (m.Content?.ToString()?.Length ?? 0) / 4);
            }

            // Create final usage object for cost calculation
            var finalUsage = new Usage
            {
                PromptTokens = totalPromptTokens,
                CompletionTokens = totalCompletionTokens,
                TotalTokens = totalPromptTokens + totalCompletionTokens
            };

            // For streaming requests, calculate actual cost based on accumulated usage
            if (virtualKeyId.HasValue)
            {
                // Calculate actual cost using the cost calculation service
                decimal calculatedCost = await costCalculator.CalculateCostAsync(responseModelId, finalUsage, httpRequest.HttpContext.RequestAborted);
                
                logger.LogInformation("Updating spend for Virtual Key ID {KeyId} by {Cost} after stream completion", 
                    virtualKeyId.Value, calculatedCost);
                
                await virtualKeyService.UpdateSpendAsync(virtualKeyId.Value, calculatedCost);
            }
        }
        catch (OperationCanceledException)
        {
            // If the stream fails or is cancelled, we might *not* want to charge spend, or handle it differently.
            if (!httpResponse.HasStarted)
            {
                // If we haven't sent any data yet, we can still return a proper response
                logger.LogInformation("Stream cancelled before sending any data.");
                httpResponse.Headers.ContentType = "application/json";
                return Results.Json(new OpenAIErrorResponse
                {
                    Error = new OpenAIError { Message = "Request was cancelled", Type = "invalid_request_error" }
                }, statusCode: (int)HttpStatusCode.RequestTimeout, options: jsonSerializerOptions);
            }
            logger.LogInformation("Streaming request cancelled by client.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing streaming chat completion request.");
            // Difficult to send a clean error response once streaming has started.
            if (!httpResponse.HasStarted)
            {
                // Only send error result if we haven't started sending stream data
                httpResponse.Headers.ContentType = "application/json";
                return Results.Json(new OpenAIErrorResponse 
                { 
                    Error = new OpenAIError 
                    { 
                        Message = $"Streaming error: {ex.Message}", 
                        Type = "server_error", 
                        Code = ex is ConduitException ? "llm_error" : "internal_error" 
                    } 
                }, statusCode: (int)(ex is LLMCommunicationException commEx ? commEx.StatusCode ?? HttpStatusCode.InternalServerError : HttpStatusCode.InternalServerError), 
                options: jsonSerializerOptions);
            }
            // Otherwise, the stream will likely just terminate abruptly.
        }

        // Return Empty result because the response is written directly to the stream
        return Results.Empty;
    }
}).WithTags("LLM Proxy");

app.MapPost("/v1/embeddings", async (
    [FromBody] EmbeddingRequest request,
    [FromServices] Conduit conduit,
    [FromServices] ILogger<Program> logger,
    [FromServices] IVirtualKeyService virtualKeyService,
    [FromServices] ICostCalculationService costCalculator, // Inject Cost Calculation Service
    HttpRequest httpRequest) =>
{
    logger.LogInformation("Received /v1/embeddings request for model: {Model}", request.Model);

    // 1. Extract API Key from header
    string? apiKey = null;
    string? originalApiKey = null; // Store the original key for virtual key check

    if (httpRequest.Headers.TryGetValue("Authorization", out var authHeader) && authHeader.Count > 0)
    {
        // Check if it's a Bearer token
        string auth = authHeader.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            originalApiKey = auth.Substring("Bearer ".Length).Trim();
        }
        else
        {
            originalApiKey = auth.Trim(); // Just in case they sent the raw key
        }
    }

    // --- Virtual Key Check & Validation ---
    ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = null;
    int? virtualKeyId = null; // Store the ID if a virtual key is used
    bool useVirtualKey = originalApiKey?.StartsWith("condt_", StringComparison.OrdinalIgnoreCase) ?? false;

    if (useVirtualKey)
    {
        logger.LogInformation("Virtual Key detected. Validating...");
        // First validate just to get the entity without model check
        virtualKey = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!);
        if (virtualKey == null)
        {
            logger.LogWarning("Invalid or disabled Virtual Key provided.");
            return Results.Json(new OpenAIErrorResponse 
            { 
                Error = new OpenAIError 
                { 
                    Message = "Invalid API key provided", 
                    Type = "invalid_request_error", 
                    Code = "invalid_api_key" 
                } 
            }, statusCode: (int)HttpStatusCode.Unauthorized, options: jsonSerializerOptions);
        }

        virtualKeyId = virtualKey.Id;

        // --- Budget Reset Check ---
        bool budgetWasReset = await virtualKeyService.ResetBudgetIfExpiredAsync(virtualKey.Id, httpRequest.HttpContext.RequestAborted);
        if (budgetWasReset)
        {
            // Reload the key entity to get the updated spend/start date after reset
            virtualKey = await virtualKeyService.GetVirtualKeyInfoForValidationAsync(virtualKey.Id, httpRequest.HttpContext.RequestAborted);
            
            if (virtualKey == null)
            {
                logger.LogError("Virtual key ID {KeyId} disappeared during budget reset", virtualKeyId.Value);
                return Results.Json(new OpenAIErrorResponse 
                { 
                    Error = new OpenAIError 
                    { 
                        Message = "An error occurred processing your request", 
                        Type = "server_error", 
                        Code = "internal_error" 
                    } 
                }, statusCode: (int)HttpStatusCode.InternalServerError, options: jsonSerializerOptions);
            }
            
            logger.LogInformation("Budget was reset for key ID {KeyId}. New budget period started.", virtualKeyId.Value);
        }

        // --- Budget Limit Check ---
        if (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value)
        {
            logger.LogWarning("Virtual key budget exceeded for Key ID {KeyId}. Current: {CurrentSpend}, Max: {MaxBudget}",
                virtualKey.Id, virtualKey.CurrentSpend, virtualKey.MaxBudget.Value);
            
            // Use 402 Payment Required for budget issues
            return Results.Json(new OpenAIErrorResponse 
            { 
                Error = new OpenAIError 
                { 
                    Message = "This key's budget has been exceeded.", 
                    Type = "insufficient_quota", 
                    Code = "billing_hard_limit_reached" 
                } 
            }, statusCode: StatusCodes.Status402PaymentRequired, options: jsonSerializerOptions);
        }

        // --- Model Access Check ---
        if (!string.IsNullOrEmpty(request.Model) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
        {
            // Check if the requested model is allowed for this key
            // Reusing ValidateVirtualKeyAsync with the model parameter
            var validationWithModel = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!, request.Model);
            if (validationWithModel == null)
            {
                logger.LogWarning("Virtual key {KeyId} attempted to access restricted model: {Model}", virtualKey.Id, request.Model);
                return Results.Json(new OpenAIErrorResponse 
                { 
                    Error = new OpenAIError 
                    { 
                        Message = $"The model `{request.Model}` is not allowed for this key.", 
                        Type = "invalid_request_error", 
                        Code = "model_not_allowed" 
                    } 
                }, statusCode: (int)HttpStatusCode.Forbidden, options: jsonSerializerOptions);
            }
        }

        apiKey = null; // *** CRITICAL: Do not pass the virtual key down to the actual provider ***
    }
    else
    {
        apiKey = originalApiKey; // Use the provided key directly
    }

    try
    {
        var response = await conduit.CreateEmbeddingAsync(request, apiKey, httpRequest.HttpContext.RequestAborted);
        
        // --- Cost Tracking for Virtual Keys ---
        if (virtualKeyId.HasValue && response.Usage != null)
        {
            decimal calculatedCost = await costCalculator.CalculateCostAsync(
                response.Model, response.Usage, httpRequest.HttpContext.RequestAborted);
            
            if (calculatedCost > 0)
            {
                logger.LogInformation("Updating spend for Virtual Key ID {KeyId} by {Cost}", virtualKeyId.Value, calculatedCost);
                bool spendUpdated = await virtualKeyService.UpdateSpendAsync(virtualKeyId.Value, calculatedCost);
                
                if (!spendUpdated)
                {
                    logger.LogError("Failed to update spend for Virtual Key ID {KeyId}", virtualKeyId.Value);
                    // Continue despite failure - logging is sufficient, don't block response
                }
            }
        }
        
        return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing embedding request.");
        return MapExceptionToHttpResult(ex, logger);
    }
}).WithTags("LLM Proxy");

app.MapPost("/v1/images/generations", async (
    [FromBody] ImageGenerationRequest request,
    [FromServices] Conduit conduit,
    [FromServices] ILogger<Program> logger,
    [FromServices] IVirtualKeyService virtualKeyService,
    [FromServices] ICostCalculationService costCalculator, // Inject Cost Calculation Service
    HttpRequest httpRequest) =>
{
    logger.LogInformation("Received /v1/images/generations request for model: {Model}", request.Model);
    
    // 1. Extract API Key from header
    string? apiKey = null;
    string? originalApiKey = null; // Store the original key for virtual key check

    if (httpRequest.Headers.TryGetValue("Authorization", out var authHeader) &&
        authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        originalApiKey = authHeader.ToString().Substring("Bearer ".Length).Trim();
        logger.LogDebug("Extracted API Key from Authorization header.");
    }

    // --- Virtual Key Check & Validation ---
    ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = null;
    int? virtualKeyId = null; // Store the ID if a virtual key is used
    bool useVirtualKey = originalApiKey?.StartsWith("condt_", StringComparison.OrdinalIgnoreCase) ?? false;

    if (useVirtualKey)
    {
        logger.LogInformation("Virtual Key detected. Validating...");
        // First validate just to get the entity without model check
        virtualKey = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!);
        if (virtualKey == null)
        {
            logger.LogWarning("Invalid or disabled Virtual Key provided.");
            return Results.Json(new OpenAIErrorResponse 
            { 
                Error = new OpenAIError 
                { 
                    Message = "Invalid API key provided", 
                    Type = "invalid_request_error", 
                    Code = "invalid_api_key" 
                } 
            }, statusCode: (int)HttpStatusCode.Unauthorized, options: jsonSerializerOptions);
        }

        virtualKeyId = virtualKey.Id;

        // --- Budget Reset Check ---
        bool budgetWasReset = await virtualKeyService.ResetBudgetIfExpiredAsync(virtualKey.Id, httpRequest.HttpContext.RequestAborted);
        if (budgetWasReset)
        {
            // Reload the key entity to get the updated spend/start date after reset
            virtualKey = await virtualKeyService.GetVirtualKeyInfoForValidationAsync(virtualKey.Id, httpRequest.HttpContext.RequestAborted);
            
            if (virtualKey == null)
            {
                logger.LogError("Virtual key ID {KeyId} disappeared during budget reset", virtualKeyId.Value);
                return Results.Json(new OpenAIErrorResponse 
                { 
                    Error = new OpenAIError 
                    { 
                        Message = "An error occurred processing your request", 
                        Type = "server_error", 
                        Code = "internal_error" 
                    } 
                }, statusCode: (int)HttpStatusCode.InternalServerError, options: jsonSerializerOptions);
            }
            
            logger.LogInformation("Budget was reset for key ID {KeyId}. New budget period started.", virtualKeyId.Value);
        }

        // --- Budget Limit Check ---
        if (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value)
        {
            logger.LogWarning("Virtual key budget exceeded for Key ID {KeyId}. Current: {CurrentSpend}, Max: {MaxBudget}",
                virtualKey.Id, virtualKey.CurrentSpend, virtualKey.MaxBudget.Value);
            
            // Use 402 Payment Required for budget issues
            return Results.Json(new OpenAIErrorResponse 
            { 
                Error = new OpenAIError 
                { 
                    Message = "This key's budget has been exceeded.", 
                    Type = "insufficient_quota", 
                    Code = "billing_hard_limit_reached" 
                } 
            }, statusCode: StatusCodes.Status402PaymentRequired, options: jsonSerializerOptions);
        }

        // --- Model Access Check ---
        if (!string.IsNullOrEmpty(request.Model) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
        {
            // Check if the requested model is allowed for this key
            // Reusing ValidateVirtualKeyAsync with the model parameter
            var validationWithModel = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!, request.Model);
            if (validationWithModel == null)
            {
                logger.LogWarning("Virtual key {KeyId} attempted to access restricted model: {Model}", virtualKey.Id, request.Model);
                return Results.Json(new OpenAIErrorResponse 
                { 
                    Error = new OpenAIError 
                    { 
                        Message = $"The model `{request.Model}` is not allowed for this key.", 
                        Type = "invalid_request_error", 
                        Code = "model_not_allowed" 
                    } 
                }, statusCode: (int)HttpStatusCode.Forbidden, options: jsonSerializerOptions);
            }
        }

        apiKey = null; // Don't pass virtual key down
    }
    else
    {
        apiKey = originalApiKey;
    }

    try
    {
        var response = await conduit.CreateImageAsync(request, apiKey, httpRequest.HttpContext.RequestAborted);
        
        // --- Cost Tracking for Virtual Keys ---
        if (virtualKeyId.HasValue)
        {
            // Create a synthetic usage object for cost calculation
            var syntheticUsage = new Usage
            {
                PromptTokens = request.Prompt.Length, // Approximate from prompt length
                CompletionTokens = 0,
                TotalTokens = request.Prompt.Length,
                // Only ImageCount is available in the Usage class
                ImageCount = request.N
            };
            
            // Pass additional metadata as part of the model ID string
            // This allows the cost calculator to consider image size and quality
            string modelWithMetadata = $"{request.Model}:{request.Size ?? "1024x1024"}:{request.Quality ?? "standard"}";
            
            decimal calculatedCost = await costCalculator.CalculateCostAsync(
                modelWithMetadata, syntheticUsage, httpRequest.HttpContext.RequestAborted);
            
            if (calculatedCost > 0)
            {
                logger.LogInformation("Updating spend for Virtual Key ID {KeyId} by {Cost} for image generation", 
                    virtualKeyId.Value, calculatedCost);
                bool spendUpdated = await virtualKeyService.UpdateSpendAsync(virtualKeyId.Value, calculatedCost);
                
                if (!spendUpdated)
                {
                    logger.LogError("Failed to update spend for Virtual Key ID {KeyId}", virtualKeyId.Value);
                    // Continue despite failure - logging is sufficient, don't block response
                }
            }
        }
        
        return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing image generation request");
        return MapExceptionToHttpResult(ex, logger);
    }
}).WithTags("LLM Proxy");

// Add a configuration refresh endpoint
app.MapPost("/admin/refresh-configuration", async (
    [FromServices] IOptions<ConduitSettings> settingsOptions,
    [FromServices] ILogger<Program> logger,
    [FromServices] IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configDbContextFactory, // Inject Config factory
    HttpRequest httpRequest) => // Removed WebUI factory injection
{
    logger.LogInformation("Received configuration refresh request"); // Correct LogInformation call
    
    try
    {
        // Get the settings instance from IOptions
        var settings = settingsOptions.Value;
        
        // Load provider credentials from Config context
        await using var configDbContext = await configDbContextFactory.CreateDbContextAsync();
        var providerCredsList = await configDbContext.ProviderCredentials.ToListAsync();
        
        if (providerCredsList.Any())
        {
            logger.LogInformation("Refreshing {Count} provider credentials from database", providerCredsList.Count); // Correct LogInformation call
            
            // Convert database provider credentials to Core provider credentials
            var providersList = providerCredsList.Select(p => new ProviderCredentials
            {
                ProviderName = p.ProviderName,
                ApiKey = p.ApiKey,
                ApiVersion = p.ApiVersion,
                ApiBase = p.BaseUrl // Correctly map BaseUrl from DB entity
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
                logger.LogInformation("Refreshed credentials for provider: {ProviderName}", cred.ProviderName);
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
    [FromServices] IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configDbContextFactory, // Inject Config factory
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
        // Get credentials from the Config database
        await using var configDbContext = await configDbContextFactory.CreateDbContextAsync(httpRequest.HttpContext.RequestAborted);
        
        // Load provider credentials using the correct context and entity
        var providerCreds = await configDbContext.ProviderCredentials
                                    .AsNoTracking() // Read-only operation
                                    .FirstOrDefaultAsync(p => p.ProviderName.ToLower() == providerName.ToLower(), // Use correct property
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

app.MapGet("/v1/models", async (
    [FromServices] ILogger<Program> logger,
    [FromServices] IVirtualKeyService virtualKeyService,
    [FromServices] IOptions<ConduitSettings> settingsOptions,
    HttpRequest httpRequest) =>
{
    logger.LogInformation("Received /v1/models request");

    // 1. Extract API Key from header
    string? originalApiKey = null;

    if (httpRequest.Headers.TryGetValue("Authorization", out var authHeader) && authHeader.Count > 0)
    {
        string auth = authHeader.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            originalApiKey = auth.Substring("Bearer ".Length).Trim();
        }
        else
        {
            originalApiKey = auth.Trim(); // Just in case they sent the raw key
        }
    }

    if (string.IsNullOrEmpty(originalApiKey))
    {
        logger.LogWarning("No API key provided for model listing");
        return Results.Json(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = "No API key provided",
                Type = "invalid_request_error",
                Code = "missing_api_key"
            }
        }, statusCode: (int)HttpStatusCode.Unauthorized, options: jsonSerializerOptions);
    }

    try
    {
        var settings = settingsOptions.Value;
        List<ModelProviderMapping> allowedModels = new List<ModelProviderMapping>();

        // --- Virtual Key Check & Validation ---
        bool useVirtualKey = originalApiKey?.StartsWith("condt_", StringComparison.OrdinalIgnoreCase) ?? false;
        if (useVirtualKey)
        {
            logger.LogInformation("Virtual Key detected. Validating and filtering models...");
            var virtualKey = await virtualKeyService.ValidateVirtualKeyAsync(originalApiKey!);
            if (virtualKey == null)
            {
                logger.LogWarning("Invalid or disabled Virtual Key provided");
                return Results.Json(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Invalid API key provided",
                        Type = "invalid_request_error",
                        Code = "invalid_api_key"
                    }
                }, statusCode: (int)HttpStatusCode.Unauthorized, options: jsonSerializerOptions);
            }

            // Get list of allowed models for this virtual key
            if (!string.IsNullOrEmpty(virtualKey.AllowedModels))
            {
                // Split comma-separated list and trim each value
                var permittedModelAliases = virtualKey.AllowedModels
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Filter the global model mappings to include only permitted models
                if (settings.ModelMappings != null)
                {
                    allowedModels = settings.ModelMappings
                        .Where(m => permittedModelAliases.Contains(m.ModelAlias))
                        .ToList();
                }
                
                logger.LogInformation("Filtered to {Count} models allowed for this virtual key", allowedModels.Count);
            }
            else
            {
                // If AllowedModels is empty, no models are allowed
                logger.LogInformation("Virtual key has no allowed models specified");
                allowedModels = new List<ModelProviderMapping>();
            }
        }
        else
        {
            // Direct provider key - return all configured models
            logger.LogInformation("Using direct provider key, returning all configured models");
            if (settings.ModelMappings != null)
            {
                allowedModels = settings.ModelMappings.ToList();
            }
        }

        // Format response according to OpenAI API specification
        var response = new
        {
            Object = "list",
            Data = allowedModels.Select(model => new
            {
                Id = model.ModelAlias,
                Object = "model",
                Created = 1677610602, // Fixed timestamp placeholder
                OwnedBy = "conduitllm" // Placeholder owner
            }).ToArray()
        };

        return Results.Json(response, options: jsonSerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing model listing request");
        return MapExceptionToHttpResult(ex, logger);
    }
}).WithTags("LLM Proxy");

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

// Add a health check endpoint
app.MapGet("/health", () => {
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
});

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
                _logger.LogInformation("Found {Count} provider credentials in database", providerCredsList.Count); // Correct LogInformation call
                
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings from database");
        }
    }
}
