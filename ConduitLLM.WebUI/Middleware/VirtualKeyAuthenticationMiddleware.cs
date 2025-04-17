using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Middleware;

/// <summary>
/// Middleware to handle virtual key authentication for API requests
/// </summary>
public class VirtualKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VirtualKeyAuthenticationMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Headers that might contain the API key
    private const string AuthorizationHeader = "Authorization";
    private const string ApiKeyHeader = "X-Api-Key";
    private const string BearerPrefix = "Bearer ";
    
    // Special path prefix that requires virtual key auth
    private const string ApiPrefix = "/api/v1/";

    public VirtualKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<VirtualKeyAuthenticationMiddleware> logger,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IVirtualKeyService virtualKeyService)
    {
        // Only apply to paths under the API prefix
        if (!context.Request.Path.StartsWithSegments(ApiPrefix))
        {
            // Not an API request, pass through
            await _next(context);
            return;
        }

        // Skip auth for certain endpoints like health checks if needed
        if (IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Extract virtual key from headers
        var virtualKey = ExtractApiKey(context.Request);
        
        if (string.IsNullOrEmpty(virtualKey))
        {
            _logger.LogWarning("API request missing authentication");
            
            using (var scope = _serviceProvider.CreateScope())
            {
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                notificationService.AddNotification(
                    Models.NotificationType.VirtualKeyValidation,
                    "API request missing authentication",
                    context.Request.Path,
                    $"Client IP: {context.Connection.RemoteIpAddress}"
                );
            }
            
            await RespondWithError(context, 401, "Missing API key");
            return;
        }

        try
        {
            // Extract the requested model from the request body if it's a POST request
            string? requestedModel = await ExtractRequestedModelAsync(context.Request);
            
            // Validate the key with requested model
            var validatedKey = await virtualKeyService.ValidateVirtualKeyAsync(virtualKey, requestedModel);
            
            if (validatedKey == null)
            {
                _logger.LogWarning("Invalid virtual key used in request");
                
                // Create a notification about the failed key validation
                string prefix = virtualKey.StartsWith(VirtualKeyConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase)
                    ? virtualKey.Substring(0, Math.Min(virtualKey.Length, VirtualKeyConstants.KeyPrefix.Length + 4)) + "..."
                    : "invalid-format";
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                    notificationService.AddKeyValidationFailure(
                        prefix,
                        "Key validation failed",
                        requestedModel
                    );
                }
                
                await RespondWithError(context, 401, "Invalid API key");
                return;
            }

            // Check expiry
            if (validatedKey.ExpiresAt.HasValue && validatedKey.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired virtual key used in request");
                await RespondWithError(context, 401, "API key expired");
                return;
            }

            // Check if key is enabled
            if (!validatedKey.IsEnabled)
            {
                _logger.LogWarning("Disabled virtual key used in request");
                await RespondWithError(context, 403, "API key disabled");
                return;
            }
            
            // Check budget
            if (validatedKey.MaxBudget.HasValue && validatedKey.CurrentSpend >= validatedKey.MaxBudget.Value)
            {
                _logger.LogWarning("Virtual key with exceeded budget used in request");
                await RespondWithError(context, 403, "API key budget exceeded");
                return;
            }

            // Store the validated key info for use in the request pipeline
            context.Items["ValidatedVirtualKeyId"] = validatedKey.Id;
            context.Items["ValidatedVirtualKeyAllowedModels"] = validatedKey.AllowedModels;
            context.Items["ValidatedVirtualKeyName"] = validatedKey.KeyName;

            // Continue processing the request
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating virtual key");
            
            using (var scope = _serviceProvider.CreateScope())
            {
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                notificationService.AddNotification(
                    Models.NotificationType.Error,
                    "Error during API key validation",
                    context.Request.Path,
                    ex.Message
                );
            }
            
            await RespondWithError(context, 500, "Error processing request");
        }
    }

    private bool IsExcludedPath(PathString path)
    {
        // Example: exclude health check endpoint
        return path.StartsWithSegments("/api/v1/health");
    }

    private string? ExtractApiKey(HttpRequest request)
    {
        // Try X-API-Key header first
        if (request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValue) && 
            !string.IsNullOrEmpty(apiKeyValue))
        {
            return apiKeyValue.ToString();
        }

        // Then try Authorization header with Bearer prefix
        if (request.Headers.TryGetValue(AuthorizationHeader, out var authValue) && 
            !string.IsNullOrEmpty(authValue) &&
            authValue.ToString().StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authValue.ToString()[BearerPrefix.Length..].Trim();
        }

        // Finally try query string
        if (request.Query.TryGetValue("api_key", out var queryApiKey) || 
            request.Query.TryGetValue("key", out queryApiKey))
        {
            return queryApiKey;
        }

        return null;
    }

    private async Task<string?> ExtractRequestedModelAsync(HttpRequest request)
    {
        if (request.Method != "POST" || 
            !request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return null;
        }

        try
        {
            request.EnableBuffering();
            request.Body.Position = 0;
            
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            
            // Reset the body position for other middleware
            request.Body.Position = 0;
            
            if (string.IsNullOrEmpty(body))
                return null;
            
            var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("model", out var modelElement))
            {
                return modelElement.GetString();
            }
            
            return null;
        }
        catch (Exception ex)
        {
            // Just log and return null - don't fail the request due to model extraction issues
            _logger.LogWarning(ex, "Error extracting model from request body");
            return null;
        }
    }

    private async Task RespondWithError(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var error = new
        {
            error = new
            {
                message = message,
                type = "auth_error",
                code = statusCode
            }
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, error);
    }
}

// Extension method for easy registration in Program.cs
public static class VirtualKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseVirtualKeyAuthentication(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<VirtualKeyAuthenticationMiddleware>();
    }
}
