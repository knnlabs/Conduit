using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Http.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Http.Middleware
{
    public class CorrelationIdMiddlewareTests
    {
        private readonly Mock<ILogger<CorrelationIdMiddleware>> _mockLogger;
        private readonly CorrelationIdMiddleware _middleware;
        private readonly Mock<RequestDelegate> _mockNext;

        public CorrelationIdMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
            _mockNext = new Mock<RequestDelegate>();
            _middleware = new CorrelationIdMiddleware(_mockNext.Object, _mockLogger.Object, null);
        }

        [Fact]
        public async Task InvokeAsync_ExistingCorrelationId_UsesExistingId()
        {
            // Arrange
            var existingCorrelationId = "existing-correlation-id";
            var context = new DefaultHttpContext();
            var responseFeature = new TestHttpResponseFeature();
            context.Features.Set<IHttpResponseFeature>(responseFeature);
            context.Request.Headers["X-Correlation-ID"] = existingCorrelationId;

            // Act
            await _middleware.InvokeAsync(context);
            await responseFeature.FireOnStarting();

            // Assert
            Assert.Equal(existingCorrelationId, context.Items["CorrelationId"]);
            Assert.Equal(existingCorrelationId, context.Response.Headers["X-Correlation-ID"]);
            _mockNext.Verify(next => next(context), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_NoCorrelationId_GeneratesNewId()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseFeature = new TestHttpResponseFeature();
            context.Features.Set<IHttpResponseFeature>(responseFeature);

            // Act
            await _middleware.InvokeAsync(context);
            await responseFeature.FireOnStarting();

            // Assert
            Assert.NotNull(context.Items["CorrelationId"]);
            var generatedId = context.Items["CorrelationId"] as string;
            Assert.NotNull(generatedId);
            Assert.NotEmpty(generatedId);
            Assert.Equal(generatedId, context.Response.Headers["X-Correlation-ID"]);
            _mockNext.Verify(next => next(context), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_AlternativeHeaderNames_RecognizesCorrelationId()
        {
            // Arrange
            var correlationId = "request-id-123";
            var context = new DefaultHttpContext();
            var responseFeature = new TestHttpResponseFeature();
            context.Features.Set<IHttpResponseFeature>(responseFeature);
            context.Request.Headers["X-Request-ID"] = correlationId;

            // Act
            await _middleware.InvokeAsync(context);
            await responseFeature.FireOnStarting();

            // Assert
            Assert.Equal(correlationId, context.Items["CorrelationId"]);
            Assert.Equal(correlationId, context.Response.Headers["X-Correlation-ID"]);
        }

        [Fact]
        public async Task InvokeAsync_W3CTraceContext_ExtractsCorrelationFromTraceparent()
        {
            // Arrange
            var traceId = "12345678901234567890123456789012";
            var spanId = "1234567890123456";
            var traceparent = $"00-{traceId}-{spanId}-01";
            var context = new DefaultHttpContext();
            context.Request.Headers["traceparent"] = traceparent;

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            var correlationId = context.Items["CorrelationId"] as string;
            Assert.NotNull(correlationId);
            Assert.Contains(traceId, correlationId);
        }

        [Fact]
        public async Task InvokeAsync_SetsActivityBaggage()
        {
            // Arrange
            var correlationId = "test-correlation-id";
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Correlation-ID"] = correlationId;

            Activity? activityDuringMiddleware = null;
            _mockNext.Setup(next => next(It.IsAny<HttpContext>()))
                .Callback<HttpContext>(_ =>
                {
                    activityDuringMiddleware = Activity.Current;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.NotNull(activityDuringMiddleware);
            var baggageValue = activityDuringMiddleware!.GetBaggageItem("correlation.id");
            Assert.Equal(correlationId, baggageValue);
        }

        [Fact]
        public async Task InvokeAsync_EmptyCorrelationId_GeneratesNewId()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseFeature = new TestHttpResponseFeature();
            context.Features.Set<IHttpResponseFeature>(responseFeature);
            context.Request.Headers["X-Correlation-ID"] = " ";

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            var correlationId = context.Items["CorrelationId"] as string;
            Assert.NotNull(correlationId);
            Assert.NotEmpty(correlationId);
            Assert.NotEqual(" ", correlationId);
        }

        [Fact]
        public async Task InvokeAsync_Exception_StillReturnsCorrelationId()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseFeature = new TestHttpResponseFeature();
            context.Features.Set<IHttpResponseFeature>(responseFeature);
            var expectedException = new InvalidOperationException("Test exception");
            _mockNext.Setup(next => next(It.IsAny<HttpContext>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _middleware.InvokeAsync(context));
            await responseFeature.FireOnStarting();
            
            // The correlation ID should still be set in the response headers
            Assert.NotNull(context.Items["CorrelationId"]);
            Assert.True(context.Response.Headers["X-Correlation-ID"].Count > 0);
        }

        [Fact]
        public async Task InvokeAsync_MultipleCorrelationHeaders_UsesFirstValid()
        {
            // Arrange
            var firstId = "first-id";
            var secondId = "second-id";
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Correlation-ID"] = firstId;
            context.Request.Headers["X-Request-ID"] = secondId;

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(firstId, context.Items["CorrelationId"]);
        }
    }

    // Test helper class to handle response OnStarting callbacks
    internal class TestHttpResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task>, object)> _onStartingCallbacks = new();
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state)
        {
            _onStartingCallbacks.Add((callback, state));
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStarting()
        {
            foreach (var (callback, state) in _onStartingCallbacks)
            {
                await callback(state);
            }
        }
    }
}