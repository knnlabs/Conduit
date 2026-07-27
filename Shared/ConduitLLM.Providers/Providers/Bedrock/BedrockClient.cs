using System.Net.Http.Headers;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Authentication;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.Serialization;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Bedrock
{
    /// <summary>
    /// Client for Amazon Bedrock via the model-agnostic Converse API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bedrock is the first consumer of the multi-secret credential mechanism from epic #1177:
    /// the key credential's <c>ApiKey</c> holds either an IAM access key ID (paired with the
    /// <c>secret_access_key</c> / optional <c>session_token</c> secret settings, requests signed
    /// with SigV4) or a Bedrock API key (sent as a Bearer token when no secret access key is
    /// configured). The AWS region is a provider-level structured setting that both builds the
    /// endpoint host and scopes the signature.
    /// </para>
    /// <para>
    /// Signing covers the exact request bytes, so this client builds and authenticates each
    /// <see cref="HttpRequestMessage"/> itself rather than relying on client-level default headers.
    /// </para>
    /// </remarks>
    public partial class BedrockClient : BaseLLMClient
    {
        internal const string RuntimeService = "bedrock-runtime";
        internal const string ControlPlaneService = "bedrock";
        private const string SecretAccessKeySetting = "secret_access_key";
        private const string SessionTokenSetting = "session_token";

        private readonly string _region;
        private readonly string _runtimeBaseUrl;
        private readonly string _controlPlaneBaseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="BedrockClient"/> class.
        /// </summary>
        /// <param name="provider">The provider entity carrying the region setting and optional base URL override.</param>
        /// <param name="keyCredential">The key credential; secret settings must already be revealed to plaintext.</param>
        /// <param name="providerModelId">The Bedrock model ID or inference profile ID.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public BedrockClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<BedrockClient> logger,
            IHttpClientFactory httpClientFactory)
            : base(provider, keyCredential, providerModelId, logger, httpClientFactory, "bedrock")
        {
            _region = ProviderConfigurationRegistry.GetSettingValue(provider.ProviderType, provider.Settings, "region")
                ?? throw new ConfigurationException(
                    "Bedrock is missing required configuration: AWS Region. Provide the value in the provider settings.");

            _runtimeBaseUrl = ProviderConfigurationRegistry.ResolveBaseUrl(provider);

            // The control plane (model listing) lives on the sibling bedrock.<region> host. When an
            // operator overrides the base URL (a proxy or PrivateLink endpoint), the runtime host is
            // rewritten when it carries the bedrock-runtime marker; otherwise the override is used
            // as-is and is expected to front both APIs.
            _controlPlaneBaseUrl = _runtimeBaseUrl.Contains(RuntimeService, StringComparison.OrdinalIgnoreCase)
                ? _runtimeBaseUrl.Replace(RuntimeService, ControlPlaneService, StringComparison.OrdinalIgnoreCase)
                : _runtimeBaseUrl;
        }

        /// <summary>
        /// Validates that the credential expresses one of the two supported authentication shapes.
        /// </summary>
        protected override void ValidateCredentials()
        {
            base.ValidateCredentials();

            // An IAM access key ID without its secret can never authenticate; failing here names the
            // missing field instead of letting AWS return an opaque 403 at request time.
            if (!UsesSigV4 && LooksLikeAccessKeyId(PrimaryKeyCredential.ApiKey))
            {
                throw new ConfigurationException(
                    "Bedrock is missing required configuration: Secret Access Key. The API key looks like an "
                    + "IAM access key ID, which must be paired with its secret access key on the key credential. "
                    + "Alternatively, use a Bedrock API key as the API key and leave the secret settings empty.");
            }
        }

        private static bool LooksLikeAccessKeyId(string? apiKey) =>
            apiKey != null && (apiKey.StartsWith("AKIA", StringComparison.Ordinal)
                || apiKey.StartsWith("ASIA", StringComparison.Ordinal));

        /// <summary>Whether requests are signed with SigV4 (a secret access key is configured).</summary>
        private bool UsesSigV4 => !string.IsNullOrWhiteSpace(GetSecretSetting(SecretAccessKeySetting));

        private string? GetSecretSetting(string key) =>
            PrimaryKeyCredential.SecretSettings is { } secrets && secrets.TryGetValue(key, out var value)
                && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;

        /// <summary>
        /// Builds and authenticates a Bedrock request. SigV4 signs the exact payload bytes; Bearer
        /// mode attaches the Bedrock API key. The optional per-call API key override replaces the
        /// access key ID (SigV4) or the API key (Bearer) while keeping the configured secrets.
        /// </summary>
        internal HttpRequestMessage BuildRequest(
            HttpMethod method,
            string url,
            byte[]? payload,
            string service,
            string? apiKeyOverride = null)
        {
            var request = new HttpRequestMessage(method, url);
            if (payload != null)
            {
                var content = new ByteArrayContent(payload);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content = content;
            }

            var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKeyOverride)
                ? apiKeyOverride
                : PrimaryKeyCredential.ApiKey!;

            if (UsesSigV4)
            {
                AwsSigV4Signer.Sign(
                    request,
                    payload ?? Array.Empty<byte>(),
                    new AwsSigV4Credentials(
                        effectiveApiKey,
                        GetSecretSetting(SecretAccessKeySetting)!,
                        GetSecretSetting(SessionTokenSetting)),
                    _region,
                    service,
                    DateTimeOffset.UtcNow);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey);
            }

            return request;
        }

        /// <summary>
        /// Serializes a Bedrock request body with the client's camelCase conventions.
        /// </summary>
        internal static byte[] SerializePayload(BedrockConverseRequest body) =>
            JsonSerializer.SerializeToUtf8Bytes(
                body,
                ProvidersJsonContext.Default.BedrockConverseRequest);

        /// <summary>
        /// Authentication is applied per request (SigV4 covers the payload), so no client-level
        /// authentication header is configured.
        /// </summary>
        protected override void ConfigureAuthentication(HttpClient client, string apiKey)
        {
            // Intentionally empty: BuildRequest attaches the Authorization header per request.
        }

        /// <summary>
        /// Raises a communication error carrying Bedrock's error message and status code so the
        /// shared classifier can produce an actionable connection-test result.
        /// </summary>
        private async Task ThrowOnErrorAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = ExtractErrorFromJson(body, $"{(int)response.StatusCode} ({response.StatusCode})");
            throw new LLMCommunicationException(
                $"Bedrock {operation} failed: {message} [HTTP {(int)response.StatusCode}]",
                response.StatusCode,
                body);
        }

        /// <summary>The runtime endpoint for a model-scoped action such as <c>converse</c>.</summary>
        internal string BuildModelUrl(string modelId, string action) =>
            $"{_runtimeBaseUrl}/model/{Uri.EscapeDataString(modelId)}/{action}";

        /// <summary>The control-plane endpoint for foundation-model discovery.</summary>
        internal string BuildFoundationModelsUrl() => $"{_controlPlaneBaseUrl}/foundation-models";

        /// <inheritdoc />
        public override string GetHealthCheckUrl(string? baseUrl = null) =>
            !string.IsNullOrWhiteSpace(baseUrl)
                ? $"{baseUrl.TrimEnd('/')}/foundation-models"
                : BuildFoundationModelsUrl();

        /// <inheritdoc />
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "The Bedrock adapter does not support embeddings; Bedrock embedding models use "
                + "model-specific InvokeModel payloads rather than the Converse API.");
        }

        /// <inheritdoc />
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "The Bedrock adapter does not support image generation; Bedrock image models use "
                + "model-specific InvokeModel payloads rather than the Converse API.");
        }
    }
}
