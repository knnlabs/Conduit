using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using ConduitLLM.WebUI.Data;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.WebUI.Controllers;

[ApiController]
[Route("api/v1")]
public class LlmApiController : ControllerBase
{
    private readonly ILogger<LlmApiController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDbContextFactory<ConfigurationDbContext> _contextFactory;

    public LlmApiController(
        ILogger<LlmApiController> logger,
        IHttpClientFactory httpClientFactory,
        IDbContextFactory<ConfigurationDbContext> contextFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _contextFactory = contextFactory;
    }

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions()
    {
        // This endpoint is protected by our Virtual Key middleware
        return await ProxyLlmRequest();
    }

    [HttpPost("completions")]
    public async Task<IActionResult> Completions()
    {
        // This endpoint is protected by our Virtual Key middleware
        return await ProxyLlmRequest();
    }

    [HttpPost("embeddings")]
    public async Task<IActionResult> Embeddings()
    {
        // This endpoint is protected by our Virtual Key middleware
        return await ProxyLlmRequest();
    }

    [HttpGet("models")]
    public async Task<IActionResult> Models()
    {
        // This endpoint is protected by our Virtual Key middleware
        return await ProxyLlmRequest();
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        // Health check endpoint (no auth required)
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }

    private async Task<IActionResult> ProxyLlmRequest()
    {
        try
        {
            // Check if Virtual Key ID is present (set by middleware)
            if (!HttpContext.Items.TryGetValue("ValidatedVirtualKeyId", out var _))
            {
                // This should not happen if the middleware is working correctly
                _logger.LogWarning("Request reached LLM API controller without valid virtual key");
                return Unauthorized(new { error = "Authentication required" });
            }

            // Get the request path and method
            string path = HttpContext.Request.Path.Value!;
            string method = HttpContext.Request.Method;
            
            // Read the request body
            string requestBody = await ReadRequestBodyAsync();
            
            // Extract the target provider and model from the request
            if (!TryGetTargetProvider(requestBody, out string? targetProvider, out string? targetModel))
            {
                return BadRequest(new { error = "Invalid request: missing or invalid model" });
            }
            
            // Make sure targetProvider is not null before proceeding
            if (string.IsNullOrEmpty(targetProvider))
            {
                return BadRequest(new { error = "Unable to determine provider from model" });
            }
            
            // Get the appropriate API key and endpoint for the provider
            var (success, apiKey, apiBase) = await GetProviderCredentialsAsync(targetProvider);
            if (!success || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiBase))
            {
                return BadRequest(new { error = $"Provider '{targetProvider}' not configured" });
            }
            
            // Create request to the actual provider
            var httpClient = _httpClientFactory.CreateClient();
            var providerRequest = new HttpRequestMessage
            {
                Method = new HttpMethod(method),
                RequestUri = new Uri($"{apiBase}{path.Replace("/api/v1", "")}")
            };
            
            // Add the provider's API key
            providerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            // Add any content from the original request
            if (!string.IsNullOrEmpty(requestBody))
            {
                providerRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            }
            
            // Forward the request to the provider
            var response = await httpClient.SendAsync(providerRequest);
            
            // Read the provider's response
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Return the provider's response with the same status code
            return new ContentResult
            {
                Content = responseContent,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying LLM request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task<string> ReadRequestBodyAsync()
    {
        // Enable buffering to allow reading the body multiple times
        Request.EnableBuffering();
        
        // Reset the body position
        Request.Body.Position = 0;
        
        // Read the body
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        
        // Reset the body position for other middleware
        Request.Body.Position = 0;
        
        return body;
    }

    private bool TryGetTargetProvider(string requestBody, out string? provider, out string? model)
    {
        provider = null;
        model = null;
        
        try
        {
            if (string.IsNullOrEmpty(requestBody))
                return false;
            
            var document = JsonDocument.Parse(requestBody);
            if (!document.RootElement.TryGetProperty("model", out var modelElement))
                return false;
            
            model = modelElement.GetString();
            if (string.IsNullOrEmpty(model))
                return false;

            // Extract provider from model name
            // This is a simplified approach, in a real implementation you would use your model mapping service
            if (model.StartsWith("gpt-"))
                provider = "openai";
            else if (model.StartsWith("claude-"))
                provider = "anthropic";
            else if (model.Contains("gemini-"))
                provider = "gemini";
            else if (model.StartsWith("command-") || model.Contains("embed-"))
                provider = "cohere";
            else if (model.Contains("llama") || model.Contains("mixtral"))
                provider = "fireworks";
            else
                provider = "openai"; // Default fallback

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing request body to extract model");
            return false;
        }
    }

    private async Task<(bool Success, string? ApiKey, string? ApiBase)> GetProviderCredentialsAsync(string provider)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var credentials = await context.ProviderCredentials
                .FirstOrDefaultAsync(pc => pc.ProviderName == provider);

            if (credentials == null)
                return (false, null, null);

            return (
                !string.IsNullOrEmpty(credentials.ApiKey) && !string.IsNullOrEmpty(credentials.ApiBase),
                credentials.ApiKey,
                credentials.ApiBase
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provider credentials");
            return (false, null, null);
        }
    }
}
