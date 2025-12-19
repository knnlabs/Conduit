using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ConduitLLM.Gateway.Middleware;
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
                ctx.Response.ContentType = _isStreaming ? "text/event-stream" : "application/json";
                ctx.Response.StatusCode = ctx.Response.StatusCode > 0 ? ctx.Response.StatusCode : 200;
                ctx.Response.ContentLength = bytes.Length;
                await ctx.Response.Body.WriteAsync(bytes);
            };
        }

        /// <summary>
        /// Creates a new invoker with the given fixture.
        /// Static factory method for fluent usage.
        /// </summary>
        /// <param name="fixture">The test fixture.</param>
        public static MiddlewareInvoker Create(UsageTrackingMiddlewareTestFixture fixture)
            => new(fixture);
    }
}
