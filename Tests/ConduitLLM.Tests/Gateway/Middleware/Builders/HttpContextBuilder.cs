using Microsoft.AspNetCore.Http;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.UsageTracking;

namespace ConduitLLM.Tests.Http.Middleware.Builders
{
    /// <summary>
    /// Fluent builder for creating HttpContext instances for middleware testing.
    /// Supports all common test scenarios including streaming, various providers, and media types.
    /// </summary>
    public class HttpContextBuilder
    {
        private readonly DefaultHttpContext _context;
        private string _path = "/v1/chat/completions";
        private string _method = "POST";
        private int _statusCode = 200;
        private int? _virtualKeyId;
        private string? _virtualKey;
        private string _providerType = "OpenAI";
        private int? _providerId;
        private int? _modelCostId;
        private bool _isStreaming;
        private Usage? _streamingUsage;
        private string? _streamingModel;
        private ToolUsageData? _streamingToolUsage;
        private DateTime? _requestStartTime;
        private string? _testResponseBody;
        private readonly Dictionary<string, object> _additionalItems = new();
        private IUsageContext? _usageContext;

        /// <summary>
        /// Initializes a new HttpContext builder with default settings.
        /// </summary>
        public HttpContextBuilder()
        {
            _context = new DefaultHttpContext();
            _context.Response.Body = new MemoryStream();
        }

        /// <summary>
        /// Sets the request path. Defaults to /v1/chat/completions.
        /// </summary>
        /// <param name="path">The request path.</param>
        public HttpContextBuilder WithPath(string path)
        {
            _path = path;
            return this;
        }

        /// <summary>
        /// Sets the HTTP method. Defaults to POST.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        public HttpContextBuilder WithMethod(string method)
        {
            _method = method;
            return this;
        }

        /// <summary>
        /// Configures for chat completions endpoint.
        /// </summary>
        public HttpContextBuilder ForChatCompletions() => WithPath("/v1/chat/completions");

        /// <summary>
        /// Configures for image generations endpoint.
        /// </summary>
        public HttpContextBuilder ForImageGenerations() => WithPath("/v1/images/generations");

        /// <summary>
        /// Configures for video generations endpoint.
        /// </summary>
        public HttpContextBuilder ForVideoGenerations() => WithPath("/v1/videos/generations");

        /// <summary>
        /// Configures for embeddings endpoint.
        /// </summary>
        public HttpContextBuilder ForEmbeddings() => WithPath("/v1/embeddings");

        /// <summary>
        /// Configures for audio transcriptions endpoint.
        /// </summary>
        public HttpContextBuilder ForAudioTranscriptions() => WithPath("/v1/audio/transcriptions");

        /// <summary>
        /// Configures for audio speech (TTS) endpoint.
        /// </summary>
        public HttpContextBuilder ForAudioSpeech() => WithPath("/v1/audio/speech");

        /// <summary>
        /// Configures for functions endpoint.
        /// </summary>
        public HttpContextBuilder ForFunctions() => WithPath("/v1/functions");

        /// <summary>
        /// Configures for a polling endpoint (excluded from usage tracking).
        /// </summary>
        public HttpContextBuilder ForPolling() => WithPath("/v1/videos/tasks/123/status");

        /// <summary>
        /// Configures for a non-API path (excluded from usage tracking).
        /// </summary>
        public HttpContextBuilder ForNonApiPath() => WithPath("/health");

        /// <summary>
        /// Sets the virtual key context for authenticated requests.
        /// </summary>
        /// <param name="id">The virtual key ID.</param>
        /// <param name="key">The virtual key value. Defaults to "test-key-{id}".</param>
        public HttpContextBuilder WithVirtualKey(int id, string? key = null)
        {
            _virtualKeyId = id;
            _virtualKey = key ?? $"test-key-{id}";
            return this;
        }

        /// <summary>
        /// Configures the provider type.
        /// </summary>
        /// <param name="providerType">The provider type name.</param>
        public HttpContextBuilder WithProvider(string providerType)
        {
            _providerType = providerType;
            return this;
        }

