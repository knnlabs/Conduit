using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonNamingPolicy

using ConduitLLM.Configuration;
using ConduitLLM.Core;
using ConduitLLM.Core.Exceptions; // Add namespace for custom exceptions
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers; // Assuming LLMClientFactory is here
using ConduitLLM.WebUI.Data; // Added for DbContext and models
using ConduitLLM.WebUI.Interfaces; // Added for IVirtualKeyService
using ConduitLLM.WebUI.Services;  // Added for VirtualKeyService

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Added for EF Core
using Microsoft.Extensions.Options; // Added for IOptions
using Microsoft.AspNetCore.RateLimiting;
using ConduitLLM.Http.Security;

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
    builder.Services.AddDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext>(options =>
        options.UseSqlite(dbConnectionString)); // Existing registration for WebUI context
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
        options.UseSqlite(dbConnectionString)); // Add this for ConfigurationDbContext
}
else if (dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext>(options =>
        options.UseNpgsql(dbConnectionString)); // Existing registration for WebUI context
    builder.Services.AddDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>(options =>
        options.UseNpgsql(dbConnectionString)); // Add this for ConfigurationDbContext
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
builder.Services.AddAuthorization();

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
            // Pass the actual provider apiKey (which might be null if virtual key was used)
            await foreach (var chunk in conduit.StreamChatCompletionAsync(request, apiKey, httpRequest.HttpContext.RequestAborted)
                            .WithCancellation(httpRequest.HttpContext.RequestAborted)) 
            {
                if (httpRequest.HttpContext.RequestAborted.IsCancellationRequested)
                {
                    logger.LogInformation("Stream was cancelled.");
                    break;
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

            // For streaming requests, we'll have to use the approximate usage data collected at the client side
            // or use a placeholder cost until we have a better solution
            if (virtualKeyId.HasValue)
            {
                // TODO: Implement proper usage tracking for streaming requests
                // Could accumulate tokens in client or estimate based on response length
                // For now, use a minimal cost placeholder
                decimal cost = 0.01m; // Placeholder cost - streaming calculation will be improved later
                logger.LogInformation("Updating spend for Virtual Key ID {KeyId} by {Cost} after stream completion", virtualKeyId.Value, cost);
                await virtualKeyService.UpdateSpendAsync(virtualKeyId.Value, cost);
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
                ImageCount = request.N ?? 1
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
    [FromServices] IDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext> dbContextFactory) =>
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
    [FromServices] IDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext> dbContextFactory, // Inject DbContextFactory
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
        
        // Load provider credentials
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
    private readonly IDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext> _dbContextFactory;
    private readonly IOptions<ConduitSettings> _settingsOptions;
    private readonly ILogger<DatabaseSettingsStartupFilter> _logger;

    public DatabaseSettingsStartupFilter(
        IDbContextFactory<ConduitLLM.WebUI.Data.ConfigurationDbContext> dbContextFactory,
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
            // Load provider credentials
            var providerCredsList = await _dbContextFactory.CreateDbContextAsync();
            var providerCredsList2 = await providerCredsList.ProviderCredentials.ToListAsync();
            if (providerCredsList2.Any())
            {
                _logger.LogInformation("Found {Count} provider credentials in database", providerCredsList2.Count);
                
                // Convert database provider credentials to Core provider credentials
                var providersList = providerCredsList2.Select(p => new ProviderCredentials
                {
                    ProviderName = p.ProviderName,
                    ApiKey = p.ApiKey,
                    ApiVersion = p.ApiVersion,
                    ApiBase = p.ApiBase
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

            // Load model mappings from database
            var modelMappings = await providerCredsList.ModelMappings.ToListAsync();
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
