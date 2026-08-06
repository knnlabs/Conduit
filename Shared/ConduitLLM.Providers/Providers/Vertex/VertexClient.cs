using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Authentication;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.OpenAICompatible;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Vertex;

/// <summary>
/// Google Vertex AI client using its OpenAI-compatible Chat Completions endpoint.
/// </summary>
/// <remarks>
/// Authentication is performed with a short-lived Google OAuth access token minted from the
/// service-account JSON stored on the selected key credential. Chat request/response mapping and
/// SSE streaming are inherited from <see cref="OpenAICompatibleClient"/>.
/// </remarks>
public sealed class VertexClient : OpenAICompatibleClient
{
    private const string ServiceAccountJsonSetting = "service_account_json";
    private const string GoogleApiBaseUrl = "https://aiplatform.googleapis.com";

    private readonly string _projectId;
    private readonly string _location;
    private readonly GoogleServiceAccountTokenProvider _tokenProvider;

    protected override IAuthenticationStrategy AuthenticationStrategy =>
        OAuthAccessTokenStrategy.Instance;

    public VertexClient(
        Provider provider,
        ProviderKeyCredential keyCredential,
        string providerModelId,
        ILogger<VertexClient> logger,
        IHttpClientFactory httpClientFactory)
        : base(
            provider,
            keyCredential,
            providerModelId,
            logger,
            httpClientFactory,
            "Vertex",
            ProviderConfigurationRegistry.ResolveBaseUrl(provider))
    {
        _projectId = ProviderConfigurationRegistry.GetSettingValue(
                provider.ProviderType,
                provider.Settings,
                "project_id")
            ?? throw new ConfigurationException(
                "Vertex is missing required configuration: Google Cloud Project ID.");
        _location = ProviderConfigurationRegistry.GetSettingValue(
                provider.ProviderType,
                provider.Settings,
                "location")
            ?? throw new ConfigurationException(
                "Vertex is missing required configuration: Vertex AI Location.");

        _tokenProvider = new GoogleServiceAccountTokenProvider(
            httpClientFactory,
            keyCredential.Id,
            GetServiceAccountJson());
    }

    protected override void ValidateCredentials()
    {
        if (string.IsNullOrWhiteSpace(GetSecretSetting(ServiceAccountJsonSetting)))
        {
            throw new ConfigurationException(
                "Vertex is missing required configuration: Service Account JSON.");
        }
    }