        /// <summary>
        /// Configures as an OpenAI provider request.
        /// </summary>
        public HttpContextBuilder AsOpenAI() => WithProvider("OpenAI");

        /// <summary>
        /// Configures as an Anthropic provider request.
        /// </summary>
        public HttpContextBuilder AsAnthropic() => WithProvider("Anthropic");

        /// <summary>
        /// Configures as a Groq provider request.
        /// </summary>
        public HttpContextBuilder AsGroq() => WithProvider("Groq");

        /// <summary>
        /// Configures as a Cerebras provider request.
        /// </summary>
        public HttpContextBuilder AsCerebras() => WithProvider("Cerebras");

        /// <summary>
        /// Sets the provider ID for database lookups.
        /// </summary>
        /// <param name="providerId">The provider ID.</param>
        public HttpContextBuilder WithProviderId(int providerId)
        {
            _providerId = providerId;
            return this;
        }

        /// <summary>
        /// Sets the model cost ID for direct cost lookups.
        /// </summary>
        /// <param name="modelCostId">The model cost ID.</param>
        public HttpContextBuilder WithModelCostId(int modelCostId)
        {
            _modelCostId = modelCostId;
            return this;
        }

        /// <summary>
        /// Sets the response status code.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        public HttpContextBuilder WithStatusCode(int statusCode)
        {
            _statusCode = statusCode;
            return this;
        }

        /// <summary>
        /// Configures as an error response.
        /// </summary>
        /// <param name="statusCode">The error status code. Defaults to 500.</param>
        public HttpContextBuilder AsError(int statusCode = 500)
        {
            _statusCode = statusCode;
            return this;
        }

        /// <summary>
        /// Configures as a rate-limited response (429).
        /// </summary>
        public HttpContextBuilder AsRateLimited() => AsError(429);

        /// <summary>
        /// Configures as a bad request response (400).
        /// </summary>
        public HttpContextBuilder AsBadRequest() => AsError(400);

        /// <summary>
        /// Configures as an unauthorized response (401).
        /// </summary>
        public HttpContextBuilder AsUnauthorized() => AsError(401);

        /// <summary>
        /// Configures for streaming response.
        /// </summary>
        /// <param name="usage">Optional streaming usage data.</param>
        /// <param name="model">Optional model name for streaming.</param>
        public HttpContextBuilder AsStreaming(Usage? usage = null, string? model = null)
        {
            _isStreaming = true;
            _streamingUsage = usage;
            _streamingModel = model;
            return this;
        }

        /// <summary>
        /// Configures streaming tool usage data.
        /// </summary>
        /// <param name="toolUsage">The tool usage data.</param>
        public HttpContextBuilder WithStreamingToolUsage(ToolUsageData toolUsage)
        {
            _streamingToolUsage = toolUsage;
            return this;
        }

        /// <summary>
        /// Sets the request start time for response time calculations.
        /// </summary>
        /// <param name="startTime">The request start time.</param>
        public HttpContextBuilder WithRequestStartTime(DateTime startTime)
        {
            _requestStartTime = startTime;
            return this;
        }

        /// <summary>
        /// Sets the test response body for the next delegate to write.
        /// Used with MiddlewareInvoker for tests that simulate controller responses.
        /// </summary>
        /// <param name="responseBody">The JSON response body.</param>
        public HttpContextBuilder WithTestResponseBody(string responseBody)
        {
            _testResponseBody = responseBody;
            return this;
        }

        /// <summary>
        /// Sets the trace identifier for the request.
        /// </summary>
        /// <param name="traceId">The trace identifier.</param>
        public HttpContextBuilder WithTraceId(string traceId)
        {
            _context.TraceIdentifier = traceId;
            return this;
        }

        /// <summary>
        /// Sets image request metadata.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <param name="quality">Image quality (e.g., "standard", "hd").</param>
        /// <param name="size">Image size (e.g., "1024x1024").</param>
        /// <param name="n">Number of images requested.</param>
        public HttpContextBuilder WithImageRequest(
            string model,
            string quality = "standard",
            string size = "1024x1024",
            int n = 1)
        {
            _usageContext = new ImageUsageContext
            {
                Model = model,
                Quality = quality,
                Size = size,
                N = n
            };
            return this;
        }

