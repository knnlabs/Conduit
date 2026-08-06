using System.Diagnostics;

using ConduitLLM.Core.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Middleware
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Core")]
    public class CorrelationIdMiddlewareTests
    {
        private readonly Mock<ILogger<CorrelationIdMiddleware>> _mockLogger;

        public CorrelationIdMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
        }

        [Fact]
        public async Task GeneratesNewCorrelationId_WhenNoHeadersPresent()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            string? capturedTraceId = null;

            RequestDelegate next = ctx =>
            {
                capturedTraceId = ctx.TraceIdentifier;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotNull(capturedTraceId);
            Assert.True(Guid.TryParse(capturedTraceId, out _), "Expected a valid GUID");
        }

        [Fact]
        public async Task IgnoresLegacyCorrelationHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["X-Correlation-ID"] = "legacy-id";
            string? capturedTraceId = null;

            RequestDelegate next = ctx =>
            {
                capturedTraceId = ctx.TraceIdentifier;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotEqual("legacy-id", capturedTraceId);
            Assert.True(Guid.TryParse(capturedTraceId, out _));
        }

        [Fact]
        public async Task ExtractsCorrelationId_FromXRequestIDHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["x-request-id"] = "req-456";
            string? capturedTraceId = null;

            RequestDelegate next = ctx =>
            {
                capturedTraceId = ctx.TraceIdentifier;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal("req-456", capturedTraceId);
        }

        [Fact]
        public async Task IgnoresLegacyTraceIdHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["X-Trace-ID"] = "trace-789";
            string? capturedTraceId = null;

            RequestDelegate next = ctx =>
            {
                capturedTraceId = ctx.TraceIdentifier;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotEqual("trace-789", capturedTraceId);
            Assert.True(Guid.TryParse(capturedTraceId, out _));
        }

        [Fact]
        public async Task ExtractsTraceId_FromTraceparentHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
            string? capturedTraceId = null;

            RequestDelegate next = ctx =>
            {
                capturedTraceId = ctx.TraceIdentifier;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", capturedTraceId);
        }

        [Fact]
        public async Task SetsContextItems_WithCorrelationId()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["x-request-id"] = "test-corr-id";
            object? capturedItemValue = null;

            RequestDelegate next = ctx =>
            {
                ctx.Items.TryGetValue(CorrelationIdOptions.CorrelationIdItemsKey, out capturedItemValue);
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal("test-corr-id", capturedItemValue);
        }

        [Fact]
        public async Task AddsResponseHeader_WhenIncludeInResponseTrue()
        {
            // Arrange
            // DefaultHttpContext doesn't fire OnStarting callbacks automatically,
            // so we use a custom IHttpResponseFeature that captures and invokes them.
            var onStartingCallbacks = new List<(Func<object, Task> callback, object state)>();

            var features = new FeatureCollection();
            features.Set<IHttpResponseFeature>(new TestResponseFeature(onStartingCallbacks));
            features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            var context = new DefaultHttpContext(features);
            context.Request.Headers["x-request-id"] = "resp-header-test";

            RequestDelegate next = _ => Task.CompletedTask;

            var options = new CorrelationIdOptions { IncludeInResponse = true };
            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object, options);

            // Act
            await middleware.InvokeAsync(context);

            // Fire the registered OnStarting callbacks (simulating response start)
            foreach (var (callback, state) in onStartingCallbacks)
            {
                await callback(state);
            }

            // Assert
            Assert.Equal("resp-header-test", context.Response.Headers["x-request-id"]);
        }

        [Fact]
        public async Task OmitsResponseHeader_WhenIncludeInResponseFalse()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["x-request-id"] = "no-resp-header";

            RequestDelegate next = ctx => Task.CompletedTask;

            var options = new CorrelationIdOptions { IncludeInResponse = false };
            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object, options);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.False(context.Response.Headers.ContainsKey("x-request-id"));
        }

        [Fact]
        public async Task UsesShortIds_WhenConfigured()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            string? capturedTraceId = null;

            RequestDelegate next = ctx =>
            {
                capturedTraceId = ctx.TraceIdentifier;
                return Task.CompletedTask;
            };

            var options = new CorrelationIdOptions { UseShortIds = true };
            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object, options);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotNull(capturedTraceId);
            Assert.Equal(8, capturedTraceId.Length);
        }

        [Fact]
        public void GetCorrelationId_ReturnsFromItems()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Items[CorrelationIdOptions.CorrelationIdItemsKey] = "items-corr-id";
            context.TraceIdentifier = "trace-id-fallback";

            // Act
            var result = context.GetCorrelationId();

            // Assert
            Assert.Equal("items-corr-id", result);
        }

        [Fact]
        public void GetCorrelationId_FallsBackToTraceIdentifier()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.TraceIdentifier = "trace-id-fallback";

            // Act
            var result = context.GetCorrelationId();

            // Assert
            Assert.Equal("trace-id-fallback", result);
        }

        [Fact]
        public async Task BeginsLoggingScope_WithCorrelationId()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["x-request-id"] = "scope-test-id";

            Dictionary<string, object>? capturedScope = null;
            _mockLogger
                .Setup(x => x.BeginScope(It.IsAny<It.IsAnyType>()))
                .Callback<object>(state =>
                {
                    if (state is Dictionary<string, object> dict)
                    {
                        capturedScope = new Dictionary<string, object>(dict);
                    }
                })
                .Returns(Mock.Of<IDisposable>());

            RequestDelegate next = _ => Task.CompletedTask;
            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.NotNull(capturedScope);
            Assert.True(capturedScope.ContainsKey("CorrelationId"));
            Assert.Equal("scope-test-id", capturedScope["CorrelationId"]);
        }

        [Fact]
        public async Task SetsActivityBaggageAndTag_WhenActivityExists()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Headers["x-request-id"] = "activity-test-id";

            RequestDelegate next = _ => Task.CompletedTask;
            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Create an Activity so the middleware can set baggage/tags
            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            using var source = new ActivitySource("test");
            using var activity = source.StartActivity("test-operation");
            Assert.NotNull(activity); // Ensure activity was created

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal("activity-test-id", activity.GetBaggageItem("correlation.id"));

            var tag = activity.Tags.FirstOrDefault(t => t.Key == "correlation.id");
            Assert.Equal("activity-test-id", tag.Value);
        }

        [Fact]
        public async Task CallsNextDelegate()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var nextCalled = false;

            RequestDelegate next = _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
        }
    }

    /// <summary>
    /// Test helper that captures OnStarting callbacks so they can be manually invoked.
    /// </summary>
    internal class TestResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> callback, object state)> _onStartingCallbacks;

        public TestResponseFeature(List<(Func<object, Task>, object)> onStartingCallbacks)
        {
            _onStartingCallbacks = onStartingCallbacks;
        }

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state)
        {
            _onStartingCallbacks.Add((callback, state));
        }

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
