namespace ConduitLLM.Functions.Providers.Exa;

public partial class ExaClient
{
    /// <inheritdoc />
    public Task<Interfaces.FunctionAuthenticationResult> VerifyAuthenticationAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default) =>
        VerifyViaProbeAsync(
            "/search",
            new
            {
                query = "test",
                numResults = 1,
                type = "keyword"
            },
            apiKey,
            cancellationToken);
}