        /// <summary>
        /// Sets video request metadata.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <param name="duration">Video duration in seconds.</param>
        /// <param name="size">Video size/resolution.</param>
        /// <param name="n">Number of videos requested.</param>
        public HttpContextBuilder WithVideoRequest(
            string model,
            int? duration = null,
            string? size = null,
            int n = 1)
        {
            _usageContext = new VideoUsageContext
            {
                Model = model,
                Duration = duration,
                Size = size,
                N = n
            };
            return this;
        }

        /// <summary>
        /// Adds a custom item to HttpContext.Items.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">The item value.</param>
        public HttpContextBuilder WithItem(string key, object value)
        {
            _additionalItems[key] = value;
            return this;
        }

        /// <summary>
        /// Marks the usage as estimated (for streaming responses without exact counts).
        /// </summary>
        public HttpContextBuilder WithEstimatedUsage()
        {
            _additionalItems["UsageIsEstimated"] = true;
            return this;
        }

        /// <summary>
        /// Builds the configured HttpContext instance.
        /// </summary>
        /// <returns>The configured HttpContext.</returns>
        public HttpContext Build()
        {
            _context.Request.Path = _path;
            _context.Request.Method = _method;
            _context.Response.StatusCode = _statusCode;

            if (_virtualKeyId.HasValue)
            {
                _context.Items["VirtualKeyId"] = _virtualKeyId.Value;
                _context.Items["VirtualKey"] = _virtualKey;
            }

            _context.Items["ProviderType"] = _providerType;

            if (_providerId.HasValue)
                _context.Items["ProviderId"] = _providerId.Value;

            if (_modelCostId.HasValue)
                _context.Items["ModelCostId"] = _modelCostId.Value;

            if (_isStreaming)
            {
                _context.Items["IsStreamingRequest"] = true;
                var accounting = _context.GetOrCreateRequestAccountingContext();
                accounting.SetOperation(RequestOperation.ChatCompletion, _virtualKeyId, _streamingModel);
                if (_streamingUsage != null && !string.IsNullOrWhiteSpace(_streamingModel))
                    accounting.RecordProviderUsage(_streamingUsage, _streamingModel, UsageEvidenceSource.Provider);
                if (_streamingToolUsage != null)
                {
                    accounting.RecordProviderToolUsage(new ProviderToolUsage
                    {
                        Tools = _streamingToolUsage.Tools.Select(tool => new ProviderToolUsageItem
                        {
                            ToolName = tool.ToolName,
                            Count = tool.Count,
                            DurationSeconds = tool.DurationSeconds
                        }).ToList()
                    });
                }
            }

            if (_requestStartTime.HasValue)
                _context.Items["RequestStartTime"] = _requestStartTime.Value;

            if (_testResponseBody != null)
                _context.Items["TestResponseBody"] = _testResponseBody;

            foreach (var item in _additionalItems)
                _context.Items[item.Key] = item.Value;

            if (_usageContext != null)
                _context.SetUsageContext(_usageContext);

            return _context;
        }

        /// <summary>
        /// Creates a simple context for path filtering tests without virtual key.
        /// </summary>
        /// <param name="path">The request path.</param>
        public static HttpContext CreateForPathTest(string path)
        {
            return new HttpContextBuilder()
                .WithPath(path)
                .Build();
        }

        /// <summary>
        /// Creates a context for error response testing.
        /// </summary>
        /// <param name="statusCode">The error status code.</param>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        public static HttpContext CreateForErrorTest(int statusCode, int virtualKeyId = 123)
        {
            return new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(virtualKeyId)
                .AsError(statusCode)
                .Build();
        }

        /// <summary>
        /// Creates a context for streaming tests.
        /// </summary>
        /// <param name="usage">The streaming usage data.</param>
        /// <param name="model">The model name.</param>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        public static HttpContext CreateForStreamingTest(
            Usage usage,
            string model,
            int virtualKeyId = 123)
        {
            return new HttpContextBuilder()
                .ForChatCompletions()
                .WithVirtualKey(virtualKeyId)
                .AsStreaming(usage, model)
                .Build();
        }
    }
}
