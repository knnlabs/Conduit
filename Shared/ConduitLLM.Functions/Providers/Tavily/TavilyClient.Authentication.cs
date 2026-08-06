using System.Net;

using ConduitLLM.Functions.Interfaces;

namespace ConduitLLM.Functions.Providers.Tavily;

public partial class TavilyClient
{
    /// <inheritdoc />
    public Task<FunctionAuthenticationResult> VerifyAuthenticationAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default) =>
        VerifyViaProbeAsync(
            "/search",
            new
            {
                query = "test",
                max_results = 1,
                search_depth = "basic"
            },
            apiKey,
            cancellationToken);

    protected override FunctionAuthenticationResult? TranslateAuthenticationFailure(
        HttpStatusCode statusCode,
        string responseBody) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest => FunctionAuthenticationResult.Failure(
                "Authentication failed",
                "Invalid API key format for Tavily. Verify the API key starts with 'tvly-' and is correct."),
            HttpStatusCode.TooManyRequests => FunctionAuthenticationResult.Failure(
                "Rate limit exceeded",
                "Too many requests. Development keys: 100 RPM, Production keys: 1000 RPM"),
            (HttpStatusCode)432 => FunctionAuthenticationResult.Failure(
                "Plan usage limit exceeded",
                "Monthly API credit limit reached for your Tavily plan"),
            (HttpStatusCode)433 => FunctionAuthenticationResult.Failure(
                "Pay-as-you-go limit exceeded",
                "PAYGO spending limit reached for your Tavily account"),
            _ => null
        };
}
