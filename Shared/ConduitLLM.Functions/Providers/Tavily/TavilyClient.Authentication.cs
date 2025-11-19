using System.Diagnostics;
using System.Net.Http.Json;

namespace ConduitLLM.Functions.Providers.Tavily;

/// <summary>
/// Authentication operations for TavilyClient.
/// </summary>
public partial class TavilyClient
{
    /// <inheritdoc />
    public async Task<Interfaces.FunctionAuthenticationResult> VerifyAuthenticationAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveApiKey = apiKey ?? _credential.ApiKey;

        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            return Interfaces.FunctionAuthenticationResult.Failure(
                "API key is required for Tavily",
                "No API key provided in credential or parameter");
        }

        try
        {
            _logger.LogInformation("Verifying Tavily authentication...");

            using var client = CreateHttpClient(apiKey);

            // Make a lightweight search request to verify authentication
            // Use basic search with minimal results to reduce cost
            var testRequest = new
            {
                query = "test",
                max_results = 1,
                search_depth = "basic"
            };

            var response = await client.PostAsJsonAsync("/search", testRequest, _jsonOptions, cancellationToken);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Tavily authentication successful (Response: {StatusCode}, Time: {ElapsedMs}ms)",
                    response.StatusCode, stopwatch.ElapsedMilliseconds);

                return Interfaces.FunctionAuthenticationResult.Success(
                    $"Successfully authenticated with Tavily API",
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            // Handle specific error codes
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Tavily authentication failed: Invalid API key");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Authentication failed",
                    "Invalid API key for Tavily");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                _logger.LogWarning("Tavily authentication failed: Bad request (likely invalid API key format)");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Authentication failed",
                    "Invalid API key format for Tavily. Please verify your API key starts with 'tvly-' and is correct.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Tavily authentication failed: Access forbidden");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Access forbidden",
                    "API key does not have sufficient permissions for Tavily");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Tavily authentication failed: Rate limit exceeded");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Rate limit exceeded",
                    "Too many requests. Development keys: 100 RPM, Production keys: 1000 RPM");
            }

            if (response.StatusCode == (System.Net.HttpStatusCode)432)
            {
                _logger.LogWarning("Tavily authentication failed: Plan usage limit exceeded");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Plan usage limit exceeded",
                    "Monthly API credit limit reached for your Tavily plan");
            }

            if (response.StatusCode == (System.Net.HttpStatusCode)433)
            {
                _logger.LogWarning("Tavily authentication failed: Pay-as-you-go limit exceeded");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Pay-as-you-go limit exceeded",
                    "PAYGO spending limit reached for your Tavily account");
            }

            _logger.LogWarning("Tavily authentication failed with unexpected status: {StatusCode}", response.StatusCode);
            return Interfaces.FunctionAuthenticationResult.Failure(
                $"Unexpected response: {response.StatusCode}",
                $"Tavily API returned status {(int)response.StatusCode}. Response: {errorBody}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Tavily authentication timed out after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return Interfaces.FunctionAuthenticationResult.Failure(
                "Request timeout",
                "Authentication request timed out. Please try again or check your network connection.");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Network error verifying Tavily authentication: {Error}", ex.Message);

            return Interfaces.FunctionAuthenticationResult.Failure(
                $"Network error: {ex.Message}",
                "Unable to connect to Tavily API. Please check your network connection and try again.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Tavily authentication failed with exception: {Error}", ex.Message);

            return Interfaces.FunctionAuthenticationResult.Failure(
                $"Authentication verification failed: {ex.Message}",
                ex.ToString());
        }
    }
}
