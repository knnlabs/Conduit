using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing model discovery and authentication verification.
    /// </summary>
    /// <remarks>
    /// Model discovery targets the control-plane <c>GET /foundation-models</c> endpoint on the
    /// <c>bedrock.&lt;region&gt;</c> host (signing service <c>bedrock</c>, not
    /// <c>bedrock-runtime</c>). This is also what backs the provider connection test: it is a real
    /// authenticated request, so an invalid key, missing model access, or a wrong region produce
    /// classifiable errors instead of an opaque failure.
    /// </remarks>
    public partial class BedrockClient
    {
        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateHttpClient(apiKey);
                using var httpRequest = BuildRequest(
                    HttpMethod.Get, BuildFoundationModelsUrl(), null, ControlPlaneService, apiKey);

                Logger.LogDebug("Listing Bedrock foundation models at {Endpoint}", httpRequest.RequestUri);

                using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
                await ThrowOnErrorAsync(httpResponse, "model discovery", cancellationToken);

                var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                var parsed = JsonSerializer.Deserialize<BedrockListFoundationModelsResponse>(body, DefaultJsonOptions);

                return parsed?.ModelSummaries?
                    .Where(summary => !string.IsNullOrWhiteSpace(summary.ModelId))
                    .Select(summary => ExtendedModelInfo
                        .Create(summary.ModelId!, ProviderName, summary.ModelId!)
                        .WithName(summary.ModelName ?? summary.ModelId!))
                    .ToList()
                    ?? new List<ExtendedModelInfo>();
            }, "GetModels", cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The base implementation issues a bare GET with a Bearer header, which cannot carry a
        /// SigV4 signature; verification is instead performed through the signed model-discovery
        /// request.
        /// </remarks>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var models = await GetModelsAsync(apiKey, cancellationToken);
                return Core.Interfaces.AuthenticationResult.Success(
                    $"Connected successfully to Amazon Bedrock in {_region} ({models.Count} foundation models visible)",
                    (DateTime.UtcNow - startTime).TotalMilliseconds);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LLMCommunicationException ex)
            {
                Logger.LogWarning(ex, "Bedrock authentication verification failed");
                return Core.Interfaces.AuthenticationResult.Failure("Authentication failed", ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Bedrock authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}", ex.ToString());
            }
        }
    }
}