    public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await ResolveAccessTokenAsync(apiKey, cancellationToken);
        return await base.CreateChatCompletionAsync(request, accessToken, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var accessToken = await ResolveAccessTokenAsync(apiKey, cancellationToken);
        await foreach (var chunk in base.StreamChatCompletionAsync(
            request,
            accessToken,
            cancellationToken).WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Validates the configured project/location and then retrieves Google's Model Garden catalog.
    /// The project-scoped validation call is intentional: the public publisher catalog alone cannot
    /// distinguish a valid credential from a mistyped project ID.
    /// </summary>
    public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteApiRequestAsync(async () =>
        {
            var accessToken = await ResolveAccessTokenAsync(apiKey, cancellationToken);
            using var client = CreateHttpClient(accessToken);

            var projectModelsEndpoint =
                $"{GoogleApiBaseUrl}/v1/projects/{Uri.EscapeDataString(_projectId)}"
                + $"/locations/{Uri.EscapeDataString(_location)}/models?pageSize=1";
            using (var validationRequest = new HttpRequestMessage(HttpMethod.Get, projectModelsEndpoint))
            using (var validationResponse = await client.SendAsync(
                validationRequest,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken))
            {
                await ThrowOnVertexErrorAsync(
                    validationResponse,
                    "project/location validation",
                    cancellationToken);
            }

            var publisherModelsEndpoint =
                $"{GoogleApiBaseUrl}/v1beta1/publishers/google/models?pageSize=100";
            using var modelsRequest = new HttpRequestMessage(HttpMethod.Get, publisherModelsEndpoint);
            using var modelsResponse = await client.SendAsync(
                modelsRequest,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            var body = await modelsResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!modelsResponse.IsSuccessStatusCode)
            {
                throw CreateVertexHttpException(
                    modelsResponse,
                    body,
                    "publisher-model discovery");
            }

            return ParsePublisherModels(body);
        }, "GetModels", cancellationToken);
    }

    protected override object MapToOpenAIRequest(ChatCompletionRequest request)
    {
        var mapped = base.MapToOpenAIRequest(request);
        if (mapped is Dictionary<string, object?> values)
        {
            values["model"] = QualifyModelId(ProviderModelId);
        }

        return mapped;
    }

    protected override Exception? TranslateHttpError(
        HttpResponseMessage response,
        string responseContent) =>
        CreateVertexHttpException(response, responseContent, "request");

    public override string GetHealthCheckUrl(string? baseUrl = null) =>
        $"{GoogleApiBaseUrl}/v1/projects/{Uri.EscapeDataString(_projectId)}"
        + $"/locations/{Uri.EscapeDataString(_location)}/models?pageSize=1";

    public override Task<EmbeddingResponse> CreateEmbeddingAsync(
        EmbeddingRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "The Vertex OpenAI-compatible adapter currently supports chat completions only.");

    public override Task<ImageGenerationResponse> CreateImageAsync(
        ImageGenerationRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "The Vertex OpenAI-compatible adapter currently supports chat completions only.");

    private async Task<string> ResolveAccessTokenAsync(
        string? accessTokenOverride,
        CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(accessTokenOverride)
            ? accessTokenOverride
            : await _tokenProvider.GetAccessTokenAsync(cancellationToken);

    private string GetServiceAccountJson() =>
        GetSecretSetting(ServiceAccountJsonSetting)
        ?? throw new ConfigurationException(
            "Vertex is missing required configuration: Service Account JSON.");

    private string? GetSecretSetting(string key) =>
        PrimaryKeyCredential.SecretSettings is { } secrets
        && secrets.TryGetValue(key, out var value)
        && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static string QualifyModelId(string modelId) =>
        modelId.Contains('/', StringComparison.Ordinal)
            ? modelId
            : $"google/{modelId}";

    private async Task ThrowOnVertexErrorAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw CreateVertexHttpException(response, body, operation);
    }

    private static LLMCommunicationException CreateVertexHttpException(
        HttpResponseMessage response,
        string responseContent,
        string operation)
    {
        var providerMessage = ExtractGoogleError(responseContent)
            ?? $"{(int)response.StatusCode} ({response.StatusCode})";

        var guidance = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                " Verify the service-account key and grant the service account the Vertex AI User role.",
            HttpStatusCode.NotFound =>
                " Verify project_id, location, and the publisher-qualified model ID.",
            HttpStatusCode.BadRequest =>
                " Verify project_id, location, and the request parameters.",
            HttpStatusCode.TooManyRequests =>
                " Check the Vertex AI quota for the configured project and location.",
            _ => string.Empty
        };

        return new LLMCommunicationException(
            $"Vertex AI {operation} failed: {providerMessage} [HTTP {(int)response.StatusCode}].{guidance}",
            response.StatusCode,
            responseContent);
    }

    private static string? ExtractGoogleError(string responseContent)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return null;
            }

            if (error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }

            if (error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Preserve the HTTP status when Google or a proxy returned a non-JSON response.
        }

        return null;
    }

    private List<ExtendedModelInfo> ParsePublisherModels(string responseContent)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            if (!document.RootElement.TryGetProperty("publisherModels", out var models)
                || models.ValueKind != JsonValueKind.Array)
            {
                return new List<ExtendedModelInfo>();
            }

            var result = new List<ExtendedModelInfo>();
            foreach (var model in models.EnumerateArray())
            {
                var resourceName = GetString(model, "name");
                if (string.IsNullOrWhiteSpace(resourceName))
                {
                    continue;
                }

                var modelName = resourceName.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    continue;
                }

                var id = $"google/{modelName}";
                var displayName = GetString(model, "displayName") ?? id;
                result.Add(ExtendedModelInfo.Create(id, ProviderName, displayName));
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new LLMCommunicationException(
                "Vertex AI publisher-model discovery returned invalid JSON.",
                ex);
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
