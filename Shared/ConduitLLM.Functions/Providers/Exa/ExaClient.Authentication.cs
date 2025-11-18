using System.Diagnostics;
using System.Net.Http.Json;

namespace ConduitLLM.Functions.Providers.Exa;

/// <summary>
/// Authentication operations for ExaClient.
/// </summary>
public partial class ExaClient
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
                "API key is required for Exa",
                "No API key provided in credential or parameter");
        }

        try
        {
            _logger.LogInformation("Verifying Exa authentication...");

            using var client = CreateHttpClient(apiKey);

            // Make a lightweight search request to verify authentication
            // Use a simple query with minimal results to reduce cost
            var testRequest = new
            {
                query = "test",
                numResults = 1,
                type = "keyword" // Use keyword to minimize cost
            };

            var response = await client.PostAsJsonAsync("/search", testRequest, _jsonOptions, cancellationToken);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Exa authentication successful (Response: {StatusCode}, Time: {ElapsedMs}ms)",
                    response.StatusCode, stopwatch.ElapsedMilliseconds);

                return Interfaces.FunctionAuthenticationResult.Success(
                    $"Successfully authenticated with Exa API",
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            // Handle specific error codes
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Exa authentication failed: Invalid API key");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Authentication failed",
                    "Invalid API key for Exa");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                _logger.LogWarning("Exa authentication failed: Bad request (likely invalid API key or account issue)");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Authentication failed",
                    "Invalid API key or account configuration for Exa. Please verify your API key is correct and your account is active.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Exa authentication failed: Access forbidden");
                return Interfaces.FunctionAuthenticationResult.Failure(
                    "Access forbidden",
                    "API key does not have sufficient permissions for Exa");
            }

            _logger.LogWarning("Exa authentication failed with unexpected status: {StatusCode}", response.StatusCode);
            return Interfaces.FunctionAuthenticationResult.Failure(
                $"Unexpected response: {response.StatusCode}",
                $"Exa API returned status {(int)response.StatusCode}. Response: {errorBody}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Exa authentication timed out after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return Interfaces.FunctionAuthenticationResult.Failure(
                "Request timeout",
                "Authentication request timed out. Please try again or check your network connection.");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Network error verifying Exa authentication: {Error}", ex.Message);

            return Interfaces.FunctionAuthenticationResult.Failure(
                $"Network error: {ex.Message}",
                "Unable to connect to Exa API. Please check your network connection and try again.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Exa authentication failed with exception: {Error}", ex.Message);

            return Interfaces.FunctionAuthenticationResult.Failure(
                $"Authentication verification failed: {ex.Message}",
                ex.ToString());
        }
    }
}
