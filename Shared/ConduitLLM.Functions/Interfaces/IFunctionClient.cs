using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Base interface for all function provider clients.
/// </summary>
/// <remarks>
/// This interface is analogous to ILLMClient for provider integrations.
/// Each function provider (Exa, Perplexity, Tavily, custom RAG) implements this interface.
/// </remarks>
public interface IFunctionClient
{
    /// <summary>
    /// Verifies authentication with the function provider.
    /// </summary>
    /// <param name="apiKey">Optional API key override for testing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result with success status and diagnostic information.</returns>
    /// <remarks>
    /// Used to test credentials before activating a function configuration.
    /// Should make a lightweight API call to verify the key is valid.
    /// </remarks>
    Task<FunctionAuthenticationResult> VerifyAuthenticationAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the function with the provided parameters.
    /// </summary>
    /// <param name="parameters">Provider-specific parameters for the function execution.</param>
    /// <param name="apiKey">Optional API key override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Function execution result with response data and metadata.</returns>
    /// <remarks>
    /// This is the main execution method that calls the external provider API.
    /// Parameters are provider-specific and validated by the concrete implementation.
    /// </remarks>
    Task<FunctionExecutionResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates usage metrics from the execution result.
    /// </summary>
    /// <param name="parameters">The original request parameters.</param>
    /// <param name="result">The execution result from the provider.</param>
    /// <returns>Usage data for cost calculation.</returns>
    /// <remarks>
    /// This method extracts billable dimensions from the provider's response:
    /// - For Exa: search type, result count, content extraction counts
    /// - For Perplexity: tokens consumed, citations count
    /// - For RAG: documents indexed, vector dimensions
    ///
    /// The usage data is then passed to FunctionCostCalculationService.
    /// </remarks>
    FunctionExecutionUsage CalculateUsageFromResponse(
        Dictionary<string, object> parameters,
        FunctionExecutionResult result);

    /// <summary>
    /// Gets the provider type for this client.
    /// </summary>
    Enums.FunctionProviderType ProviderType { get; }

    /// <summary>
    /// Gets the provider name (for logging and diagnostics).
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Result of authentication verification with a function provider.
/// </summary>
public class FunctionAuthenticationResult
{
    /// <summary>
    /// Whether authentication was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Success or error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Response time in milliseconds (for diagnostics).
    /// </summary>
    public double ResponseTimeMs { get; set; }

    /// <summary>
    /// Additional diagnostic details.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    public static FunctionAuthenticationResult Success(string message, double responseTimeMs)
    {
        return new FunctionAuthenticationResult
        {
            IsSuccess = true,
            Message = message,
            ResponseTimeMs = responseTimeMs
        };
    }

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    public static FunctionAuthenticationResult Failure(string message, string? details = null)
    {
        return new FunctionAuthenticationResult
        {
            IsSuccess = false,
            Message = message,
            Details = details
        };
    }
}

/// <summary>
/// Result of a function execution.
/// </summary>
public class FunctionExecutionResult
{
    /// <summary>
    /// Whether the execution was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Response data from the provider (serialized as JSON).
    /// </summary>
    public string ResponseJson { get; set; } = string.Empty;

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// HTTP status code from the provider (if applicable).
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Provider-specific metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
