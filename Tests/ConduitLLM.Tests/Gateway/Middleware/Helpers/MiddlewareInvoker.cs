using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.UsageTracking;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Core;
using ConduitLLM.Configuration;
using ConduitLLM.Tests.Http.Middleware.Fixtures;

namespace ConduitLLM.Tests.Http.Middleware.Helpers
{
    /// <summary>
    /// Helper for invoking UsageTrackingMiddleware with all required dependencies.
    /// Simplifies test setup by encapsulating the parameter explosion.
    /// </summary>
    public class MiddlewareInvoker
    {
        private readonly UsageTrackingMiddlewareTestFixture _fixture;
        private RequestDelegate? _nextDelegate;
        private string? _responseJson;
        private bool _isStreaming;

        /// <summary>
        /// Initializes a new middleware invoker with the given test fixture.
        /// </summary>
        /// <param name="fixture">The test fixture containing mocks.</param>
        public MiddlewareInvoker(UsageTrackingMiddlewareTestFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// Configures the JSON response that will be written by the next delegate.
        /// </summary>
        /// <param name="responseData">The response data object to serialize.</param>
        public MiddlewareInvoker WithResponse(object responseData)
        {
            _responseJson = JsonSerializer.Serialize(responseData);
            return this;
        }

        /// <summary>
        /// Configures the response using a pre-built JSON string.
        /// </summary>
        /// <param name="responseJson">The JSON response string.</param>
        public MiddlewareInvoker WithResponseJson(string responseJson)
        {
            _responseJson = responseJson;
            return this;
        }

        /// <summary>
        /// Uses a passthrough delegate (for streaming tests or when response body is set via context).
        /// </summary>
        public MiddlewareInvoker AsPassthrough()
        {
            _nextDelegate = ctx => Task.CompletedTask;
            return this;
        }

        /// <summary>
        /// Creates a middleware with a streaming response.
        /// The context items should already contain StreamingUsage and StreamingModel.
        /// </summary>
        public MiddlewareInvoker AsStreamingResponse()
        {
            _isStreaming = true;
            _nextDelegate = ctx =>
            {
                ctx.Response.ContentType = "text/event-stream";
                return Task.CompletedTask;
            };
            return this;
        }

        /// <summary>
        /// Uses a custom next delegate.
        /// </summary>
        /// <param name="next">The custom delegate.</param>
        public MiddlewareInvoker WithNextDelegate(RequestDelegate next)
        {
            _nextDelegate = next;
            return this;
        }

        /// <summary>
        /// Uses a delegate that reads response from TestResponseBody context item.
        /// This mimics how a controller would write the response.
        /// </summary>
        public MiddlewareInvoker WithTestResponseBodyDelegate()
        {
            _nextDelegate = async ctx =>
            {
                if (ctx.Items.TryGetValue("TestResponseBody", out var responseBody) &&
                    responseBody is string responseJson)
                {
                    PublishTypedEvidence(ctx, responseJson);
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(responseJson);
                }
            };
            return this;
        }

        /// <summary>
        /// Invokes the middleware and returns the context for assertions.
        /// Uses the mocked ToolCostService from the fixture.
        /// </summary>
        /// <param name="context">The HTTP context to process.</param>
        /// <returns>The processed HTTP context.</returns>
        public async Task<HttpContext> InvokeAsync(HttpContext context)
        {
            var next = _nextDelegate ?? CreateJsonResponseDelegate();
            var middleware = new UsageTrackingMiddleware(next, _fixture.Logger.Object);

            await middleware.InvokeAsync(
                context,
                _fixture.CostService.Object,
                _fixture.BatchSpendService.Object,
                _fixture.RequestLogService.Object,
                _fixture.VirtualKeyService.Object,
                _fixture.BillingAuditService.Object,
                _fixture.ToolCostService.Object);

            return context;
        }

        /// <summary>
        /// Invokes the middleware with a real tool cost calculation service.
        /// Use this for tool usage integration tests that need database lookups.
        /// </summary>
        /// <param name="context">The HTTP context to process.</param>
        /// <returns>The processed HTTP context.</returns>
        public async Task<HttpContext> InvokeWithRealToolServiceAsync(HttpContext context)
        {
            var next = _nextDelegate ?? CreateJsonResponseDelegate();
            var middleware = new UsageTrackingMiddleware(next, _fixture.Logger.Object);

            await middleware.InvokeAsync(
                context,
                _fixture.CostService.Object,
                _fixture.BatchSpendService.Object,
                _fixture.RequestLogService.Object,
                _fixture.VirtualKeyService.Object,
                _fixture.BillingAuditService.Object,
                _fixture.GetRealToolCostService());

            return context;
        }

        /// <summary>
        /// Resets the invoker to its initial state for reuse.
        /// </summary>
        public MiddlewareInvoker Reset()
        {
            _nextDelegate = null;
            _responseJson = null;
            _isStreaming = false;
            return this;
        }

        private RequestDelegate CreateJsonResponseDelegate()
        {
            var json = _responseJson ?? "{}";
            var bytes = Encoding.UTF8.GetBytes(json);

            return async ctx =>
            {
                PublishTypedEvidence(ctx, json);
                ctx.Response.ContentType = _isStreaming ? "text/event-stream" : "application/json";
                ctx.Response.StatusCode = ctx.Response.StatusCode > 0 ? ctx.Response.StatusCode : 200;
                ctx.Response.ContentLength = bytes.Length;
                await ctx.Response.Body.WriteAsync(bytes);
            };
        }

        private void PublishTypedEvidence(HttpContext context, string json)
        {
            if (_isStreaming)
                return;

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            var operation = path switch
            {
                var value when value.Contains("/embeddings") => RequestOperation.Embedding,
                var value when value.Contains("/rerank") => RequestOperation.Rerank,
                var value when value.Contains("/images/") => RequestOperation.Image,
                var value when value.Contains("/videos/") => RequestOperation.Video,
                var value when value.Contains("/audio/") => RequestOperation.Audio,
                var value when value.Contains("/functions/") => RequestOperation.Function,
                var value when value.Contains("/completions") => RequestOperation.ChatCompletion,
                _ => RequestOperation.Unknown
            };
            if (operation == RequestOperation.Unknown)
                return;

            var virtualKeyId = context.Items.TryGetValue("VirtualKeyId", out var keyValue) && keyValue is int keyId
                ? keyId
                : (int?)null;
            var accounting = context.GetOrCreateRequestAccountingContext();

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var usageContext = context.GetUsageContext();
                var model = root.TryGetProperty("model", out var modelElement)
                    ? modelElement.GetString()
                    : usageContext?.Model;
                model ??= "unknown";
                accounting.SetOperation(operation, virtualKeyId, model);

                if (operation == RequestOperation.Function)
                {
                    var actualCost = TryReadDecimal(root, "actualCost") ??
                                     TryReadDecimal(root, "ActualCost") ??
                                     TryReadDecimal(root, "estimatedCost") ??
                                     TryReadDecimal(root, "EstimatedCost");
                    if (actualCost is null &&
                        (root.TryGetProperty("actualCost", out _) ||
                         root.TryGetProperty("ActualCost", out _) ||
                         root.TryGetProperty("estimatedCost", out _) ||
                         root.TryGetProperty("EstimatedCost", out _)))
                    {
                        return;
                    }
                    var executionId = root.TryGetProperty("executionId", out var executionIdElement)
                        ? executionIdElement.ToString()
                        : null;
                    accounting.RecordDirectCost(new DirectCostEvidence(
                        context.Items.TryGetValue("FunctionConfigurationName", out var functionName)
                            ? functionName?.ToString() ?? "function"
                            : "function",
                        actualCost ?? 0m,
                        executionId,
                        null));
                    return;
                }

                Usage? usage = null;
                if (root.TryGetProperty("usage", out var usageElement))
                    usage = UsageExtractor.ExtractUsage(usageElement, _fixture.Logger.Object);

                if (operation == RequestOperation.Image)
                {
                    usage ??= new Usage();
                    usage.ImageCount ??= root.TryGetProperty("data", out var imageData) && imageData.ValueKind == JsonValueKind.Array
                        ? imageData.GetArrayLength()
                        : (usageContext as ImageUsageContext)?.N ?? 1;
                    usage.ImageQuality ??= (usageContext as ImageUsageContext)?.Quality;
                    usage.ImageResolution ??= (usageContext as ImageUsageContext)?.Size;
                }
                else if (operation == RequestOperation.Video && usageContext is VideoUsageContext video)
                {
                    usage ??= new Usage();
                    usage.VideoDurationSeconds ??= video.Duration;
                    usage.VideoResolution ??= video.Size;
                    usage.PricingParameters ??= video.PricingParameters;
                }
                else if (operation == RequestOperation.Audio && usageContext is AudioUsageContext audio)
                {
                    usage = new Usage
                    {
                        AudioDurationSeconds = audio.AudioDurationSeconds,
                        TtsCharacters = audio.TtsCharacters
                    };
                }

                if (usage is not null && model != "unknown")
                {
                    accounting.RecordProviderUsage(usage, model, UsageEvidenceSource.Provider);
                }

                if (context.Items.TryGetValue("ChatFunctionCost", out var functionCostValue) &&
                    functionCostValue is decimal functionCost)
                {
                    accounting.RecordFunctionExecutions([], functionCost);
                }
                if (context.Items.TryGetValue("ChatProviderCalls", out var providerCallsValue) &&
                    providerCallsValue is IEnumerable<ProviderCallUsage> providerCalls)
                {
                    accounting.RecordProviderCalls(providerCalls);
                }

                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeValue) &&
                                   Enum.TryParse<ProviderType>(providerTypeValue?.ToString(), true, out var parsedProvider)
                    ? parsedProvider
                    : ProviderType.OpenAI;
                var hostedTools = UsageExtractor.ExtractToolUsage(json, providerType, _fixture.Logger.Object);
                if (hostedTools is not null)
                {
                    accounting.RecordProviderToolUsage(new ProviderToolUsage
                    {
                        Tools = hostedTools.Tools.Select(tool => new ProviderToolUsageItem
                        {
                            ToolName = tool.ToolName,
                            Count = tool.Count,
                            DurationSeconds = tool.DurationSeconds
                        }).ToList()
                    });
                }
            }
            catch (JsonException)
            {
                accounting.SetOperation(operation, virtualKeyId, context.GetUsageContext()?.Model);
            }
        }

        private static decimal? TryReadDecimal(JsonElement root, string propertyName) =>
            root.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetDecimal(out var value)
                ? value
                : null;

        /// <summary>
        /// Creates a new invoker with the given fixture.
        /// Static factory method for fluent usage.
        /// </summary>
        /// <param name="fixture">The test fixture.</param>
        public static MiddlewareInvoker Create(UsageTrackingMiddlewareTestFixture fixture)
            => new(fixture);
    }
}
